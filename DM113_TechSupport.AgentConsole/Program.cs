using Grpc.Net.Client;
using dm113_tech_support;
using System.IO;
using Grpc.Core;

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("\n");
Console.WriteLine("  ╔════════════════════════════════════════════════════╗");
Console.WriteLine("  ║      Sistema de Suporte Técnico | SISUTÉ           ║");
Console.WriteLine("  ╠════════════════════════════════════════════════════╣");
Console.WriteLine("  ║                                                    ║");
Console.WriteLine("  ║              INSTRUÇÕES | ATENDENTE                ║");
Console.WriteLine("  ║                                                    ║");
Console.WriteLine("  ╠════════════════════════════════════════════════════╣");
Console.WriteLine("  ║  Passo 1 - Digite seu nome                         ║");
Console.WriteLine("  ║  Passo 2 - Digite o [numero] do Chamado            ║");
Console.WriteLine("  ║  Passo 3 - Converse com o cliente                  ║");
Console.WriteLine("  ║  Comando: /sair - Para encerrar o chat             ║");
Console.WriteLine("  ║                                                    ║");
Console.WriteLine("  ╚════════════════════════════════════════════════════╝");
Console.WriteLine("\n");
Console.ResetColor();

try
{
    var ticketFilePath = GetTicketFilePath();

    // Exibir chamados abertos
    if (!File.Exists(ticketFilePath))
    {
        Console.WriteLine(" > Nenhum chamado encontrado.");
        Console.WriteLine("\nPressione qualquer tecla para sair...");
        Console.ReadKey();
        return;
    }

    Console.Write("\n > Seu nome (atendente): ");
    var nome = Console.ReadLine();
    var sender = $" >>> [{nome}]";

    Console.WriteLine("\n > Chamados disponíveis:\n");
    var lines = File.ReadAllLines(ticketFilePath)
                    .Where(line => !string.IsNullOrWhiteSpace(line) && line.Split('|').Length >= 5 && line.Split('|')[4] == "aberto")
                    .ToList();

    if (lines.Count == 0)
    {
        Console.WriteLine(" > Nenhum chamado aberto no momento.");
        Console.WriteLine("\nPressione qualquer tecla para sair...");
        Console.ReadKey();
        return;
    }

    int index = 1;
    foreach (var line in lines)
    {
        var parts = line.Split('|');
        Console.WriteLine($" [{index}] - Ticket ID: {parts[0]} | Usuário: {parts[1]} | Descrição: {parts[2]}");
        index++;
    }

    Console.Write("\n > Digite o [ # ] que deseja atender: ");
    if (!int.TryParse(Console.ReadLine(), out int ticketIndex) || ticketIndex < 1 || ticketIndex > lines.Count)
    {
        Console.WriteLine(" > ID inválido. Encerrando o programa.");
        Console.WriteLine("\nPressione qualquer tecla para sair...");
        Console.ReadKey();
        return;
    }

    var ticketId = lines[ticketIndex - 1].Split('|')[0].Trim();
    Console.WriteLine($"\n > Entrou no chat do ticket {ticketId}...\n\n");

    // Iniciar canal gRPC
    using var channel = GrpcChannel.ForAddress("http://localhost:5000");
    var client = new Support.SupportClient(channel);

    using var call = client.ChatSupport();

    var cancellationTokenSource = new CancellationTokenSource();
    var isExiting = false;

    // --- Tarefa de Envio de Mensagens ---
    var sending = Task.Run(async () =>
    {
        try
        {
            // Mensagem inicial de entrada no chat
            await call.RequestStream.WriteAsync(new ChatMessage
            {
                TicketId = ticketId,
                Sender = sender,
                Message = $" >>> [Atendente {nome} entrou no chat]",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });

            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                var msg = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(msg)) continue;
                if (msg.Trim().ToLower() == "/sair")
                {
                    isExiting = true;
                    await call.RequestStream.WriteAsync(new ChatMessage
                    {
                        TicketId = ticketId,
                        Sender = sender,
                        Message = $" >>> [Atendente {nome} saiu do chat]",
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });

                    // Informa ao servidor que não enviará mais mensagens
                    await call.RequestStream.CompleteAsync();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\n✅ Saindo do chat... Até logo!");
                    Console.ResetColor();

                    cancellationTokenSource.Cancel(); // Para a tarefa de recebimento
                    break;
                }                

                await call.RequestStream.WriteAsync(new ChatMessage
                {
                    TicketId = ticketId,
                    Sender = sender,
                    Message = msg,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { /* Ignora */}
        catch (Exception ex) when (!isExiting)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Erro durante o envio: {ex.Message}");
            Console.ResetColor();
            cancellationTokenSource.Cancel();
        }
    });

    // --- Tarefa de Recebimento de Mensagens ---
    var receiving = Task.Run(async () =>
    {
        try
        {
            await foreach (var incoming in call.ResponseStream.ReadAllAsync(cancellationTokenSource.Token))
            {
                if (incoming.Sender != sender)
                {
                    var time = DateTimeOffset.FromUnixTimeSeconds(incoming.Timestamp).ToLocalTime().ToString("HH:mm:ss");
                    var senderName = incoming.Sender.PadRight(20);
                    Console.ForegroundColor = ConsoleColor.Cyan; // Cor do cliente
                    Console.WriteLine($"  [{time}] {senderName}: {incoming.Message}");
                    Console.ResetColor();
                }
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { /* Ignora */}
        catch (Exception ex) when (!isExiting)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Erro durante o recebimento: {ex.Message}");
            Console.ResetColor();
        }
    });

    await Task.WhenAny(sending, receiving);
}
catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("\n❌ Erro: Não foi possível conectar com o servidor.");
    Console.WriteLine("   Verifique se o servidor está rodando e tente novamente.");
    Console.ResetColor();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n❌ Erro geral: {ex.Message}");
    Console.ResetColor();
}

if (!Console.IsInputRedirected)
{
    Console.WriteLine("\nPressione qualquer tecla para sair...");
    Console.ReadKey();
}

string GetTicketFilePath()
{
    var basePath = AppContext.BaseDirectory;
    // Navega 4 níveis acima para chegar na raiz da solução e depois entra no projeto do servidor
    var rootPath = Path.GetFullPath(Path.Combine(basePath, @"..\..\..\..\DM113_TechSupport.Server\Logs"));
    return Path.Combine(rootPath, "tickets.txt");
}
using Grpc.Net.Client;
using dm113_tech_support;
using System.IO;
using Grpc.Core;

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("\n");
Console.WriteLine("  ╔════════════════════════════════════════════════════╗");
Console.WriteLine("  ║   Sistema de Suporte Técnico | SISUTÉ              ║");
Console.WriteLine("  ╠════════════════════════════════════════════════════╣");
Console.WriteLine("  ║                                                    ║");
Console.WriteLine("  ║           INSTRUÇÕES | ATENDENTE                   ║");
Console.WriteLine("  ║                                                    ║");
Console.WriteLine("  ╠════════════════════════════════════════════════════╣");
Console.WriteLine("  ║  Passo 1 - Digite seu nome                         ║");
Console.WriteLine("  ║  Passo 2 - Digite o [numero] do Chamado            ║");
Console.WriteLine("  ║  Passo 3 - Converse com o cliente                  ║");
Console.WriteLine("  ║  Comando: /sair - Para encerrar o chat             ║");
Console.WriteLine("  ║  Comando: /atualizar - Para recarregar a lista     ║");
Console.WriteLine("  ║                                                    ║");
Console.WriteLine("  ╚════════════════════════════════════════════════════╝");
Console.WriteLine("\n");
Console.ResetColor();

try
{
    var ticketFilePath = GetTicketFilePath();

    Console.Write("\n > Seu nome (atendente): ");
    var nome = Console.ReadLine();
    var sender = $" >>> [{nome}]";

    string ticketId = null;
    List<string> openTickets;

    while (true)
    {
        openTickets = DisplayAndGetOpenTickets(ticketFilePath);

        if (openTickets.Count == 0)
        {
            Console.WriteLine("\n > Nenhum chamado aberto no momento.");
            Console.WriteLine("\nPressione qualquer tecla para sair...");
            Console.ReadKey();
            return;
        }

        Console.Write("\n > Digite o [ # ] que deseja atender ou '/atualizar' para recarregar: ");
        var userInput = Console.ReadLine();

        if (userInput.Trim().Equals("/atualizar", StringComparison.OrdinalIgnoreCase))
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n > Atualizando lista de chamados...");
            Console.ResetColor();
            Thread.Sleep(1000); 
            continue;
        }

        if (!int.TryParse(userInput, out int ticketIndex) || ticketIndex < 1 || ticketIndex > openTickets.Count)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(" > Opção inválida. Tente novamente.");
            Console.ResetColor();
            Thread.Sleep(1500);
            Console.Clear();
            continue;
        }

        // Se a seleção for válida, armazena o ID do ticket e sai do loop
        ticketId = openTickets[ticketIndex - 1].Split('|')[0].Trim();
        break;
    }


    Console.WriteLine($"\n > Entrou no chat do ticket {ticketId}...\n\n");

    using var channel = GrpcChannel.ForAddress("http://localhost:5000");
    var client = new Support.SupportClient(channel);

    using var call = client.ChatSupport();

    var cancellationTokenSource = new CancellationTokenSource();
    var isExiting = false;

    var sending = Task.Run(async () =>
    {
        try
        {
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

                    
                    await call.RequestStream.CompleteAsync();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\n✅ Saindo do chat... Até logo!");
                    Console.ResetColor();

                    cancellationTokenSource.Cancel();
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
                    Console.ForegroundColor = ConsoleColor.Cyan;
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
    Console.WriteLine("  Verifique se o servidor está rodando e tente novamente.");
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

List<string> DisplayAndGetOpenTickets(string ticketFilePath)
{
    Console.WriteLine("\n > Chamados disponíveis:\n");
    if (!File.Exists(ticketFilePath))
    {
        Console.WriteLine(" > Nenhum chamado encontrado.");
        return new List<string>();
    }

    var lines = File.ReadAllLines(ticketFilePath)
                    .Where(line => !string.IsNullOrWhiteSpace(line) && line.Split('|').Length >= 5 && line.Split('|')[4] == "aberto")
                    .ToList();

    if (lines.Count > 0)
    {
        int index = 1;
        foreach (var line in lines)
        {
            var parts = line.Split('|');
            Console.WriteLine($" [{index}] - Ticket ID: {parts[0]} | Usuário: {parts[1]} | Descrição: {parts[2]}");
            index++;
        }
    }

    return lines;
}

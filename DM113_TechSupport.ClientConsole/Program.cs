using dm113_tech_support;
using Grpc.Core;
using Grpc.Net.Client;

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("\n");
Console.WriteLine("  ╔════════════════════════════════════════════════════╗");
Console.WriteLine("  ║      Sistema de Suporte Técnico | SISUTÉ           ║");
Console.WriteLine("  ╠════════════════════════════════════════════════════╣");
Console.WriteLine("  ║                                                    ║");
Console.WriteLine("  ║               INSTRUÇÕES | CLIENTE                 ║");
Console.WriteLine("  ║                                                    ║");
Console.WriteLine("  ╠════════════════════════════════════════════════════╣");
Console.WriteLine("  ║  Passo 1 - Digite seu nome                         ║");
Console.WriteLine("  ║  Passo 2 - Digite o problema do produto            ║");
Console.WriteLine("  ║  Passo 3 - Aguarde o atendente responder           ║");
Console.WriteLine("  ║  Comando: /sair - Para encerrar o chat             ║");
Console.WriteLine("  ║                                                    ║");
Console.WriteLine("  ╚════════════════════════════════════════════════════╝");
Console.WriteLine("\n");
Console.ResetColor();

try
{
    using var channel = GrpcChannel.ForAddress("http://localhost:5000");
    var client = new Support.SupportClient(channel);

    // Abrir chamado
    Console.Write("> Seu nome: ");
    var nome = Console.ReadLine();

    Console.Write("> Descreva o problema: ");
    var descricao = Console.ReadLine();

    OpenTicketReply resposta;
    try
    {
        resposta = await client.OpenTicketAsync(new OpenTicketRequest
        {
            UserName = nome,
            Description = descricao
        });
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("\n❌ Erro: Não foi possível conectar com o servidor.");
        Console.WriteLine("   Verifique se o servidor está rodando e tente novamente.");
        Console.ResetColor();
        Console.WriteLine("\nPressione qualquer tecla para sair...");
        Console.ReadKey();
        return;
    }

    Console.WriteLine($"\n >> Chamado aberto com ID: {resposta.TicketId}");
    Console.WriteLine("\n > Aguardando conexão do atendente... Digite mensagens para conversar.");
    Console.WriteLine(" > Digite '/sair' para encerrar o chat.\n");

    // Inicia sessão de chat
    using var call = client.ChatSupport();

    var cancellationTokenSource = new CancellationTokenSource();
    var isExiting = false;

    var sending = Task.Run(async () =>
    {
        try
        {
            await call.RequestStream.WriteAsync(new ChatMessage
            {
                TicketId = resposta.TicketId,
                Sender = nome,
                Message = "[Cliente entrou no chat]"
            });

            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                var msg = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(msg)) continue;

                // Verifica comando de saída
                if (msg.Trim().ToLower() == "/sair")
                {
                    isExiting = true;
                    await call.RequestStream.WriteAsync(new ChatMessage
                    {
                        TicketId = resposta.TicketId,
                        Sender = nome,
                        Message = "[Cliente saiu do chat]",
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });

                    // MOVA A CHAMADA PARA DENTRO DO LOOP DE SAÍDA
                    await call.RequestStream.CompleteAsync();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\n✅ Saindo do chat... Até logo!");
                    Console.ResetColor();

                    cancellationTokenSource.Cancel();
                    break;
                }

                await call.RequestStream.WriteAsync(new ChatMessage
                {
                    TicketId = resposta.TicketId,
                    Sender = nome,
                    Message = msg,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && isExiting)
        {
            // Ignora erro de cancelamento quando está saindo intencionalmente
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n❌ Conexão com o servidor foi perdida.");
            Console.ResetColor();
            cancellationTokenSource.Cancel();
        }
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
                if (incoming.Sender != nome)
                {
                    var time = DateTimeOffset.FromUnixTimeSeconds(incoming.Timestamp).ToLocalTime().ToString("HH:mm:ss");
                    var sender = incoming.Sender.PadRight(20);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"> [{time}] {sender}: {incoming.Message}");
                    Console.ResetColor();
                }
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && isExiting)
        {
            // Ignora erro de cancelamento quando está saindo intencionalmente
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            if (!isExiting)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n❌ Conexão com o servidor foi perdida.");
                Console.ResetColor();
            }
        }
        catch (Exception ex) when (!isExiting)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Erro durante o recebimento: {ex.Message}");
            Console.ResetColor();
        }
    });


    await Task.WhenAny(sending, receiving);
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
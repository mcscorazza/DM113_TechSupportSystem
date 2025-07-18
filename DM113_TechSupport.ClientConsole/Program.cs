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
Console.WriteLine("  ║                                                    ║");
Console.WriteLine("  ╚════════════════════════════════════════════════════╝");
Console.WriteLine("\n");
Console.ResetColor();
using var channel = GrpcChannel.ForAddress("http://localhost:5000");
var client = new Support.SupportClient(channel);

// Abrir chamado
Console.Write("> Seu nome: ");
var nome = Console.ReadLine();

Console.Write("> Descreva o problema: ");
var descricao = Console.ReadLine();

var resposta = await client.OpenTicketAsync(new OpenTicketRequest
{
    UserName = nome,
    Description = descricao
});

Console.WriteLine($"\n >> Chamado aberto com ID: {resposta.TicketId}");
Console.WriteLine("\n > Aguardando conexão do atendente... Digite mensagens para conversar.\n");

// Inicia sessão de chat
using var call = client.ChatSupport();

var sending = Task.Run(async () =>
{
    await call.RequestStream.WriteAsync(new ChatMessage
    {
        TicketId = resposta.TicketId,
        Sender = nome,
        Message = "[Cliente entrou no chat]"
    });
    while (true)
    {
        var msg = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(msg)) continue;

        await call.RequestStream.WriteAsync(new ChatMessage
        {
            TicketId = resposta.TicketId,
            Sender = nome,
            Message = msg,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
    }
});

var receiving = Task.Run(async () =>
{
    await foreach (var incoming in call.ResponseStream.ReadAllAsync())
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
});

await Task.WhenAny(sending, receiving);

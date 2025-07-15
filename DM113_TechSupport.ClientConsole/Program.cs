using dm113_tech_support;
using Grpc.Core;
using Grpc.Net.Client;

Console.WriteLine("🧑 CLIENTE - Sistema de Suporte Técnico");
using var channel = GrpcChannel.ForAddress("http://localhost:5000");
var client = new Support.SupportClient(channel);

// Abrir chamado
Console.Write("Seu nome: ");
var nome = Console.ReadLine();

Console.Write("Descreva o problema: ");
var descricao = Console.ReadLine();

var resposta = await client.OpenTicketAsync(new OpenTicketRequest
{
    UserName = nome,
    Description = descricao
});

Console.WriteLine($"\n✅ Chamado aberto com ID: {resposta.TicketId}");
Console.WriteLine("Aguardando conexão do atendente... Digite mensagens para conversar.\n");

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
        var msg = "[" + nome + "]" + Console.ReadLine();
        if (string.IsNullOrWhiteSpace(msg)) continue;

        await call.RequestStream.WriteAsync(new ChatMessage
        {
            TicketId = resposta.TicketId,
            Sender = nome,
            Message = msg
        });
    }
});

var receiving = Task.Run(async () =>
{
    await foreach (var incoming in call.ResponseStream.ReadAllAsync())
    {
        if (incoming.Sender != nome)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"{incoming.Sender}: {incoming.Message}");
            Console.ResetColor();
        }
    }
});

await Task.WhenAny(sending, receiving);

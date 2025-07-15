using Grpc.Net.Client;
using dm113_tech_support;
using System.IO;
using System.Threading.Channels;
using Grpc.Core;

Console.WriteLine("\n\nSistema de Suporte Técnico - ATENDENTE\n");
var ticketFilePath = GetTicketFilePath();

Console.WriteLine(ticketFilePath);

// Exibir chamados abertos
if (!File.Exists(ticketFilePath))
{
    Console.WriteLine(" > Nenhum chamado encontrado.");
    return;
}

Console.WriteLine("\n > Chamados disponíveis:\n");
var lines = File.ReadAllLines(ticketFilePath)
                .Where(line => line.Trim().Length > 0)
                .ToList();

foreach (var line in lines)
{
    var parts = line.Split('|');
    if (parts.Length >= 5 && parts[4] == "aberto")
    {
        Console.WriteLine($" > ID: {parts[0]} | Usuário: {parts[1]} | Descrição: {parts[2]}");
    }
}

Console.Write("\n > Digite o ticket_id que deseja atender: ");
var ticketId = Console.ReadLine();

Console.Write("\n > Seu nome (atendente): ");
var nome = Console.ReadLine();
var sender = $" >>> [{nome}]";

Console.WriteLine($"\n > Entrou no chat do ticket {ticketId}...\n\n");

// Iniciar canal gRPC
using var channel = GrpcChannel.ForAddress("http://localhost:5000");
var client = new Support.SupportClient(channel);

using var call = client.ChatSupport();

await call.RequestStream.WriteAsync(new ChatMessage
{
    TicketId = ticketId,
    Sender = sender,
    Message = $"  >>> [Atendente {nome} entrou no chat]",
});

var sending = Task.Run(async () =>
{
    while (true)
    {
        var msg = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(msg)) continue;

        await call.RequestStream.WriteAsync(new ChatMessage
        {
            TicketId = ticketId,
            Sender = sender,
            Message = "  >>> " + msg,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
    }
});

var receiving = Task.Run(async () =>
{
    await foreach (var incoming in call.ResponseStream.ReadAllAsync())
    {
        if (incoming.Sender != sender)
        {
            var time = DateTimeOffset.FromUnixTimeSeconds(incoming.Timestamp).ToLocalTime().ToString("HH:mm:ss");
            var sender = incoming.Sender.PadRight(20); // Alinha em 15 caracteres
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  [{time}] {sender}: {incoming.Message}");
            Console.ResetColor();
        }
    }
});

await Task.WhenAny(sending, receiving);

string GetTicketFilePath()
{
    var basePath = AppContext.BaseDirectory;
    var rootPath = Path.GetFullPath(Path.Combine(basePath, @"..\..\..\..\DM113_TechSupport.Server\Logs"));
    return Path.Combine(rootPath, "tickets.txt");
}
using dm113_tech_support;
using Grpc.Core;
using System.Collections.Concurrent;

namespace DM113_TechSupport.Server.Services;

public class SupportService : Support.SupportBase
{

    private static readonly ConcurrentDictionary<string, IServerStreamWriter<ChatMessage>> ClientStreams = new();
    private static readonly ConcurrentDictionary<string, IServerStreamWriter<ChatMessage>> AgentStreams = new();

    private static readonly string LogsPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
    private static readonly string TicketFilePath = Path.Combine(LogsPath, "tickets.txt");
    private static readonly string ChatLogsPath = Path.Combine(LogsPath, "Chats");


    public override Task<OpenTicketReply> OpenTicket(OpenTicketRequest request, ServerCallContext context)
    {
        Directory.CreateDirectory(LogsPath);

        string ticketId = GenerateTicketId();
        string log = $"{ticketId}|{request.UserName}|{request.Description}|{DateTime.UtcNow}|aberto";

        File.AppendAllText(TicketFilePath, log + Environment.NewLine);

        return Task.FromResult(new OpenTicketReply
        {
            TicketId = ticketId,
            Message = $"Chamado criado com ID: {ticketId}"
        });
    }

    public override async Task ChatSupport(
    IAsyncStreamReader<ChatMessage> requestStream,
    IServerStreamWriter<ChatMessage> responseStream,
    ServerCallContext context)
    {
        await foreach (var message in requestStream.ReadAllAsync())
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Msg recebida de {message.Sender}: {message.Message}");
            var isAgent = message.Sender.StartsWith(" >>> ");

            if (isAgent)
            {
                Console.WriteLine("-> Encaminhando para cliente");
                AgentStreams.TryAdd(message.TicketId, responseStream);

                if (ClientStreams.TryGetValue(message.TicketId, out var clientStream))
                {
                    await clientStream.WriteAsync(message);
                }
                else
                {
                    Console.WriteLine(" ! Cliente ainda não conectado.");
                }
            }
            else
            {
                Console.WriteLine("-> Encaminhando para atendente");
                ClientStreams.TryAdd(message.TicketId, responseStream);
                if (AgentStreams.TryGetValue(message.TicketId, out var agentStream))
                {
                    await agentStream.WriteAsync(message);
                }
                else
                {
                    Console.WriteLine(" ! Atendente ainda não conectado.");
                }
            }

            // Salvar log em arquivo
            Directory.CreateDirectory(ChatLogsPath);
            var logLine = $"[{DateTime.UtcNow}] {message.Sender}: {message.Message}";
            var logFilePath = Path.Combine(ChatLogsPath, $"chat_{message.TicketId}.log");
            File.AppendAllText(logFilePath, logLine + Environment.NewLine);
        }
    }

    private static string GenerateTicketId()
    {
        var datePart = DateTime.UtcNow.ToString("yyMMdd");
        var rand = new Random();
        var suffix = new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 4)
            .Select(s => s[rand.Next(s.Length)]).ToArray());

        return $"TKT-{datePart}-{suffix}";
    }
}


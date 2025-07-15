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

        string ticketId = Guid.NewGuid().ToString();
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
            var isAgent = message.Sender.StartsWith("Agent:");

            if (isAgent)
            {
                AgentStreams[message.TicketId] = responseStream;
                if (ClientStreams.TryGetValue(message.TicketId, out var clientStream))
                    await clientStream.WriteAsync(message);
            }
            else
            {
                ClientStreams[message.TicketId] = responseStream;
                if (AgentStreams.TryGetValue(message.TicketId, out var agentStream))
                    await agentStream.WriteAsync(message);
            }

            Directory.CreateDirectory(ChatLogsPath);

            var logLine = $"[{DateTime.UtcNow}] {message.Sender}: {message.Message}";
            var logFilePath = Path.Combine(ChatLogsPath, $"chat_{message.TicketId}.log");

            File.AppendAllText(logFilePath, logLine + Environment.NewLine);
        }
    }
}

using dm113_tech_support;
using Grpc.Core;
using System.Collections.Concurrent;

namespace DM113_TechSupport.Server.Services;

public class SupportService : Support.SupportBase
{
    // Dicionários para armazenar streams de clientes e atendentes
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
        string? currentTicketId = null;
        string? currentSender = null;
        bool isCurrentUserAnAgent = false;

        try
        {
            await foreach (var message in requestStream.ReadAllAsync())
            {
                if (currentTicketId == null)
                {
                    currentTicketId = message.TicketId;
                    currentSender = message.Sender;
                    isCurrentUserAnAgent = message.Sender.StartsWith(" >>> ");
                }

                var isAgent = message.Sender.StartsWith(" >>> ");

                // Verifica se é uma mensagem de saída ou entrada
                bool isExitMessage = message.Message.Contains("saiu do chat") ||
                                     message.Message.Contains("entrou no chat");

                if (isAgent)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan; // Cor para mensagens do atendente
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow; // Cor para mensagens do cliente
                }

                if (!isExitMessage)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Msg recebida de {message.Sender}: {message.Message}");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message.Message}");
                }
                Console.ResetColor();
               
                if (isAgent)
                {
                    AgentStreams.TryAdd(message.TicketId, responseStream);
                    if (ClientStreams.TryGetValue(message.TicketId, out var clientStream))
                    {
                        try
                        {
                            await clientStream.WriteAsync(message);
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($" ! Erro ao enviar mensagem para cliente: {ex.Message}");
                            Console.ResetColor();
                            ClientStreams.TryRemove(message.TicketId, out _);
                        }
                    }
                    else if (!isExitMessage)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(" ! Cliente ainda não conectado.");
                        Console.ResetColor();
                    }
                }
                else
                {
                    if (!isExitMessage)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray; // Cor para logs internos
                        Console.WriteLine("-> Encaminhando para atendente");
                        Console.ResetColor();
                    }

                    ClientStreams.TryAdd(message.TicketId, responseStream);

                    if (AgentStreams.TryGetValue(message.TicketId, out var agentStream))
                    {
                        try
                        {
                            await agentStream.WriteAsync(message);
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($" ! Erro ao enviar mensagem para atendente: {ex.Message}");
                            Console.ResetColor();

                            // Remove o stream inválido
                            AgentStreams.TryRemove(message.TicketId, out _);
                        }
                    }
                    else if (!isExitMessage)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(" ! Atendente ainda não conectado.");
                        Console.ResetColor();
                    }
                }

                // Salvar log em arquivo
                Directory.CreateDirectory(ChatLogsPath);
                var logLine = $"[{DateTime.UtcNow}] {message.Sender}: {message.Message}";
                var logFilePath = Path.Combine(ChatLogsPath, $"chat_{message.TicketId}.log");
                try
                {
                    await File.AppendAllTextAsync(logFilePath, logLine + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($" ! Erro ao salvar log: {ex.Message}");
                    Console.ResetColor();
                }
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            // Cliente ou atendente desconectou
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Usuário desconectou normalmente.");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Erro no chat: {ex.Message}");
            Console.ResetColor();
        }
        finally
        {
            if (!string.IsNullOrEmpty(currentTicketId))
            {
                if (isCurrentUserAnAgent)
                {
                    AgentStreams.TryRemove(currentTicketId, out _);
                    Console.WriteLine($" > Stream do atendente para o ticket {currentTicketId} removido.");
                }
                else
                {
                    ClientStreams.TryRemove(currentTicketId, out _);
                    Console.WriteLine($" > Stream do cliente para o ticket {currentTicketId} removido.");
                }
            }
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
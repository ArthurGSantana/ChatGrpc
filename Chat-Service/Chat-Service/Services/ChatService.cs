using System;
using System.Collections.Concurrent;
using Chat.Service.Protos;
using Grpc.Core;
using static Chat.Service.Protos.ChatService;

namespace Chat.Service.Services;

public class ChatService : ChatServiceBase
{
    private static readonly List<string> _userConnections = new();
    private static readonly ConcurrentDictionary<string, IServerStreamWriter<MessageResponse>> _connectedClients = new();

    public override Task<ChatResponse> JoinChat(ChatRequest request, ServerCallContext context)
    {
        var userId = request.UserId;

        if (_userConnections.Contains(userId))
        {
            return Task.FromResult(new ChatResponse
            {
                Success = false,
                Message = "User already connected."
            });
        }

        _userConnections.Add(userId);

        // Send welcome message to everyone
        // BroadcastMessage("System", $"{userId} has joined the chat").ConfigureAwait(false);

        return Task.FromResult(new ChatResponse
        {
            Success = true,
            Message = $"Welcome to the chat, {userId}!"
        });
    }

    public override async Task HandleMessage(
    IAsyncStreamReader<MessageRequest> requestStream,
    IServerStreamWriter<MessageResponse> responseStream,
    ServerCallContext context)
    {
        string userId = null;

        // Register this client's stream for receiving messages
        // We'll update the userId once we receive the first message

        // Handle disconnection
        context.CancellationToken.Register(() =>
        {
            if (userId != null)
            {
                _connectedClients.TryRemove(userId, out _);
                _userConnections.Remove(userId);

                // Announce departure
                BroadcastMessage("System", $"{userId} has left the chat").ConfigureAwait(false);
            }
        });

        // Process incoming messages
        try
        {
            // Read messages from this client and broadcast to all connected clients
            while (await requestStream.MoveNext(context.CancellationToken))
            {
                var message = requestStream.Current;

                // Use the userId from the message

                // First message, set up the userId and register the client
                userId = message.UserId;
                _connectedClients[userId] = responseStream;

                await BroadcastMessage(userId, message.Message);
            }
        }
        catch (Exception ex)
        {
            // Log exception appropriately
            Console.WriteLine($"Error handling message stream: {ex.Message}");
        }
    }
    private async Task BroadcastMessage(string userId, string message)
    {
        var messageResponse = new MessageResponse
        {
            UserId = userId,
            Message = message
        };

        // Create a list of sending tasks
        var sendingTasks = new List<Task>();
        
        var client = _connectedClients.FirstOrDefault(c => c.Key == userId);

        // Envio de mensagem para o usuario atual pois no BFF o signalR esta enviando para todos os usuarios
        try
        {
            sendingTasks.Add(client.Value.WriteAsync(messageResponse));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message to {client.Key}: {ex.Message}");
        }

        // Send message to all connected clients
        // foreach (var client in _connectedClients)
        // {

        // }

        // Wait for all messages to be sent
        await Task.WhenAll(sendingTasks);
    }
}
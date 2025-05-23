using System;
using System.Collections.Concurrent;
using Chat.Service.Protos;
using Chat_BFF.Grpc.Interfaces;
using Chat_BFF.Interfaces.Service;
using Chat_BFF.SignalR;
using Grpc.Core;
using Microsoft.AspNetCore.SignalR;

namespace Chat_BFF.Service;

public class ChatMessagingService : IChatMessagingService
{
    private readonly IChatClientService _chatClientService;
    private readonly ILogger<ChatMessagingService> _logger;
    private readonly IHubContext<ChatHub> _hubContext;

    private AsyncDuplexStreamingCall<MessageRequest, MessageResponse> _streamingCall;
    private CancellationTokenSource _cts;
    private Task _messageProcessingTask;

    // Dictionary para manter as solicitações de mensagens pendentes
    private readonly ConcurrentDictionary<string, Queue<MessageRequest>> _pendingMessages =
        new ConcurrentDictionary<string, Queue<MessageRequest>>();

    public ChatMessagingService(
        IChatClientService chatClientService,
        ILogger<ChatMessagingService> logger,
        IHubContext<ChatHub> hubContext)
    {
        _chatClientService = chatClientService;
        _logger = logger;
        _hubContext = hubContext;
    }

    // Método para iniciar o serviço de mensagens
    // Este método é chamado quando o serviço é iniciado
    // e deve ser chamado apenas uma vez durante o ciclo de vida do aplicativo
    public async Task JoinAsync(string userId, CancellationToken cancellationToken)
    {
        var request = new ChatRequest
        {
            UserId = userId
        };

        try
        {
            // Envia o pedido de entrada para o serviço gRPC
            var response = await _chatClientService.JoinChatAsync(request, cancellationToken);

            _logger.LogInformation("User {UserId} joined chat successfully", userId);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Error joining chat for user {UserId}", userId);
            throw;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Inicializa a conexão streaming
        _streamingCall = _chatClientService.HandleMessageStream(_cts.Token);

        // Inicia a tarefa que processa mensagens recebidas
        _messageProcessingTask = ProcessIncomingMessagesAsync(_cts.Token);

        // Inicia a tarefa que envia mensagens pendentes
        _ = ProcessOutgoingMessagesAsync(_cts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts != null)
        {
            _cts.Cancel();

            if (_streamingCall != null)
            {
                await _streamingCall.RequestStream.CompleteAsync().ConfigureAwait(false);
            }

            try
            {
                await _messageProcessingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Esperado quando cancelado
            }
        }
    }

    public void SendMessage(string userId, string messageText)
    {
        _logger.LogInformation("Received message from user {UserId}: {Message}", userId, messageText);

        QueueMessageForSending(userId, messageText);
    }

    // Método para enviar mensagem do usuário para o ChatService
    public void QueueMessageForSending(string userId, string messageText)
    {
        // Verificar se o streaming está inicializado
        if (_streamingCall == null)
        {
            _logger.LogWarning("Attempting to queue message without active streaming connection. Starting connection...");
            StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        var message = new MessageRequest
        {
            UserId = userId,
            Message = messageText
        };

        _pendingMessages.AddOrUpdate(
            userId,
            new Queue<MessageRequest>(new[] { message }),
            (_, queue) =>
            {
                queue.Enqueue(message);
                return queue;
            });

        _logger.LogInformation("Message from user {UserId} queued for sending", userId);
    }

    private async Task ProcessIncomingMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var response in _streamingCall.ResponseStream.ReadAllAsync(cancellationToken))
            {
                _logger.LogInformation("Received message from user {UserId}: {Message}",
                    response.UserId, response.Message);

                // Enviar a mensagem para os clientes signalR
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", response.UserId, response.Message, cancellationToken);

            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Error processing incoming messages");

            // Implementar lógica de reconexão aqui se necessário
        }
    }

    private async Task ProcessOutgoingMessagesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting message processing task for outgoing messages");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Processamento em lote das mensagens pendentes
                foreach (var userId in _pendingMessages.Keys)
                {
                    if (_pendingMessages.TryGetValue(userId, out var queue) && queue.Count > 0)
                    {
                        var message = queue.Dequeue();
                        _logger.LogInformation("Processing queued message for user {UserId}: {Message}",
                            userId, message.Message);

                        if (_streamingCall == null)
                        {
                            _logger.LogWarning("StreamingCall is null when trying to send message");
                            continue;
                        }

                        await _streamingCall.RequestStream.WriteAsync(message);
                        _logger.LogInformation("Message from user {UserId} sent to ChatService", userId);
                    }
                }

                // Aguarda um pouco antes de verificar novas mensagens
                await Task.Delay(50, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error sending message to ChatService");
                await Task.Delay(1000, cancellationToken); // Delay maior em caso de erro
            }
        }

        _logger.LogInformation("Outgoing message processing task completed");
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _streamingCall?.Dispose();
    }
}

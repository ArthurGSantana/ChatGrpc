using System;
using System.Collections.Concurrent;
using Chat.Service.Protos;
using Chat_BFF.Grpc.Interfaces;
using Chat_BFF.Interfaces.Service;
using Chat_BFF.Interfaces.WebSocketConfig;
using Grpc.Core;

namespace Chat_BFF.Service;

public class ChatMessagingService : IChatMessagingService
{
    private readonly IChatClientService _chatClientService;
    private readonly ILogger<ChatMessagingService> _logger;
    private readonly IWebSocketConnectionManager _webSocketManager;

    private AsyncDuplexStreamingCall<MessageRequest, MessageResponse> _streamingCall;
    private CancellationTokenSource _cts;
    private Task _messageProcessingTask;

    // Dictionary para manter as solicitações de mensagens pendentes
    private readonly ConcurrentDictionary<string, Queue<MessageRequest>> _pendingMessages =
        new ConcurrentDictionary<string, Queue<MessageRequest>>();

    public ChatMessagingService(
        IChatClientService chatClientService,
        IWebSocketConnectionManager webSocketManager,
        ILogger<ChatMessagingService> logger)
    {
        _chatClientService = chatClientService;
        _webSocketManager = webSocketManager;
        _logger = logger;
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

    // Método para enviar mensagem do usuário para o ChatService
    public void QueueMessageForSending(string userId, string messageText)
    {
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

                // Enviar a mensagem para os clientes WebSocket
                await _webSocketManager.SendMessageToAllAsync(new
                {
                    userId = response.UserId,
                    message = response.Message,
                    timestamp = DateTime.UtcNow
                }, cancellationToken);
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
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _streamingCall?.Dispose();
    }
}

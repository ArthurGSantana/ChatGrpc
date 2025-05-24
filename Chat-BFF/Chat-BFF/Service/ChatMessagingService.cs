using System.Collections.Concurrent;
using Chat.Service.Protos;
using Chat_BFF.Grpc.Interfaces;
using Chat_BFF.Interfaces.Service;
using Chat_BFF.SignalR;
using Grpc.Core;
using Microsoft.AspNetCore.SignalR;

namespace Chat_BFF.Service;

public class ChatMessagingService : IChatMessagingService, IDisposable
{
    private readonly IChatClientService _chatClientService;
    private readonly ILogger<ChatMessagingService> _logger;
    private readonly IHubContext<ChatHub> _hubContext;

    private AsyncDuplexStreamingCall<MessageRequest, MessageResponse>? _streamingCall;
    private CancellationTokenSource? _cts;
    private Task? _messageProcessingTask;

    private const string SystemClient = "System";

    // Dictionary para manter as solicitações de mensagens pendentes organizadas por usuário
    private readonly ConcurrentDictionary<string, Queue<MessageRequest>> _pendingMessages =
        new ConcurrentDictionary<string, Queue<MessageRequest>>();

    public ChatMessagingService(
        IChatClientService chatClientService,
        ILogger<ChatMessagingService> logger,
        IHubContext<ChatHub> hubContext)
    {
        _chatClientService = chatClientService ?? throw new ArgumentNullException(nameof(chatClientService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));

        _logger.LogInformation("Serviço de mensagens inicializado");
    }

    public async Task JoinAsync(string userId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Tentando incluir usuário {UserId} no chat", userId);

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Tentativa de entrada com userId nulo ou vazio");
            throw new ArgumentException("ID de usuário não pode estar vazio", nameof(userId));
        }

        var request = new ChatRequest
        {
            UserId = userId
        };

        try
        {
            _logger.LogDebug("Enviando requisição JoinChatAsync para userId: {UserId}", userId);
            // Envia o pedido de entrada para o serviço gRPC
            var response = await _chatClientService.JoinChatAsync(request, cancellationToken);

            _logger.LogInformation("Usuário {UserId} entrou no chat com sucesso. Resposta: {@Response}",
                userId, response);

            await _hubContext.Clients.All.SendAsync(
                    "ReceiveMessage",
                    SystemClient,
                    $"O usuário {userId} entrou no chat",
                    cancellationToken);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Erro gRPC ao entrar no chat para o usuário {UserId}. Status: {Status}, Detalhe: {Detail}",
                userId, ex.StatusCode, ex.Status.Detail);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao entrar no chat para o usuário {UserId}", userId);
            throw;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Iniciando serviço de mensagens");

        try
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Inicializa a conexão streaming
            _logger.LogDebug("Estabelecendo conexão streaming gRPC");
            _streamingCall = _chatClientService.HandleMessageStream(_cts.Token);
            _logger.LogInformation("Conexão streaming gRPC estabelecida com sucesso");

            // Inicia a tarefa que processa mensagens recebidas
            _logger.LogDebug("Iniciando tarefa de processamento de mensagens recebidas");
            _messageProcessingTask = ProcessIncomingMessagesAsync(_cts.Token);

            // Inicia a tarefa que envia mensagens pendentes
            _logger.LogDebug("Iniciando tarefa de processamento de mensagens enviadas");
            _ = ProcessOutgoingMessagesAsync(_cts.Token);

            _logger.LogInformation("Serviço de mensagens iniciado com sucesso");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar o serviço de mensagens");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Parando serviço de mensagens");

        if (_cts != null)
        {
            _logger.LogDebug("Cancelando operações em andamento");
            _cts.Cancel();

            if (_streamingCall != null)
            {
                _logger.LogDebug("Finalizando stream de requisições gRPC");
                try
                {
                    await _streamingCall.RequestStream.CompleteAsync().ConfigureAwait(false);
                    _logger.LogInformation("Stream de requisições gRPC finalizado com sucesso");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao finalizar stream de requisições gRPC");
                }
            }

            try
            {
                _logger.LogDebug("Aguardando conclusão da tarefa de processamento de mensagens");
                await _messageProcessingTask.ConfigureAwait(false);
                _logger.LogInformation("Tarefa de processamento de mensagens concluída com sucesso");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Tarefa de processamento de mensagens foi cancelada como esperado");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro durante a conclusão da tarefa de processamento de mensagens");
            }
        }

        _logger.LogInformation("Serviço de mensagens parado");
    }

    public void SendMessage(string userId, string messageText)
    {
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Tentativa de envio de mensagem com userId nulo ou vazio");
            return;
        }

        _logger.LogInformation("Processando mensagem do usuário {UserId}: {MessagePreview}",
            userId, messageText?.Length > 20 ? messageText.Substring(0, 20) + "..." : messageText);

        QueueMessageForSending(userId, messageText);
    }

    public void QueueMessageForSending(string userId, string messageText)
    {
        _logger.LogDebug("Enfileirando mensagem para o usuário {UserId}", userId);

        // Verificar se o streaming está inicializado
        if (_streamingCall == null)
        {
            _logger.LogWarning("Conexão streaming não inicializada. Iniciando conexão...");
            StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            _logger.LogInformation("Conexão streaming inicializada");
        }

        var message = new MessageRequest
        {
            UserId = userId,
            Message = messageText
        };

        _logger.LogDebug("MessageRequest criado: {UserId}, {MessagePreview}",
            message.UserId, message.Message?.Length > 20 ? message.Message.Substring(0, 20) + "..." : message.Message);

        _pendingMessages.AddOrUpdate(
            userId,
            new Queue<MessageRequest>(new[] { message }),
            (_, queue) =>
            {
                queue.Enqueue(message);
                return queue;
            });

        _logger.LogInformation("Mensagem do usuário {UserId} enfileirada para envio. Tamanho da fila: {QueueLength}",
            userId, _pendingMessages.TryGetValue(userId, out var q) ? q.Count : 0);
    }

    private async Task ProcessIncomingMessagesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Iniciando processamento de mensagens recebidas do serviço gRPC");
        int messageCount = 0;

        try
        {
            await foreach (var response in _streamingCall.ResponseStream.ReadAllAsync(cancellationToken))
            {
                messageCount++;
                _logger.LogInformation("Mensagem #{Count} recebida do usuário {UserId}: {MessagePreview}",
                    messageCount,
                    response.UserId,
                    response.Message?.Length > 20 ? response.Message.Substring(0, 20) + "..." : response.Message);

                try
                {
                    // Enviar a mensagem para os clientes signalR
                    _logger.LogDebug("Encaminhando mensagem para todos os clientes SignalR");
                    await _hubContext.Clients.All.SendAsync(
                        "ReceiveMessage",
                        response.UserId,
                        response.Message,
                        cancellationToken);

                    _logger.LogInformation("Mensagem de {UserId} entregue aos clientes SignalR", response.UserId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao enviar mensagem para os clientes SignalR");
                }
            }

            _logger.LogInformation("Stream de respostas gRPC concluído após {MessageCount} mensagens", messageCount);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Erro ao processar mensagens recebidas do serviço gRPC");

            // Implementação de lógica de reconexão poderia ser adicionada aqui
            _logger.LogWarning("Lógica de reconexão seria implementada aqui");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Processamento de mensagens recebidas foi cancelado");
        }
    }

    private async Task ProcessOutgoingMessagesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Iniciando processamento de mensagens a serem enviadas para o serviço gRPC");
        int totalMessagesSent = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                int batchMessageCount = 0;

                // Processamento em lote das mensagens pendentes
                foreach (var userId in _pendingMessages.Keys)
                {
                    if (_pendingMessages.TryGetValue(userId, out var queue) && queue.Count > 0)
                    {
                        var message = queue.Dequeue();
                        batchMessageCount++;
                        totalMessagesSent++;

                        _logger.LogDebug("Processando mensagem #{TotalCount} enfileirada do usuário {UserId}: {MessagePreview}",
                            totalMessagesSent, userId, message.Message?.Length > 20 ? message.Message.Substring(0, 20) + "..." : message.Message);

                        if (_streamingCall == null)
                        {
                            _logger.LogWarning("StreamingCall é nulo ao tentar enviar mensagem. Pulando.");
                            continue;
                        }

                        await _streamingCall.RequestStream.WriteAsync(message);
                        _logger.LogInformation("Mensagem #{TotalCount} do usuário {UserId} enviada para o serviço de Chat",
                            totalMessagesSent, userId);
                    }
                }

                if (batchMessageCount > 0)
                {
                    _logger.LogDebug("Processadas {BatchCount} mensagens neste lote. Total enviado: {TotalCount}",
                        batchMessageCount, totalMessagesSent);
                }

                // Aguarda um pouco antes de verificar novas mensagens
                await Task.Delay(50, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Erro ao enviar mensagem para o serviço de Chat");

                // Implementar lógica de reconexão poderia ser adicionada aqui
                _logger.LogWarning("Lógica de reconexão seria implementada aqui");

                await Task.Delay(1000, cancellationToken); // Delay maior em caso de erro
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Processamento de mensagens de saída foi cancelado");
                break;
            }
        }

        _logger.LogInformation("Tarefa de processamento de mensagens de saída concluída. Total de mensagens enviadas: {TotalCount}",
            totalMessagesSent);
    }

    public void Dispose()
    {
        _logger.LogInformation("Liberando recursos do serviço de mensagens");

        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _streamingCall?.Dispose();

            _logger.LogInformation("Recursos do serviço de mensagens liberados");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao liberar recursos do serviço de mensagens");
        }
    }
}
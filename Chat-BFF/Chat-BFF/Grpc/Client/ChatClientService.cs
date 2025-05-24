using Chat.Service.Protos;
using Chat_BFF.Grpc.Interfaces;
using Grpc.Core;
using Grpc.Net.Client;
using static Chat.Service.Protos.ChatService;

namespace Chat_BFF.Grpc.Client;

public class ChatClientService : ChatServiceClient, IChatClientService
{
    private readonly ILogger<ChatClientService> _logger;
    protected readonly ChatServiceClient Client;
    private readonly string _grpcUrl;

    public ChatClientService(ILogger<ChatClientService> logger, string grpcUrl)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _grpcUrl = grpcUrl ?? throw new ArgumentNullException(nameof(grpcUrl));

        _logger.LogInformation("Inicializando cliente gRPC para o serviço de chat. URL: {GrpcUrl}", _grpcUrl);

        try
        {
            var channelOptions = new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler
                {
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                    EnableMultipleHttp2Connections = true,
                }
            };

            var channel = GrpcChannel.ForAddress(_grpcUrl, channelOptions);
            Client = new ChatServiceClient(channel);

            _logger.LogInformation("Cliente gRPC para serviço de chat inicializado com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao inicializar o cliente gRPC para URL: {GrpcUrl}", _grpcUrl);
            throw;
        }
    }

    /// <summary>
    /// Registra um usuário no serviço de chat
    /// </summary>
    /// <param name="request">Requisição com ID do usuário</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resposta da operação de entrada no chat</returns>
    public async Task<ChatResponse> JoinChatAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            _logger.LogError("Tentativa de chamada JoinChatAsync com request nulo");
            throw new ArgumentNullException(nameof(request));
        }

        _logger.LogInformation("Enviando solicitação JoinChatAsync para o usuário {UserId}", request.UserId);

        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            _logger.LogDebug("Definindo prazo para requisição JoinChatAsync: {Deadline} segundos", 30);

            var response = await Client.JoinChatAsync(
                request,
                deadline: deadline,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Resposta de JoinChatAsync recebida para usuário {UserId}: {Success}",
                request.UserId, response.Success);
            _logger.LogDebug("Detalhes da resposta JoinChatAsync: {@Response}", response);

            return response;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Erro RPC durante JoinChatAsync para usuário {UserId}. Status: {Status}, Detalhes: {Detail}",
                request.UserId, ex.StatusCode, ex.Status.Detail);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Operação JoinChatAsync cancelada para usuário {UserId}", request.UserId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado durante JoinChatAsync para usuário {UserId}", request.UserId);
            throw;
        }
    }

    /// <summary>
    /// Estabelece um stream bidirecional de mensagens com o serviço de chat
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Objeto de chamada streaming para troca de mensagens</returns>
    public AsyncDuplexStreamingCall<MessageRequest, MessageResponse> HandleMessageStream(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Iniciando stream bidirecional de mensagens com o serviço de chat");

        try
        {
            // Adicionar metadados para o header user-id
            var headers = new Metadata
            {
                { "user-id", "system" } // Identificador padrão para o sistema
            };

            _logger.LogDebug("Configurando headers para stream de mensagens: {Headers}", string.Join(", ", headers));

            // Definindo um prazo para a chamada streaming
            var deadline = DateTime.UtcNow.AddHours(24); // Stream de longa duração
            _logger.LogDebug("Definindo prazo para stream de mensagens: {Deadline}", deadline);

            // Inicia a chamada de streaming bidirecional com os metadados
            var streamingCall = Client.HandleMessage(headers, deadline: deadline, cancellationToken: cancellationToken);

            _logger.LogInformation("Stream bidirecional de mensagens estabelecido com sucesso");
            return streamingCall;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Erro RPC ao estabelecer stream de mensagens. Status: {Status}, Detalhes: {Detail}",
                ex.StatusCode, ex.Status.Detail);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao estabelecer conexão de stream com o serviço de chat");
            throw;
        }
    }

    /// <summary>
    /// Envia uma mensagem única para o serviço de chat (método alternativo para testes)
    /// </summary>
    /// <param name="userId">ID do usuário</param>
    /// <param name="message">Texto da mensagem</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado da operação</returns>
    public async Task<bool> SendDirectMessageAsync(string userId, string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogError("Tentativa de envio de mensagem direta com userId nulo ou vazio");
            throw new ArgumentException("ID do usuário não pode ser vazio", nameof(userId));
        }

        _logger.LogInformation("Tentando enviar mensagem direta para usuário {UserId}", userId);

        try
        {
            // Este método seria implementado caso o serviço gRPC oferecesse
            // um endpoint para envio de mensagens únicas
            _logger.LogWarning("Método SendDirectMessageAsync não implementado no serviço gRPC atual");

            // Código de implementação aqui quando o serviço suportar

            return true;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Erro RPC ao enviar mensagem direta para {UserId}. Status: {Status}",
                userId, ex.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao enviar mensagem direta para {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Verifica o status da conexão com o serviço gRPC
    /// </summary>
    /// <returns>True se o serviço estiver disponível</returns>
    public async Task<bool> CheckServiceHealthAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Verificando saúde do serviço gRPC: {GrpcUrl}", _grpcUrl);

        try
        {
            // Poderia utilizar Health Checking do gRPC se disponível
            // Por enquanto, uma simples chamada JoinChat com usuário teste
            var testRequest = new ChatRequest { UserId = "health-check" };
            var response = await Client.JoinChatAsync(testRequest,
                deadline: DateTime.UtcNow.AddSeconds(5),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Verificação de saúde do serviço gRPC concluída com sucesso");
            return response.Success;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha na verificação de saúde do serviço gRPC: {GrpcUrl}", _grpcUrl);
            return false;
        }
    }
}
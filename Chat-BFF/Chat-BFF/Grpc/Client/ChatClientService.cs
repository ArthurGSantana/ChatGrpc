using System;
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

    public ChatClientService(ILogger<ChatClientService> logger, string grpcUrl)
    {
        _logger = logger;

        var channel = GrpcChannel.ForAddress(grpcUrl, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                EnableMultipleHttp2Connections = true,
            }
        });

        Client = new ChatServiceClient(channel);
    }

    public async Task<ChatResponse> JoinChatAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await Client.JoinChatAsync(request, cancellationToken: cancellationToken);

            _logger.LogInformation("JoinChat response: {Response}", response);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Joinchat, user {UserId}", request.UserId);

            throw;
        }
    }
    
    public AsyncDuplexStreamingCall<MessageRequest, MessageResponse> HandleMessageStream(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Initializing bidirectional message stream with ChatService");
            
            // Inicia a chamada de streaming bidirecional e retorna o objeto de chamada
            // que será gerenciado pelo serviço específico no BFF
            var streamingCall = Client.HandleMessage(cancellationToken: cancellationToken);
            
            return streamingCall;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error establishing stream connection with ChatService");
            throw;
        }
    }
}

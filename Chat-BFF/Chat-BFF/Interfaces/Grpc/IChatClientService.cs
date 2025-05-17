using System;
using Chat.Service.Protos;
using Grpc.Core;

namespace Chat_BFF.Grpc.Interfaces;

public interface IChatClientService
{
    Task<ChatResponse> JoinChatAsync(ChatRequest request, CancellationToken cancellationToken);
    AsyncDuplexStreamingCall<MessageRequest, MessageResponse> HandleMessageStream(CancellationToken cancellationToken);
}

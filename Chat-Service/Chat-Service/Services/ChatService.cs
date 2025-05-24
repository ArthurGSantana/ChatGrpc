using System;
using System.Collections.Concurrent;
using Chat.Service.Protos;
using Grpc.Core;
using static Chat.Service.Protos.ChatService;

namespace Chat.Service.Services;

public class ChatService(ILogger<ChatService> _logger) : ChatServiceBase
{
    private static readonly List<string> _userConnections = new();
    private static readonly ConcurrentDictionary<string, IServerStreamWriter<MessageResponse>> _connectedClients = new();

    public override Task<ChatResponse> JoinChat(ChatRequest request, ServerCallContext context)
    {
        var userId = request.UserId;
        _logger.LogInformation("Tentativa de conexão do usuário {UserId}", userId);

        if (_userConnections.Contains(userId))
        {
            _logger.LogWarning("Conexão recusada: Usuário {UserId} já está conectado", userId);
            return Task.FromResult(new ChatResponse
            {
                Success = false,
                Message = "User already connected."
            });
        }

        _userConnections.Add(userId);
        _logger.LogInformation("Usuário {UserId} entrou no chat com sucesso", userId);

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
        _logger.LogDebug("Iniciando nova conexão de stream bidirecional");

        // Registra o stream deste cliente para receber mensagens
        // Vamos atualizar o userId assim que recebermos a primeira mensagem

        // Trata a desconexão
        context.CancellationToken.Register(() =>
        {
            if (userId != null)
            {
                _logger.LogInformation("Desconectando usuário {UserId}", userId);
                _connectedClients.TryRemove(userId, out _);
                _userConnections.Remove(userId);

                // Anuncia a saída
                _logger.LogInformation("Anunciando saída do usuário {UserId}", userId);
                BroadcastMessage("System", $"{userId} has left the chat").ConfigureAwait(false);
            }
        });

        // Processa mensagens recebidas
        try
        {
            _logger.LogDebug("Iniciando recebimento de mensagens do cliente");
            // Lê mensagens deste cliente e transmite para todos os clientes conectados
            while (await requestStream.MoveNext(context.CancellationToken))
            {
                var message = requestStream.Current;

                // Usa o userId da mensagem
                userId = message.UserId;
                _connectedClients[userId] = responseStream;

                _logger.LogDebug("Mensagem recebida de {UserId}: {Message}", userId, message.Message);
                await BroadcastMessage(userId, message.Message);
            }
        }
        catch (Exception ex)
        {
            // Registra exceção adequadamente
            _logger.LogError(ex, "Erro ao processar o stream de mensagens para usuário {UserId}", userId ?? "desconhecido");
        }
        finally
        {
            _logger.LogInformation("Finalizando sessão de comunicação para usuário {UserId}", userId ?? "desconhecido");
        }
    }

    private async Task BroadcastMessage(string userId, string message)
    {
        _logger.LogDebug("Iniciando broadcast de mensagem do usuário {UserId}", userId);

        var messageResponse = new MessageResponse
        {
            UserId = userId,
            Message = message
        };

        // Cria uma lista de tarefas de envio
        var sendingTasks = new List<Task>();

        var client = _connectedClients.FirstOrDefault(c => c.Key == userId);

        if (client.Key != null)
        {
            // Envio de mensagem para o usuário atual pois no BFF o signalR está enviando para todos os usuários
            try
            {
                _logger.LogDebug("Enviando mensagem para o usuário {UserId}", client.Key);
                sendingTasks.Add(client.Value.WriteAsync(messageResponse));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar mensagem para o usuário {UserId}", client.Key);
            }
        }
        else
        {
            _logger.LogWarning("Tentativa de enviar mensagem para usuário não conectado: {UserId}", userId);
        }

        // Envia mensagem para todos os clientes conectados se necessário
        // foreach (var client in _connectedClients)
        // {
        //     _logger.LogDebug("Enviando mensagem para {UserId}", client.Key);
        //     sendingTasks.Add(...);
        // }

        // Aguarda todas as mensagens serem enviadas
        await Task.WhenAll(sendingTasks);
        _logger.LogDebug("Concluído o envio da mensagem do usuário {UserId}", userId);
    }
}
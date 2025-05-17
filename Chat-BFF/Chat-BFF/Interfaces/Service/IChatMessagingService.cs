using System;

namespace Chat_BFF.Interfaces.Service;

public interface IChatMessagingService
{
    /// <summary>
    /// Inicia o serviço de mensagens e estabelece a conexão de streaming com o serviço gRPC
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento para interromper as operações</param>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Para o serviço de mensagens e fecha a conexão de streaming
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento para interromper as operações</param>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Enfileira uma mensagem para ser enviada ao serviço gRPC
    /// </summary>
    /// <param name="userId">ID do usuário que está enviando a mensagem</param>
    /// <param name="messageText">Texto da mensagem a ser enviada</param>
    void QueueMessageForSending(string userId, string messageText);
}

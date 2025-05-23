namespace Chat_BFF.Interfaces.Service;

public interface IChatMessagingService
{
    /// <summary>
    /// Adiciona um usuário à sala de chat
    /// </summary>
    /// <param name="userId">ID do usuário que está se juntando ao chat</param>
    /// <param name="cancellationToken">Token de cancelamento para interromper as operações</param>
    Task JoinAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Envia uma mensagem para o serviço gRPC
    /// </summary>
    /// <param name="userId">ID do usuário que está enviando a mensagem</param>
    /// <param name="message">Texto da mensagem a ser enviada</param>
    void SendMessage(string userId, string message);

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

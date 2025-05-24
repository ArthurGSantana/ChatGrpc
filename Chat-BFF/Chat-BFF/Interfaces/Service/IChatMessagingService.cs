namespace Chat_BFF.Interfaces.Service;

public interface IChatMessagingService
{
    /// <summary>
    /// Registra um usuário no serviço de chat
    /// </summary>
    /// <param name="userId">ID do usuário</param>
    /// <param name="cancellationToken">Token para cancelamento da operação</param>
    /// <returns>Task representando a operação assíncrona</returns>
    /// <exception cref="RpcException">Lançada quando ocorre erro na comunicação gRPC</exception>
    Task JoinAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Processa uma mensagem de chat enviada por um usuário,
    /// enfileirando-a para envio ao serviço gRPC.
    /// </summary>
    /// <param name="userId">ID do usuário que enviou a mensagem</param>
    /// <param name="messageText">Texto da mensagem</param>
    void SendMessage(string userId, string message);

    /// <summary>
    /// Inicia o serviço de mensagens, estabelecendo a conexão streaming com o serviço gRPC
    /// e iniciando as tarefas de processamento de mensagens.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação</param>
    /// <returns>Task representando a operação assíncrona</returns>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Para o serviço de mensagens, cancelando todas as operações em andamento
    /// e liberando os recursos utilizados.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelamento da operação</param>
    /// <returns>Task representando a operação assíncrona</returns>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Enfileira uma mensagem para envio ao serviço gRPC.
    /// Inicializa a conexão streaming se necessário.
    /// </summary>
    /// <param name="userId">ID do usuário que enviou a mensagem</param>
    /// <param name="messageText">Texto da mensagem</param>
    void QueueMessageForSending(string userId, string messageText);
}

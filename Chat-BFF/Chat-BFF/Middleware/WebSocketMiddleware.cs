using System;
using System.Net.WebSockets;
using System.Text;
using Chat_BFF.Interfaces.Service;
using Chat_BFF.Interfaces.WebSocketConfig;

namespace Chat_BFF.Middleware;

public class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebSocketConnectionManager _manager;
    private readonly IChatMessagingService _chatService;
    private readonly ILogger<WebSocketMiddleware> _logger;

    public WebSocketMiddleware(
        RequestDelegate next,
        IWebSocketConnectionManager manager,
        IChatMessagingService chatService,
        ILogger<WebSocketMiddleware> logger)
    {
        _next = next;
        _manager = manager;
        _chatService = chatService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
            string socketId = _manager.AddSocket(webSocket);

            await HandleWebSocketConnection(context, webSocket, socketId);
        }
        else
        {
            await _next(context);
        }
    }

    private async Task HandleWebSocketConnection(HttpContext context, WebSocket webSocket, string socketId)
    {
        // Obter o userId do query string (poderiam vir de um token JWT também)
        string userId = context.Request.Query["userId"];

        if (string.IsNullOrEmpty(userId))
        {
            await _manager.SendMessageAsync(socketId, new { error = "userId is required" }, CancellationToken.None);
            await _manager.RemoveSocketAsync(socketId);
            return;
        }

        try
        {
            // Avisa o cliente que a conexão foi estabelecida
            await _manager.SendMessageAsync(socketId, new
            {
                type = "connection",
                userId,
                message = "Connected successfully",
                timestamp = DateTime.UtcNow
            }, CancellationToken.None);

            var buffer = new byte[4096];
            WebSocketReceiveResult result;

            // Loop para receber mensagens do WebSocket
            while (webSocket.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                do
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType != WebSocketMessageType.Close)
                        ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    using var reader = new StreamReader(ms, Encoding.UTF8);
                    string message = await reader.ReadToEndAsync();

                    _logger.LogInformation("Message received from WebSocket: {Message}", message);

                    try
                    {
                        // Processar a mensagem recebida
                        // Assumindo formato: {"message": "texto da mensagem"}
                        var messageObj = System.Text.Json.JsonSerializer.Deserialize<ChatMessage>(message);
                        if (!string.IsNullOrEmpty(messageObj?.Message))
                        {
                            // Enfileirar a mensagem para ser enviada ao serviço gRPC
                            _chatService.QueueMessageForSending(userId, messageObj.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing WebSocket message");
                        await _manager.SendMessageAsync(socketId, new { error = "Invalid message format" }, CancellationToken.None);
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("WebSocket connection closing");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebSocket connection");
        }
        finally
        {
            await _manager.RemoveSocketAsync(socketId);
        }
    }

    private class ChatMessage
    {
        public string Message { get; set; }
    }
}

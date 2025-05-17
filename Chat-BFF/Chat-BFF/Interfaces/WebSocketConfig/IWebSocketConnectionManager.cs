using System;
using System.Net.WebSockets;

namespace Chat_BFF.Interfaces.WebSocketConfig;

public interface IWebSocketConnectionManager
{
    string AddSocket(WebSocket socket);
    WebSocket GetSocketById(string id);
    Dictionary<string, WebSocket> GetAll();
    string GetId(WebSocket socket);
    Task RemoveSocketAsync(string id);
    Task SendMessageAsync(WebSocket socket, object message, CancellationToken cancellationToken);
    Task SendMessageAsync(string socketId, object message, CancellationToken cancellationToken);
    Task SendMessageToAllAsync(object message, CancellationToken cancellationToken);
}

using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Chat_BFF.Interfaces.WebSocketConfig;

namespace Chat_BFF.WebSocketConfig;

public class WebSocketConnectionManager : IWebSocketConnectionManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();
    private readonly ILogger<WebSocketConnectionManager> _logger;

    public WebSocketConnectionManager(ILogger<WebSocketConnectionManager> logger)
    {
        _logger = logger;
    }

    public string AddSocket(WebSocket socket)
    {
        string id = Guid.NewGuid().ToString();
        _sockets.TryAdd(id, socket);
        _logger.LogInformation("WebSocket connection added. Total connections: {Count}", _sockets.Count);
        return id;
    }

    public WebSocket? GetSocketById(string id)
    {
        return _sockets.TryGetValue(id, out WebSocket socket) ? socket : null;
    }

    public string GetId(WebSocket socket)
    {
        return _sockets.FirstOrDefault(p => p.Value == socket).Key;
    }

    public Dictionary<string, WebSocket> GetAll()
    {
        return _sockets.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    public async Task RemoveSocketAsync(string id)
    {
        if (_sockets.TryRemove(id, out WebSocket socket))
        {
            if (socket.State != WebSocketState.Closed && socket.State != WebSocketState.Aborted)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Connection closed by the server",
                    CancellationToken.None);
            }

            _logger.LogInformation("WebSocket connection removed. Total connections: {Count}", _sockets.Count);
        }
    }

    public async Task SendMessageAsync(WebSocket socket, object message, CancellationToken cancellationToken)
    {
        if (socket.State != WebSocketState.Open)
        {
            _logger.LogWarning("Attempted to send message to closed socket");
            return;
        }

        var serializedMessage = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(serializedMessage);
        await socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }

    public async Task SendMessageAsync(string socketId, object message, CancellationToken cancellationToken)
    {
        var socket = GetSocketById(socketId);
        if (socket != null)
        {
            await SendMessageAsync(socket, message, cancellationToken);
        }
        else
        {
            _logger.LogWarning("Socket with ID {SocketId} not found", socketId);
        }
    }

    public async Task SendMessageToAllAsync(object message, CancellationToken cancellationToken)
    {
        foreach (var pair in _sockets)
        {
            if (pair.Value.State == WebSocketState.Open)
            {
                await SendMessageAsync(pair.Value, message, cancellationToken);
            }
        }
    }
}

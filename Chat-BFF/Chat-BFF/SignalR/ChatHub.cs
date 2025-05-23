using System;
using Chat_BFF.Interfaces.Service;
using Microsoft.AspNetCore.SignalR;

namespace Chat_BFF.SignalR;

public class ChatHub : Hub
{
    private readonly IChatMessagingService _chatService;

    public ChatHub(IChatMessagingService chatService)
    {
        _chatService = chatService;
    }

    public void SendMessage(string userId, string message)
    {
        _chatService.SendMessage(userId, message);
    }
}

using Chat_BFF.Interfaces.Service;
using Chat_BFF.Records;
using Microsoft.AspNetCore.Mvc;

namespace Chat_BFF.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatMessagingService _chatMessagingService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatMessagingService chatMessagingService,
        ILogger<ChatController> logger)
    {
        _chatMessagingService = chatMessagingService;
        _logger = logger;
    }

    [HttpPost("join")]
    public async Task<IActionResult> Join(JoinChatRequest joinChatRequest)
    {
        try
        {
            await _chatMessagingService.JoinAsync(joinChatRequest.UserId, CancellationToken.None);
            return Ok(new { message = "User joined chat" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining chat for user {UserId}", joinChatRequest.UserId);
            return StatusCode(500, new { error = "Failed to join chat" });
        }
    }

    [HttpGet("stop")]
    public IActionResult Stop()
    {
        try
        {
            _chatMessagingService.StopAsync(CancellationToken.None);
            return Ok(new { message = "Chat service stopped" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping chat service");
            return StatusCode(500, new { error = "Failed to stop chat service" });
        }
    }
}

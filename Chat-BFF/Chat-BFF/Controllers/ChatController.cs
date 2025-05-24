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

    /// <summary>
    ///  Permite que um usuário se junte ao chat.
    /// /// Recebe o ID do usuário e inicia o processo de conexão ao serviço de chat.
    /// </summary>
    /// <param name="joinChatRequest"></param>
    /// <returns></returns>
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
            _logger.LogError(ex, "Erro no usuário {UserId} ao entrar no chat!", joinChatRequest.UserId);
            return StatusCode(500, new { error = "Failed to join chat" });
        }
    }
}

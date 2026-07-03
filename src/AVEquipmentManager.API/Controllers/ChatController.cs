using AVEquipmentManager.API.Services;
using AVEquipmentManager.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AVEquipmentManager.API.Controllers;

/// <summary>
/// POST /api/chat → Admin, Staff, Student (all authenticated roles)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]   // all authenticated roles can use the chatbot
public class ChatController : ControllerBase
{
    private readonly ChatbotService _chatbot;

    public ChatController(ChatbotService chatbot)
    {
        _chatbot = chatbot;
    }

    // POST /api/chat
    [HttpPost]
    public async Task<ActionResult<ChatMessageDto>> Chat([FromBody] ChatMessageDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Message))
            return BadRequest(new { message = "Message cannot be empty." });

        var response = await _chatbot.ProcessMessageAsync(dto.Message);

        return Ok(new ChatMessageDto
        {
            Message = dto.Message,
            Response = response,
            Timestamp = DateTime.UtcNow,
            IsUser = false
        });
    }
}

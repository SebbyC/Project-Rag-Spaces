using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RagWorkspace.Api.Interfaces;

namespace RagWorkspace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatService chatService,
        ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value ?? "anonymous";
            var response = await _chatService.ProcessChatAsync(userId, request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return StatusCode(500, new { error = "An error occurred processing your request" });
        }
    }

    [HttpPost("stream")]
    public async Task StreamChat([FromBody] ChatRequest request)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Add("Cache-Control", "no-cache");
        Response.Headers.Add("Connection", "keep-alive");

        var userId = User.FindFirst("sub")?.Value ?? "anonymous";
        
        try
        {
            await foreach (var chunk in _chatService.StreamChatAsync(userId, request))
            {
                await Response.WriteAsync($"data: {chunk}\n\n");
                await Response.Body.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming chat response");
            await Response.WriteAsync($"data: {{\\"error\\":\\"An error occurred while streaming response\\"}}\n\n");
            await Response.Body.FlushAsync();
        }
        finally
        {
            await Response.WriteAsync("data: [DONE]\n\n");
            await Response.Body.FlushAsync();
        }
    }
    
    [HttpGet("history/{sessionId}")]
    public async Task<IActionResult> GetChatHistory(string sessionId)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value ?? "anonymous";
            var messages = await _chatService.GetChatHistoryAsync(userId, sessionId);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chat history");
            return StatusCode(500, new { error = "An error occurred retrieving chat history" });
        }
    }
}
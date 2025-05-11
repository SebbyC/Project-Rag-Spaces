using RagWorkspace.Api.Models;

namespace RagWorkspace.Api.Interfaces;

public interface IChatService
{
    Task<ChatMessageResponse> ProcessChatAsync(string userId, ChatRequest request);
    IAsyncEnumerable<string> StreamChatAsync(string userId, ChatRequest request);
    Task<List<ChatMessageResponse>> GetChatHistoryAsync(string userId, string sessionId);
}

public class ChatRequest
{
    public string? SessionId { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Model { get; set; }
    public bool UseRag { get; set; } = true;
}

public class ChatMessageResponse
{
    public string Id { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? Model { get; set; }
    public string? Provider { get; set; }
}
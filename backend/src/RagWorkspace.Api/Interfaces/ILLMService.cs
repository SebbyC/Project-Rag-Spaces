namespace RagWorkspace.Api.Interfaces;

public interface ILLMService
{
    Task<LLMResponse> GenerateCompletionAsync(LLMRequest request);
    Task<float[]> GenerateEmbeddingAsync(string text);
    string GetProviderName();
}

public class LLMRequest
{
    public List<ChatMessage> Messages { get; set; } = new();
    public string? Model { get; set; }
    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 2000;
    public bool Stream { get; set; } = false;
}

public class LLMResponse
{
    public string Content { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
}

public class ChatMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}
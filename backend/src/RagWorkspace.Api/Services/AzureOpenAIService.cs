using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using RagWorkspace.Api.Configuration;
using RagWorkspace.Api.Interfaces;

namespace RagWorkspace.Api.Services;

public class AzureOpenAIService : ILLMService, IEmbeddingProvider
{
    private readonly OpenAIClient _client;
    private readonly AzureOpenAIOptions _options;
    private readonly ILogger<AzureOpenAIService> _logger;

    public AzureOpenAIService(
        IOptions<AzureOpenAIOptions> options,
        ILogger<AzureOpenAIService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new OpenAIClient(
            new Uri(_options.Endpoint),
            new AzureKeyCredential(_options.ApiKey));
    }

    public async Task<LLMResponse> GenerateCompletionAsync(LLMRequest request)
    {
        try
        {
            var chatMessages = request.Messages
                .Select(m => new ChatRequestMessage(
                    m.Role == "system" ? ChatRole.System :
                    m.Role == "assistant" ? ChatRole.Assistant :
                    ChatRole.User, m.Content))
                .ToList();

            var options = new ChatCompletionsOptions(
                request.Model ?? _options.ModelName,
                chatMessages)
            {
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens
            };

            var response = await _client.GetChatCompletionsAsync(options);
            var completion = response.Value;

            return new LLMResponse
            {
                Content = completion.Choices[0].Message.Content,
                TokensUsed = completion.Usage.TotalTokens,
                Model = request.Model ?? _options.ModelName,
                Provider = GetProviderName()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating completion with Azure OpenAI");
            throw;
        }
    }

    public async IAsyncEnumerable<string> GenerateCompletionStreamAsync(LLMRequest request)
    {
        try
        {
            var chatMessages = request.Messages
                .Select(m => new ChatRequestMessage(
                    m.Role == "system" ? ChatRole.System :
                    m.Role == "assistant" ? ChatRole.Assistant :
                    ChatRole.User, m.Content))
                .ToList();

            var options = new ChatCompletionsOptions(
                request.Model ?? _options.ModelName,
                chatMessages)
            {
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens
            };

            var streamingResponse = await _client.GetChatCompletionsStreamingAsync(options);
            
            await foreach (var update in streamingResponse)
            {
                if (update.ContentUpdate != null)
                {
                    yield return update.ContentUpdate;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming completion with Azure OpenAI");
            throw;
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        try
        {
            if (string.IsNullOrEmpty(text))
            {
                _logger.LogWarning("Empty text provided for embedding generation");
                return Array.Empty<float>();
            }

            var options = new EmbeddingsOptions(_options.EmbeddingModel, new[] { text });
            var response = await _client.GetEmbeddingsAsync(options);
            return response.Value.Data[0].Embedding.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding with Azure OpenAI for text: {TextSnippet}",
                text.Length > 100 ? text.Substring(0, 100) + "..." : text);
            throw;
        }
    }

    public int GetEmbeddingVectorSize()
    {
        // Return the vector size based on the configured embedding model
        return _options.EmbeddingModel.ToLowerInvariant() switch
        {
            // Azure OpenAI embedding models and their dimensions
            var model when model.Contains("text-embedding-3-large") => 3072,
            var model when model.Contains("text-embedding-3-small") => 1536,
            var model when model.Contains("text-embedding-ada-002") => 1536,
            _ => 1536 // Default to 1536 dimensions for unknown models
        };
    }

    public string GetProviderName() => "azure-openai";
}
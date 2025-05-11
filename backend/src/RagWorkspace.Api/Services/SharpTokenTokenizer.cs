using Microsoft.Extensions.Options;
using RagWorkspace.Api.Configuration;
using RagWorkspace.Api.Interfaces;

namespace RagWorkspace.Api.Services;

/// <summary>
/// Implementation of ITokenizerService using SharpToken for OpenAI-compatible tokenization
/// </summary>
public class SharpTokenTokenizer : ITokenizerService
{
    private readonly ILogger<SharpTokenTokenizer> _logger;
    private readonly string _modelName;
    
    // For implementations using SharpToken, we would have:
    // private readonly GptEncoding _encoding;

    public SharpTokenTokenizer(
        IOptions<AzureOpenAIOptions> options,
        ILogger<SharpTokenTokenizer> logger)
    {
        _logger = logger;
        _modelName = options.Value.EmbeddingModel;
        
        // In an actual implementation with SharpToken, we would initialize the encoder:
        // _encoding = GptEncoding.GetEncodingForModel(GetEncodingName(_modelName));
    }

    public async Task<int> EstimateTokenCountAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }
        
        // This would be a non-blocking task with SharpToken
        return await Task.FromResult(EstimateTokenCount(text));
    }

    public int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }
        
        try
        {
            // With SharpToken we would do:
            // return _encoding.Encode(text).Count;
            
            // Simple estimation for demonstration purposes:
            // Approximates 4 characters per token which is a rough average for English text
            return Math.Max(1, (int)(text.Length / 4.0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error estimating token count for text. Using fallback estimation.");
            // Fallback to simple estimation if encoding fails
            return Math.Max(1, (int)(text.Length / 4.0));
        }
    }

    public string GetModelName() => _modelName;
    
    // Helper to map model names to encoding names for SharpToken
    private static string GetEncodingName(string modelName)
    {
        return modelName.ToLowerInvariant() switch
        {
            var name when name.Contains("gpt-4") => "cl100k_base",
            var name when name.Contains("gpt-3.5") => "cl100k_base",
            var name when name.Contains("text-embedding-3") => "cl100k_base",
            var name when name.Contains("text-embedding-ada-002") => "cl100k_base",
            _ => "cl100k_base" // Default to cl100k_base as fallback
        };
    }
}
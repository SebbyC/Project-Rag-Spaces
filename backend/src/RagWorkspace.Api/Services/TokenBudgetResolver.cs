using Microsoft.Extensions.Options;
using RagWorkspace.Api.Configuration;
using RagWorkspace.Api.Interfaces;

namespace RagWorkspace.Api.Services;

public class TokenBudgetResolver : ITokenBudgetResolver
{
    private readonly ILogger<TokenBudgetResolver> _logger;
    
    // Default token limits for different models
    private readonly Dictionary<string, int> _modelContextLimits = new(StringComparer.OrdinalIgnoreCase)
    {
        // Azure OpenAI / OpenAI models
        { "gpt-4o", 128000 },
        { "gpt-4-turbo", 128000 },
        { "gpt-4", 8192 },
        { "gpt-3.5-turbo", 16385 },
        { "gpt-3.5-turbo-16k", 16385 },
        
        // Google models
        { "gemini-1.5-pro", 1000000 },
        { "gemini-1.0-pro", 32768 },
        
        // Default fallback
        { "default", 8000 }
    };
    
    // Percentage of the context window to use for RAG content
    private const double RAG_CONTEXT_PERCENTAGE = 0.6; // 60% of total context
    
    // Conversation summary triggers at this percentage of the model's context limit
    private const double SUMMARY_TRIGGER_PERCENTAGE = 0.8; // 80% of total context
    
    // Rough token estimation ratio (chars per token) - very approximate
    private const double CHARS_PER_TOKEN = 4.0;

    public TokenBudgetResolver(ILogger<TokenBudgetResolver> logger)
    {
        _logger = logger;
    }

    public int GetContextBudgetForRAG(string modelName)
    {
        int contextLimit = GetModelContextLimit(modelName);
        int ragBudget = (int)(contextLimit * RAG_CONTEXT_PERCENTAGE);
        
        _logger.LogDebug("Token budget for RAG with model {ModelName}: {Budget} tokens (from {ContextLimit})",
            modelName, ragBudget, contextLimit);
            
        return ragBudget;
    }

    public int GetConversationSummaryCutoff(string modelName)
    {
        int contextLimit = GetModelContextLimit(modelName);
        int summaryCutoff = (int)(contextLimit * SUMMARY_TRIGGER_PERCENTAGE);
        
        _logger.LogDebug("Conversation summary cutoff for model {ModelName}: {Cutoff} tokens (from {ContextLimit})",
            modelName, summaryCutoff, contextLimit);
            
        return summaryCutoff;
    }

    public int EstimateTokenCount(string text, string? modelName = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }
        
        // Very simple estimation based on character count
        // In practice, a proper tokenizer like SharpToken would be used here
        int estimatedTokens = (int)(text.Length / CHARS_PER_TOKEN);
        
        return Math.Max(1, estimatedTokens); // At least 1 token for non-empty text
    }

    private int GetModelContextLimit(string modelName)
    {
        if (string.IsNullOrEmpty(modelName))
        {
            _logger.LogWarning("No model name provided for context limit lookup. Using default.");
            return _modelContextLimits["default"];
        }
        
        // Try to find an exact match
        if (_modelContextLimits.TryGetValue(modelName, out int limit))
        {
            return limit;
        }
        
        // Try to find a partial match
        foreach (var kvp in _modelContextLimits)
        {
            if (modelName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Using context limit of {Limit} for model {ModelName} based on partial match with {KeyModel}",
                    kvp.Value, modelName, kvp.Key);
                return kvp.Value;
            }
        }
        
        // Fall back to default
        _logger.LogWarning("No context limit found for model {ModelName}. Using default limit of {DefaultLimit}.",
            modelName, _modelContextLimits["default"]);
        return _modelContextLimits["default"];
    }
}
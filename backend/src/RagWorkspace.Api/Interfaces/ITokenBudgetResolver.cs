namespace RagWorkspace.Api.Interfaces;

/// <summary>
/// Service for resolving token budgets for various LLM operations
/// </summary>
public interface ITokenBudgetResolver
{
    /// <summary>
    /// Gets the maximum context budget for RAG operations with the specified model
    /// </summary>
    /// <param name="modelName">The LLM model name</param>
    /// <returns>The maximum number of tokens to use for RAG context</returns>
    int GetContextBudgetForRAG(string modelName);
    
    /// <summary>
    /// Gets the token cutoff point for summarizing a conversation
    /// </summary>
    /// <param name="modelName">The LLM model name</param>
    /// <returns>The token threshold at which to summarize a conversation</returns>
    int GetConversationSummaryCutoff(string modelName);
    
    /// <summary>
    /// Estimates the number of tokens in a text
    /// </summary>
    /// <param name="text">The text to estimate</param>
    /// <param name="modelName">Optional model name for model-specific estimation</param>
    /// <returns>Estimated token count</returns>
    int EstimateTokenCount(string text, string? modelName = null);
}
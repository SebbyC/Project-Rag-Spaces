namespace RagWorkspace.Api.Interfaces;

/// <summary>
/// Service for estimating token counts in text
/// </summary>
public interface ITokenizerService
{
    /// <summary>
    /// Estimates the number of tokens in the provided text
    /// </summary>
    /// <param name="text">The text to estimate token count for</param>
    /// <returns>The estimated token count</returns>
    Task<int> EstimateTokenCountAsync(string text);
    
    /// <summary>
    /// Estimates the number of tokens in the provided text synchronously
    /// </summary>
    /// <param name="text">The text to estimate token count for</param>
    /// <returns>The estimated token count</returns>
    int EstimateTokenCount(string text);
    
    /// <summary>
    /// Gets the name of the tokenizer model being used
    /// </summary>
    /// <returns>The model name</returns>
    string GetModelName();
}
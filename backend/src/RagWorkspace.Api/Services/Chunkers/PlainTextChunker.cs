using System.Text;
using Microsoft.Extensions.Options;
using RagWorkspace.Api.Configuration;
using RagWorkspace.Api.Interfaces;
using RagWorkspace.Api.Models;

namespace RagWorkspace.Api.Services.Chunkers;

/// <summary>
/// Chunks plain text content using recursive separator-based splitting
/// </summary>
public class PlainTextChunker
{
    private readonly ITokenizerService _tokenizer;
    private readonly ChunkingOptions _options;
    private readonly ILogger<PlainTextChunker> _logger;

    public PlainTextChunker(
        ITokenizerService tokenizer,
        IOptions<ChunkingOptions> options,
        ILogger<PlainTextChunker> logger)
    {
        _tokenizer = tokenizer;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Chunks text content into appropriately sized pieces for embedding
    /// </summary>
    /// <param name="textContent">The text content to chunk</param>
    /// <param name="filePath">The original file path</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="userId">The user ID</param>
    /// <param name="customSeparators">Optional custom separators to use instead of defaults</param>
    /// <param name="customMaxTokens">Optional custom max tokens per chunk</param>
    /// <param name="customOverlapTokens">Optional custom overlap tokens between chunks</param>
    /// <returns>A list of chunk results</returns>
    public async Task<List<ChunkResult>> ChunkAsync(
        string textContent,
        string filePath,
        string projectId,
        string userId,
        List<string>? customSeparators = null,
        int? customMaxTokens = null,
        int? customOverlapTokens = null)
    {
        if (string.IsNullOrEmpty(textContent))
        {
            _logger.LogWarning("Empty text content provided for chunking: {FilePath}", filePath);
            return new List<ChunkResult>();
        }

        // Use default separators in order of preference if not provided
        List<string> separators = customSeparators ?? 
            new List<string> { "\n\n", "\n", ". ", " ", "" };
        
        int maxTokens = customMaxTokens ?? _options.MaxTextChunkTokens;
        int overlap = customOverlapTokens ?? _options.TextOverlapTokens;
        
        var allChunks = new List<ChunkResult>();
        int globalChunkIndex = 0;
        string fileExtension = Path.GetExtension(filePath);
        
        // Create metadata dictionary with common fields
        var baseMetadata = new Dictionary<string, string>
        {
            ["userId"] = userId,
            ["projectId"] = projectId,
            ["filePath"] = filePath,
            ["fileName"] = Path.GetFileName(filePath),
            ["fileType"] = fileExtension
        };

        await RecursiveSplitAsync(
            textContent, 
            separators, 
            maxTokens, 
            overlap,
            filePath, 
            projectId,
            userId,
            allChunks, 
            ref globalChunkIndex, 
            baseMetadata);
        
        if (allChunks.Count == 0)
        {
            _logger.LogWarning("No chunks were generated for text content: {FilePath}", filePath);
        }
        else
        {
            _logger.LogInformation("Generated {ChunkCount} chunks for text content: {FilePath}", 
                allChunks.Count, filePath);
        }
        
        return allChunks;
    }

    /// <summary>
    /// Recursively splits text into chunks using separators
    /// </summary>
    private async Task RecursiveSplitAsync(
        string text, 
        List<string> separators, 
        int maxTokens, 
        int overlap,
        string filePath, 
        string projectId,
        string userId,
        List<ChunkResult> finalizedChunks, 
        ref int chunkIndex, 
        Dictionary<string, string> initialMetadata)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }
        
        int tokenCount = await _tokenizer.EstimateTokenCountAsync(text);

        // If the text fits within the token limit, add it as a chunk
        if (tokenCount <= maxTokens)
        {
            var metadata = new Dictionary<string, string>(initialMetadata);
            metadata["chunkIndex"] = chunkIndex.ToString();
            
            finalizedChunks.Add(new ChunkResult(
                text.Trim(),
                metadata,
                filePath,
                chunkIndex++,
                tokenCount));
            
            return;
        }

        // If no more separators to try but text is still too large
        if (!separators.Any())
        {
            // Force split by character count as a last resort
            await ForceSplitByCharCountAsync(
                text, 
                maxTokens, 
                overlap, 
                filePath, 
                projectId,
                userId,
                finalizedChunks, 
                ref chunkIndex, 
                initialMetadata);
            
            return;
        }

        // Get the current separator and the remaining separators
        string currentSeparator = separators[0];
        List<string> remainingSeparators = separators.Skip(1).ToList();
        
        // Empty separator means to force character splitting (last resort)
        if (string.IsNullOrEmpty(currentSeparator))
        {
            await ForceSplitByCharCountAsync(
                text, 
                maxTokens, 
                overlap, 
                filePath, 
                projectId,
                userId,
                finalizedChunks, 
                ref chunkIndex, 
                initialMetadata);
            
            return;
        }

        // Split text by the current separator
        string[] splits = text.Split(new[] { currentSeparator }, StringSplitOptions.None);
        
        // If the split didn't actually split anything (e.g., separator not found)
        if (splits.Length <= 1)
        {
            await RecursiveSplitAsync(
                text, 
                remainingSeparators, 
                maxTokens, 
                overlap,
                filePath, 
                projectId,
                userId,
                finalizedChunks, 
                ref chunkIndex, 
                initialMetadata);
            
            return;
        }

        // Process splits with buffers
        StringBuilder currentChunkBuffer = new StringBuilder();
        
        foreach (string part in splits)
        {
            // Skip empty parts
            if (string.IsNullOrEmpty(part))
            {
                continue;
            }

            // Try adding the current part to the buffer
            string potentialNextChunk = currentChunkBuffer.Length == 0 
                ? part 
                : currentChunkBuffer + currentSeparator + part;
                
            int potentialTokens = await _tokenizer.EstimateTokenCountAsync(potentialNextChunk);

            // If adding this part would exceed the token limit and we already have content
            if (potentialTokens > maxTokens && currentChunkBuffer.Length > 0)
            {
                // Process the current buffer
                await RecursiveSplitAsync(
                    currentChunkBuffer.ToString(), 
                    remainingSeparators, 
                    maxTokens, 
                    overlap,
                    filePath, 
                    projectId,
                    userId,
                    finalizedChunks, 
                    ref chunkIndex, 
                    initialMetadata);

                // Start a new buffer with the current part
                currentChunkBuffer.Clear();
                currentChunkBuffer.Append(part);
            }
            else
            {
                // Add to the current buffer
                if (currentChunkBuffer.Length > 0)
                {
                    currentChunkBuffer.Append(currentSeparator);
                }
                currentChunkBuffer.Append(part);
            }
        }

        // Process any remaining content in the buffer
        if (currentChunkBuffer.Length > 0)
        {
            await RecursiveSplitAsync(
                currentChunkBuffer.ToString(), 
                remainingSeparators, 
                maxTokens, 
                overlap,
                filePath, 
                projectId,
                userId,
                finalizedChunks, 
                ref chunkIndex, 
                initialMetadata);
        }
    }

    /// <summary>
    /// Force splits text by character count when no other separator works
    /// </summary>
    private async Task ForceSplitByCharCountAsync(
        string text, 
        int maxTokens, 
        int overlap,
        string filePath, 
        string projectId,
        string userId,
        List<ChunkResult> finalizedChunks, 
        ref int chunkIndex, 
        Dictionary<string, string> initialMetadata)
    {
        // Rough estimate of chars per token for determining split sizes
        const double charsPerToken = 4.0;
        
        // Approximately how many characters for the target token size
        int targetCharLength = (int)(maxTokens * charsPerToken);
        int overlapCharLength = (int)(overlap * charsPerToken);
        
        int position = 0;
        
        while (position < text.Length)
        {
            // Calculate how much text to take in this chunk
            int charsToTake = Math.Min(targetCharLength, text.Length - position);
            
            // Make sure we don't cut in the middle of a word if possible
            if (position + charsToTake < text.Length)
            {
                // Try to find a space or newline to break at
                int breakPos = text.LastIndexOfAny(new[] { ' ', '\n', '\r', '.', ',', ';', ':', '!', '?' }, 
                    position + charsToTake - 1, 
                    Math.Min(50, charsToTake)); // Look back up to 50 chars
                
                if (breakPos > position)
                {
                    charsToTake = breakPos - position + 1;
                }
            }
            
            // Extract the chunk
            string chunkContent = text.Substring(position, charsToTake);
            int tokenCount = await _tokenizer.EstimateTokenCountAsync(chunkContent);
            
            // Create metadata for this chunk
            var metadata = new Dictionary<string, string>(initialMetadata);
            metadata["chunkIndex"] = chunkIndex.ToString();
            metadata["isForceChunked"] = "true";
            
            // Add the chunk
            finalizedChunks.Add(new ChunkResult(
                chunkContent.Trim(),
                metadata,
                filePath,
                chunkIndex++,
                tokenCount));
            
            // Move position forward, accounting for overlap
            position += charsToTake - overlapCharLength;
            
            // Ensure we're making progress
            if (charsToTake <= overlapCharLength)
            {
                position += overlapCharLength + 1;
            }
        }
    }
}
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RagWorkspace.Api.Configuration;
using RagWorkspace.Api.Interfaces;
using RagWorkspace.Api.Models;

namespace RagWorkspace.Api.Services.Chunkers;

/// <summary>
/// Chunks JSON and YAML configuration files
/// </summary>
public class JsonYamlChunker
{
    private readonly ITokenizerService _tokenizer;
    private readonly ChunkingOptions _options;
    private readonly ILogger<JsonYamlChunker> _logger;
    private readonly PlainTextChunker _plainTextChunker;

    public JsonYamlChunker(
        ITokenizerService tokenizer,
        IOptions<ChunkingOptions> options,
        PlainTextChunker plainTextChunker,
        ILogger<JsonYamlChunker> logger)
    {
        _tokenizer = tokenizer;
        _options = options.Value;
        _plainTextChunker = plainTextChunker;
        _logger = logger;
    }

    /// <summary>
    /// Chunks JSON content into appropriately sized pieces for embedding
    /// </summary>
    /// <param name="jsonContent">The JSON content to chunk</param>
    /// <param name="filePath">The original file path</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>A list of chunk results</returns>
    public async Task<List<ChunkResult>> ChunkJsonAsync(
        string jsonContent,
        string filePath,
        string projectId,
        string userId)
    {
        if (string.IsNullOrEmpty(jsonContent))
        {
            _logger.LogWarning("Empty JSON content provided for chunking: {FilePath}", filePath);
            return new List<ChunkResult>();
        }

        var allChunks = new List<ChunkResult>();
        int globalChunkIndex = 0;
        string fileExtension = Path.GetExtension(filePath);

        // If small enough, keep as a single chunk
        int totalTokenCount = await _tokenizer.EstimateTokenCountAsync(jsonContent);
        if (totalTokenCount <= _options.MaxConfigChunkTokens)
        {
            var metadata = new Dictionary<string, string>
            {
                ["userId"] = userId,
                ["projectId"] = projectId,
                ["filePath"] = filePath,
                ["fileName"] = Path.GetFileName(filePath),
                ["fileType"] = fileExtension,
                ["format"] = "json",
                ["chunkIndex"] = "0"
            };

            allChunks.Add(new ChunkResult(
                jsonContent.Trim(),
                metadata,
                filePath,
                0,
                totalTokenCount));

            return allChunks;
        }

        try
        {
            // Parse the JSON document
            using JsonDocument jsonDoc = JsonDocument.Parse(jsonContent);
            
            // Process the root element
            await ProcessJsonElementAsync(
                jsonDoc.RootElement,
                "$", // JSON path for root
                filePath,
                projectId,
                userId,
                allChunks,
                ref globalChunkIndex);
                
            _logger.LogInformation("Generated {ChunkCount} chunks for JSON content: {FilePath}",
                allChunks.Count, filePath);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing JSON content for chunking: {FilePath}", filePath);
            
            // Fall back to plain text chunking
            _logger.LogInformation("Falling back to plain text chunking for JSON: {FilePath}", filePath);
            return await _plainTextChunker.ChunkAsync(
                jsonContent,
                filePath,
                projectId,
                userId,
                null,
                _options.MaxConfigChunkTokens,
                _options.TextOverlapTokens);
        }

        return allChunks;
    }

    /// <summary>
    /// Chunks YAML content into appropriately sized pieces for embedding
    /// </summary>
    /// <param name="yamlContent">The YAML content to chunk</param>
    /// <param name="filePath">The original file path</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>A list of chunk results</returns>
    public async Task<List<ChunkResult>> ChunkYamlAsync(
        string yamlContent,
        string filePath,
        string projectId,
        string userId)
    {
        if (string.IsNullOrEmpty(yamlContent))
        {
            _logger.LogWarning("Empty YAML content provided for chunking: {FilePath}", filePath);
            return new List<ChunkResult>();
        }

        var allChunks = new List<ChunkResult>();
        string fileExtension = Path.GetExtension(filePath);

        // If small enough, keep as a single chunk
        int totalTokenCount = await _tokenizer.EstimateTokenCountAsync(yamlContent);
        if (totalTokenCount <= _options.MaxConfigChunkTokens)
        {
            var metadata = new Dictionary<string, string>
            {
                ["userId"] = userId,
                ["projectId"] = projectId,
                ["filePath"] = filePath,
                ["fileName"] = Path.GetFileName(filePath),
                ["fileType"] = fileExtension,
                ["format"] = "yaml",
                ["chunkIndex"] = "0"
            };

            allChunks.Add(new ChunkResult(
                yamlContent.Trim(),
                metadata,
                filePath,
                0,
                totalTokenCount));

            return allChunks;
        }

        // For YAML, we'll use indentation-based chunking
        // YAML is often structured with top-level keys that have nested content
        // We can split by these top-level keys
        
        // Split the content into lines
        string[] lines = yamlContent.Split('\n');
        
        StringBuilder currentChunk = new StringBuilder();
        int currentTokens = 0;
        int chunkIndex = 0;
        int topLevelIndent = -1;
        
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            
            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                if (currentChunk.Length > 0)
                {
                    currentChunk.AppendLine(line);
                }
                continue;
            }
            
            // Calculate indentation level
            int indent = line.Length - line.TrimStart().Length;
            
            // Determine if this is a top-level key
            bool isTopLevel = false;
            
            // If we haven't seen a top-level indent yet, assume this is one
            if (topLevelIndent == -1 && line.TrimStart().Contains(':'))
            {
                topLevelIndent = indent;
                isTopLevel = true;
            }
            else if (indent <= topLevelIndent && line.TrimStart().Contains(':'))
            {
                isTopLevel = true;
            }
            
            // If this is a top-level key and we already have content, finalize the current chunk
            if (isTopLevel && currentChunk.Length > 0 && currentTokens > 0)
            {
                var metadata = new Dictionary<string, string>
                {
                    ["userId"] = userId,
                    ["projectId"] = projectId,
                    ["filePath"] = filePath,
                    ["fileName"] = Path.GetFileName(filePath),
                    ["fileType"] = fileExtension,
                    ["format"] = "yaml",
                    ["chunkIndex"] = chunkIndex.ToString()
                };
                
                allChunks.Add(new ChunkResult(
                    currentChunk.ToString().Trim(),
                    metadata,
                    filePath,
                    chunkIndex++,
                    currentTokens));
                    
                currentChunk.Clear();
                currentTokens = 0;
            }
            
            // Add the current line to the chunk
            currentChunk.AppendLine(line);
            currentTokens += await _tokenizer.EstimateTokenCountAsync(line);
            
            // If this chunk is getting too large, finalize it
            if (currentTokens > _options.MaxConfigChunkTokens)
            {
                var metadata = new Dictionary<string, string>
                {
                    ["userId"] = userId,
                    ["projectId"] = projectId,
                    ["filePath"] = filePath,
                    ["fileName"] = Path.GetFileName(filePath),
                    ["fileType"] = fileExtension,
                    ["format"] = "yaml",
                    ["chunkIndex"] = chunkIndex.ToString(),
                    ["isLargeSection"] = "true"
                };
                
                allChunks.Add(new ChunkResult(
                    currentChunk.ToString().Trim(),
                    metadata,
                    filePath,
                    chunkIndex++,
                    currentTokens));
                    
                currentChunk.Clear();
                currentTokens = 0;
            }
        }
        
        // Add any remaining content as the final chunk
        if (currentChunk.Length > 0)
        {
            var metadata = new Dictionary<string, string>
            {
                ["userId"] = userId,
                ["projectId"] = projectId,
                ["filePath"] = filePath,
                ["fileName"] = Path.GetFileName(filePath),
                ["fileType"] = fileExtension,
                ["format"] = "yaml",
                ["chunkIndex"] = chunkIndex.ToString()
            };
            
            allChunks.Add(new ChunkResult(
                currentChunk.ToString().Trim(),
                metadata,
                filePath,
                chunkIndex,
                currentTokens));
        }
        
        _logger.LogInformation("Generated {ChunkCount} chunks for YAML content: {FilePath}",
            allChunks.Count, filePath);
            
        return allChunks;
    }

    /// <summary>
    /// Recursively processes a JSON element and adds chunks to the result list
    /// </summary>
    private async Task ProcessJsonElementAsync(
        JsonElement element,
        string jsonPath,
        string filePath,
        string projectId,
        string userId,
        List<ChunkResult> finalizedChunks,
        ref int chunkIndex)
    {
        // Convert the element to a string
        string elementJson = element.ToString();
        int tokenCount = await _tokenizer.EstimateTokenCountAsync(elementJson);
        
        // Create common metadata
        var baseMetadata = new Dictionary<string, string>
        {
            ["userId"] = userId,
            ["projectId"] = projectId,
            ["filePath"] = filePath,
            ["fileName"] = Path.GetFileName(filePath),
            ["fileType"] = Path.GetExtension(filePath),
            ["format"] = "json",
            ["jsonPath"] = jsonPath,
            ["valueKind"] = element.ValueKind.ToString()
        };
        
        // If element is small enough or is a primitive, add it as is
        bool isPrimitive = element.ValueKind != JsonValueKind.Object && 
                             element.ValueKind != JsonValueKind.Array;
                             
        if (tokenCount <= _options.MaxConfigChunkTokens || isPrimitive || ShouldTakeElementAsIs(element))
        {
            baseMetadata["chunkIndex"] = chunkIndex.ToString();
            
            finalizedChunks.Add(new ChunkResult(
                elementJson,
                baseMetadata,
                filePath,
                chunkIndex++,
                tokenCount));
                
            return;
        }
        
        // Handle objects by processing each property
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                await ProcessJsonElementAsync(
                    property.Value,
                    $"{jsonPath}.{property.Name}",
                    filePath,
                    projectId,
                    userId,
                    finalizedChunks,
                    ref chunkIndex);
            }
        }
        // Handle arrays by processing each item
        else if (element.ValueKind == JsonValueKind.Array)
        {
            // For small arrays, keep them together
            if (tokenCount <= _options.MaxConfigChunkTokens * 2)
            {
                baseMetadata["chunkIndex"] = chunkIndex.ToString();
                
                finalizedChunks.Add(new ChunkResult(
                    elementJson,
                    baseMetadata,
                    filePath,
                    chunkIndex++,
                    tokenCount));
                    
                return;
            }
            
            // For larger arrays, process each item individually
            int arrayIndex = 0;
            foreach (JsonElement arrayItem in element.EnumerateArray())
            {
                await ProcessJsonElementAsync(
                    arrayItem,
                    $"{jsonPath}[{arrayIndex}]",
                    filePath,
                    projectId,
                    userId,
                    finalizedChunks,
                    ref chunkIndex);
                    
                arrayIndex++;
            }
        }
    }
    
    /// <summary>
    /// Determines if a JSON element should be taken as is instead of being further split
    /// </summary>
    private bool ShouldTakeElementAsIs(JsonElement element)
    {
        // Keep objects with few properties together
        if (element.ValueKind == JsonValueKind.Object)
        {
            int propertyCount = 0;
            foreach (var _ in element.EnumerateObject())
            {
                propertyCount++;
                
                // If more than 10 properties, don't take as is
                if (propertyCount > 10)
                {
                    return false;
                }
            }
            
            // Small objects (≤ 10 properties) are kept together
            return true;
        }
        
        // Keep small arrays together
        if (element.ValueKind == JsonValueKind.Array)
        {
            int itemCount = 0;
            foreach (var _ in element.EnumerateArray())
            {
                itemCount++;
                
                // If more than 20 items, don't take as is
                if (itemCount > 20)
                {
                    return false;
                }
            }
            
            // Small arrays (≤ 20 items) are kept together
            return true;
        }
        
        // For any other type, no special handling
        return false;
    }
}
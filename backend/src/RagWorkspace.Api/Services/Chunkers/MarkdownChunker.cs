using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using RagWorkspace.Api.Configuration;
using RagWorkspace.Api.Interfaces;
using RagWorkspace.Api.Models;

namespace RagWorkspace.Api.Services.Chunkers;

/// <summary>
/// Chunks markdown content with heading detection and context preservation
/// </summary>
public class MarkdownChunker
{
    private readonly ITokenizerService _tokenizer;
    private readonly ChunkingOptions _options;
    private readonly ILogger<MarkdownChunker> _logger;
    private readonly PlainTextChunker _plainTextChunker;
    
    // Regex patterns for markdown headings
    private static readonly Regex _headingPattern = new Regex(@"^(#{1,6})\s+(.*?)(?:\s+#{1,6})?$", RegexOptions.Multiline);
    private static readonly Regex _sectionStartPattern = new Regex(@"(?=^#{1,6}\s+.*?$)", RegexOptions.Multiline);

    public MarkdownChunker(
        ITokenizerService tokenizer,
        IOptions<ChunkingOptions> options,
        PlainTextChunker plainTextChunker,
        ILogger<MarkdownChunker> logger)
    {
        _tokenizer = tokenizer;
        _options = options.Value;
        _plainTextChunker = plainTextChunker;
        _logger = logger;
    }

    /// <summary>
    /// Chunks markdown content into appropriately sized pieces for embedding
    /// </summary>
    /// <param name="markdownContent">The markdown content to chunk</param>
    /// <param name="filePath">The original file path</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>A list of chunk results</returns>
    public async Task<List<ChunkResult>> ChunkAsync(
        string markdownContent,
        string filePath,
        string projectId,
        string userId)
    {
        if (string.IsNullOrEmpty(markdownContent))
        {
            _logger.LogWarning("Empty markdown content provided for chunking: {FilePath}", filePath);
            return new List<ChunkResult>();
        }

        var allChunks = new List<ChunkResult>();
        int globalChunkIndex = 0;
        string fileExtension = Path.GetExtension(filePath);

        // If small enough, keep as a single chunk
        int totalTokenCount = await _tokenizer.EstimateTokenCountAsync(markdownContent);
        if (totalTokenCount <= _options.MaxMarkdownChunkTokens)
        {
            var metadata = new Dictionary<string, string>
            {
                ["userId"] = userId,
                ["projectId"] = projectId,
                ["filePath"] = filePath,
                ["fileName"] = Path.GetFileName(filePath),
                ["fileType"] = fileExtension,
                ["chunkIndex"] = "0"
            };

            allChunks.Add(new ChunkResult(
                markdownContent.Trim(),
                metadata,
                filePath,
                0,
                totalTokenCount));

            return allChunks;
        }

        // Split by headings using regex
        string[] sections = Regex.Split(markdownContent, @"(?=^#{1,6}\s+.*?$)", RegexOptions.Multiline)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();

        // If no sections were found, fall back to plain text chunking
        if (sections.Length <= 1)
        {
            _logger.LogInformation("No section headings found in markdown, falling back to plain text chunking: {FilePath}", filePath);
            return await _plainTextChunker.ChunkAsync(
                markdownContent,
                filePath,
                projectId,
                userId,
                null,
                _options.MaxMarkdownChunkTokens,
                _options.MarkdownOverlapTokens);
        }

        // Track heading context
        string currentH1 = string.Empty;
        string currentH2 = string.Empty;
        string currentH3 = string.Empty;

        // Process each section
        foreach (string section in sections)
        {
            if (string.IsNullOrWhiteSpace(section))
            {
                continue;
            }

            // Extract the heading from the section
            var headingMatch = _headingPattern.Match(section);
            if (!headingMatch.Success)
            {
                // This section doesn't start with a heading (unusual, but possible)
                // Use current heading context
                await ProcessMarkdownSectionAsync(
                    section,
                    currentH1,
                    currentH2,
                    currentH3,
                    filePath,
                    projectId,
                    userId,
                    allChunks,
                    ref globalChunkIndex);
                continue;
            }

            string headingLevel = headingMatch.Groups[1].Value; // The ### part
            string headingText = headingMatch.Groups[2].Value.Trim(); // The heading text
            
            // Update heading context based on level
            switch (headingLevel.Length)
            {
                case 1: // # H1
                    currentH1 = headingText;
                    currentH2 = string.Empty;
                    currentH3 = string.Empty;
                    break;
                case 2: // ## H2
                    currentH2 = headingText;
                    currentH3 = string.Empty;
                    break;
                case 3: // ### H3
                    currentH3 = headingText;
                    break;
                // For H4-H6, we don't track as separate context, but include in content
            }

            // Process this section with the updated heading context
            await ProcessMarkdownSectionAsync(
                section,
                currentH1,
                currentH2,
                currentH3,
                filePath,
                projectId,
                userId,
                allChunks,
                ref globalChunkIndex);
        }

        _logger.LogInformation("Generated {ChunkCount} chunks for markdown content: {FilePath}",
            allChunks.Count, filePath);

        return allChunks;
    }

    /// <summary>
    /// Processes a markdown section and adds chunks to the result list
    /// </summary>
    private async Task ProcessMarkdownSectionAsync(
        string section,
        string h1Context,
        string h2Context,
        string h3Context,
        string filePath,
        string projectId,
        string userId,
        List<ChunkResult> finalizedChunks,
        ref int chunkIndex)
    {
        // Estimate token count for this section
        int tokenCount = await _tokenizer.EstimateTokenCountAsync(section);

        // Create base metadata for this section
        var metadata = new Dictionary<string, string>
        {
            ["userId"] = userId,
            ["projectId"] = projectId,
            ["filePath"] = filePath,
            ["fileName"] = Path.GetFileName(filePath),
            ["fileType"] = Path.GetExtension(filePath),
            ["h1_context"] = h1Context,
            ["h2_context"] = h2Context,
            ["h3_context"] = h3Context
        };

        // If section fits within token limit, use it as is
        if (tokenCount <= _options.MaxMarkdownChunkTokens)
        {
            // Add heading context to the content if available
            string contentWithContext = AddHeadingContext(section, h1Context, h2Context, h3Context);
            
            metadata["chunkIndex"] = chunkIndex.ToString();
            
            finalizedChunks.Add(new ChunkResult(
                contentWithContext,
                metadata,
                filePath,
                chunkIndex++,
                await _tokenizer.EstimateTokenCountAsync(contentWithContext)));
            
            return;
        }

        // Section is too large, split by paragraphs
        await SplitSectionByParagraphsAsync(
            section,
            h1Context,
            h2Context,
            h3Context,
            metadata,
            filePath,
            projectId,
            userId,
            finalizedChunks,
            ref chunkIndex);
    }

    /// <summary>
    /// Splits a large markdown section into smaller chunks by paragraphs
    /// </summary>
    private async Task SplitSectionByParagraphsAsync(
        string section,
        string h1Context,
        string h2Context,
        string h3Context,
        Dictionary<string, string> baseMetadata,
        string filePath,
        string projectId,
        string userId,
        List<ChunkResult> finalizedChunks,
        ref int chunkIndex)
    {
        // First, try to extract the heading line if present
        string headingLine = string.Empty;
        string contentWithoutHeading = section;
        
        var headingMatch = _headingPattern.Match(section);
        if (headingMatch.Success)
        {
            headingLine = headingMatch.Value;
            contentWithoutHeading = section.Substring(headingMatch.Length).TrimStart();
        }
        
        // Split content by paragraphs (blank lines)
        string[] paragraphs = Regex.Split(contentWithoutHeading, @"\n\s*\n")
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();
            
        // If very little content left after extracting heading, just use as is
        if (paragraphs.Length == 0)
        {
            var metadata = new Dictionary<string, string>(baseMetadata);
            metadata["chunkIndex"] = chunkIndex.ToString();
            
            finalizedChunks.Add(new ChunkResult(
                section.Trim(),
                metadata,
                filePath,
                chunkIndex++,
                await _tokenizer.EstimateTokenCountAsync(section)));
            
            return;
        }

        // Use plain text chunker for paragraphs, but with heading context
        StringBuilder currentChunk = new StringBuilder();
        if (!string.IsNullOrEmpty(headingLine))
        {
            currentChunk.AppendLine(headingLine);
        }
        
        int currentTokens = await _tokenizer.EstimateTokenCountAsync(currentChunk.ToString());
        
        foreach (string paragraph in paragraphs)
        {
            int paragraphTokens = await _tokenizer.EstimateTokenCountAsync(paragraph);
            
            // If adding this paragraph would exceed the limit, finalize the current chunk
            if (currentTokens + paragraphTokens > _options.MaxMarkdownChunkTokens && currentChunk.Length > 0)
            {
                string contentWithContext = AddHeadingContext(currentChunk.ToString(), h1Context, h2Context, h3Context);
                
                var metadata = new Dictionary<string, string>(baseMetadata);
                metadata["chunkIndex"] = chunkIndex.ToString();
                metadata["isSubChunk"] = "true";
                
                finalizedChunks.Add(new ChunkResult(
                    contentWithContext,
                    metadata,
                    filePath,
                    chunkIndex++,
                    await _tokenizer.EstimateTokenCountAsync(contentWithContext)));
                
                // Start a new chunk, keeping the header
                currentChunk.Clear();
                if (!string.IsNullOrEmpty(headingLine))
                {
                    currentChunk.AppendLine(headingLine);
                }
                currentTokens = await _tokenizer.EstimateTokenCountAsync(currentChunk.ToString());
            }
            
            // Add paragraph to current chunk
            if (currentChunk.Length > 0 && currentChunk[currentChunk.Length - 1] != '\n')
            {
                currentChunk.AppendLine();
                currentChunk.AppendLine();
            }
            currentChunk.Append(paragraph);
            currentTokens = await _tokenizer.EstimateTokenCountAsync(currentChunk.ToString());
        }
        
        // Add any remaining content as the final chunk
        if (currentChunk.Length > 0)
        {
            string contentWithContext = AddHeadingContext(currentChunk.ToString(), h1Context, h2Context, h3Context);
            
            var metadata = new Dictionary<string, string>(baseMetadata);
            metadata["chunkIndex"] = chunkIndex.ToString();
            metadata["isSubChunk"] = "true";
            
            finalizedChunks.Add(new ChunkResult(
                contentWithContext,
                metadata,
                filePath,
                chunkIndex++,
                await _tokenizer.EstimateTokenCountAsync(contentWithContext)));
        }
    }

    /// <summary>
    /// Adds heading context to the content
    /// </summary>
    private static string AddHeadingContext(string content, string h1, string h2, string h3)
    {
        StringBuilder contextBuilder = new StringBuilder();
        
        if (!string.IsNullOrEmpty(h1))
        {
            contextBuilder.Append("# ").Append(h1);
            
            if (!string.IsNullOrEmpty(h2))
            {
                contextBuilder.Append(" > ## ").Append(h2);
                
                if (!string.IsNullOrEmpty(h3))
                {
                    contextBuilder.Append(" > ### ").Append(h3);
                }
            }
            
            // Only add the context section if we have any context
            if (contextBuilder.Length > 0)
            {
                contextBuilder.AppendLine();
                contextBuilder.AppendLine();
            }
        }
        
        contextBuilder.Append(content);
        return contextBuilder.ToString();
    }
}
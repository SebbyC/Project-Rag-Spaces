using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using RagWorkspace.Api.Configuration;
using RagWorkspace.Api.Interfaces;
using RagWorkspace.Api.Models;

namespace RagWorkspace.Api.Services.Chunkers;

/// <summary>
/// Chunks code files with structure-aware chunking
/// </summary>
public class CodeChunker
{
    private readonly ITokenizerService _tokenizer;
    private readonly ChunkingOptions _options;
    private readonly ILogger<CodeChunker> _logger;
    private readonly PlainTextChunker _plainTextChunker;

    // Language-specific regex patterns for code structures
    private static readonly Dictionary<string, Regex> _classPatterns = new()
    {
        [".cs"] = new Regex(@"(?:^|\n)(?:public|private|protected|internal|static|sealed|abstract)?\s*(?:partial\s+)?(?:class|struct|interface|enum|record)\s+(\w+)(?:<.+?>)?(?:\s*:(?:\s*\w+(?:<.+?>)?(?:,\s*\w+(?:<.+?>)?)*)?)?(?:\r?\n)?\s*{", RegexOptions.Compiled),
        [".java"] = new Regex(@"(?:^|\n)(?:public|private|protected)?\s*(?:static|final|abstract)?\s*(?:class|interface|enum)\s+(\w+)(?:<.+?>)?(?:\s+extends\s+\w+(?:<.+?>)?)?(?:\s+implements\s+\w+(?:<.+?>)?(?:,\s*\w+(?:<.+?>)?)*)?(?:\r?\n)?\s*{", RegexOptions.Compiled),
        [".py"] = new Regex(@"(?:^|\n)class\s+(\w+)(?:\([\w,\s.<>]*\))?(?:\r?\n)?\s*:", RegexOptions.Compiled),
        [".js"] = new Regex(@"(?:^|\n)(?:export\s+)?(?:default\s+)?(?:abstract\s+)?class\s+(\w+)(?:\s+extends\s+\w+)?(?:\s+implements\s+\w+(?:,\s*\w+)*)?(?:\r?\n)?\s*{", RegexOptions.Compiled),
        [".ts"] = new Regex(@"(?:^|\n)(?:export\s+)?(?:default\s+)?(?:abstract\s+)?class\s+(\w+)(?:<.+?>)?(?:\s+extends\s+\w+(?:<.+?>)?)?(?:\s+implements\s+\w+(?:<.+?>)?(?:,\s*\w+(?:<.+?>)?)*)?(?:\r?\n)?\s*{", RegexOptions.Compiled),
    };

    private static readonly Dictionary<string, Regex> _functionPatterns = new()
    {
        [".cs"] = new Regex(@"(?:^|\n)(?:public|private|protected|internal|static|virtual|override|abstract)?\s*(?:async\s+)?(?:\w+(?:<.+>)?(?:\s*\[\])?\s+)(\w+)\s*\(.*?\)(?:\s*=>\s*[^;\n{]+;|\s*(?:where\s+.+?)?(?:\r?\n)?\s*{)", RegexOptions.Compiled | RegexOptions.Singleline),
        [".java"] = new Regex(@"(?:^|\n)(?:public|private|protected)?\s*(?:static|final|abstract|native|synchronized)?\s*(?:\w+(?:<.+>)?(?:\[\])?\s+)(\w+)\s*\(.*?\)(?:\s*throws\s+\w+(?:,\s*\w+)*)?(?:\r?\n)?\s*{", RegexOptions.Compiled | RegexOptions.Singleline),
        [".py"] = new Regex(@"(?:^|\n)(?:async\s+)?def\s+(\w+)\s*\(.*?\)(?:\s*->.*?)?(?:\r?\n)?\s*:", RegexOptions.Compiled | RegexOptions.Singleline),
        [".js"] = new Regex(@"(?:^|\n)(?:export\s+)?(?:default\s+)?(?:async\s+)?(?:function\s+(\w+)|(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s+)?\([^)]*\)\s*=>|(\w+)\s*[:=]\s*(?:async\s+)?(?:function\s*)?\([^)]*\)\s*(?:=>)?)(?:\r?\n)?\s*{", RegexOptions.Compiled | RegexOptions.Singleline),
        [".ts"] = new Regex(@"(?:^|\n)(?:export\s+)?(?:default\s+)?(?:public|private|protected|static|abstract)?\s*(?:async\s+)?(?:function\s+(\w+)|(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s+)?\([^)]*\)\s*=>|(\w+)(?:\s*<.+?>)?\s*[:=]\s*(?:async\s+)?(?:function\s*)?\([^)]*\)(?:\s*:\s*\w+(?:<.+?>)?)?(?:\s*=>)?)(?:\r?\n)?\s*{", RegexOptions.Compiled | RegexOptions.Singleline),
    };

    public CodeChunker(
        ITokenizerService tokenizer,
        IOptions<ChunkingOptions> options,
        PlainTextChunker plainTextChunker,
        ILogger<CodeChunker> logger)
    {
        _tokenizer = tokenizer;
        _options = options.Value;
        _plainTextChunker = plainTextChunker;
        _logger = logger;
    }

    /// <summary>
    /// Chunks code content into appropriately sized pieces for embedding
    /// </summary>
    /// <param name="codeContent">The code content to chunk</param>
    /// <param name="language">The programming language</param>
    /// <param name="filePath">The original file path</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="userId">The user ID</param>
    /// <returns>A list of chunk results</returns>
    public async Task<List<ChunkResult>> ChunkAsync(
        string codeContent,
        string language,
        string filePath,
        string projectId,
        string userId)
    {
        if (string.IsNullOrEmpty(codeContent))
        {
            _logger.LogWarning("Empty code content provided for chunking: {FilePath}", filePath);
            return new List<ChunkResult>();
        }

        var allChunks = new List<ChunkResult>();
        int globalChunkIndex = 0;
        string fileExtension = Path.GetExtension(filePath);

        // If small enough, keep as a single chunk
        int totalTokenCount = await _tokenizer.EstimateTokenCountAsync(codeContent);
        if (totalTokenCount <= _options.MaxCodeChunkTokens)
        {
            var metadata = new Dictionary<string, string>
            {
                ["userId"] = userId,
                ["projectId"] = projectId,
                ["filePath"] = filePath,
                ["fileName"] = Path.GetFileName(filePath),
                ["fileType"] = fileExtension,
                ["language"] = language,
                ["chunkIndex"] = "0"
            };

            allChunks.Add(new ChunkResult(
                codeContent.Trim(),
                metadata,
                filePath,
                0,
                totalTokenCount));

            return allChunks;
        }

        // Try to find semantic blocks (classes, functions, methods)
        var codeBlocks = await FindCodeBlocksAsync(codeContent, fileExtension);
        
        // If no semantic blocks were found or not enough content was covered,
        // fall back to line-based chunking
        if (codeBlocks.Count == 0)
        {
            _logger.LogInformation("No semantic code blocks found, falling back to line-based chunking: {FilePath}", filePath);
            return await ChunkByLinesAsync(
                codeContent,
                language,
                filePath,
                projectId,
                userId);
        }

        // Sort blocks by their starting position
        codeBlocks = codeBlocks.OrderBy(b => b.startPosition).ToList();

        // Handle code before the first block
        if (codeBlocks.First().startPosition > 0)
        {
            string preamble = codeContent.Substring(0, codeBlocks.First().startPosition);
            if (!string.IsNullOrWhiteSpace(preamble))
            {
                int preambleTokens = await _tokenizer.EstimateTokenCountAsync(preamble);
                var preambleMetadata = new Dictionary<string, string>
                {
                    ["userId"] = userId,
                    ["projectId"] = projectId,
                    ["filePath"] = filePath,
                    ["fileName"] = Path.GetFileName(filePath),
                    ["fileType"] = fileExtension,
                    ["language"] = language,
                    ["blockType"] = "preamble",
                    ["chunkIndex"] = globalChunkIndex.ToString()
                };

                allChunks.Add(new ChunkResult(
                    preamble.Trim(),
                    preambleMetadata,
                    filePath,
                    globalChunkIndex++,
                    preambleTokens));
            }
        }

        // Process each code block
        for (int i = 0; i < codeBlocks.Count; i++)
        {
            var block = codeBlocks[i];
            int blockTokens = await _tokenizer.EstimateTokenCountAsync(block.content);

            var blockMetadata = new Dictionary<string, string>
            {
                ["userId"] = userId,
                ["projectId"] = projectId,
                ["filePath"] = filePath,
                ["fileName"] = Path.GetFileName(filePath),
                ["fileType"] = fileExtension,
                ["language"] = language,
                ["blockType"] = block.type,
                ["blockName"] = block.name
            };

            // If block fits within token limit, add as is
            if (blockTokens <= _options.MaxCodeChunkTokens)
            {
                blockMetadata["chunkIndex"] = globalChunkIndex.ToString();
                
                allChunks.Add(new ChunkResult(
                    block.content.Trim(),
                    blockMetadata,
                    filePath,
                    globalChunkIndex++,
                    blockTokens));
            }
            else
            {
                // Block is too large, sub-split it
                await SubSplitCodeBlockAsync(
                    block,
                    language,
                    blockMetadata,
                    filePath,
                    projectId,
                    userId,
                    allChunks,
                    ref globalChunkIndex);
            }

            // Handle code between this block and the next one (if any)
            if (i < codeBlocks.Count - 1)
            {
                int currentBlockEnd = block.startPosition + block.content.Length;
                int nextBlockStart = codeBlocks[i + 1].startPosition;
                
                if (nextBlockStart > currentBlockEnd)
                {
                    string interstitial = codeContent.Substring(currentBlockEnd, nextBlockStart - currentBlockEnd);
                    if (!string.IsNullOrWhiteSpace(interstitial))
                    {
                        int interstitialTokens = await _tokenizer.EstimateTokenCountAsync(interstitial);
                        var interstitialMetadata = new Dictionary<string, string>
                        {
                            ["userId"] = userId,
                            ["projectId"] = projectId,
                            ["filePath"] = filePath,
                            ["fileName"] = Path.GetFileName(filePath),
                            ["fileType"] = fileExtension,
                            ["language"] = language,
                            ["blockType"] = "interstitial",
                            ["chunkIndex"] = globalChunkIndex.ToString()
                        };

                        allChunks.Add(new ChunkResult(
                            interstitial.Trim(),
                            interstitialMetadata,
                            filePath,
                            globalChunkIndex++,
                            interstitialTokens));
                    }
                }
            }
        }

        // Handle code after the last block
        int lastBlockEnd = codeBlocks.Last().startPosition + codeBlocks.Last().content.Length;
        if (lastBlockEnd < codeContent.Length)
        {
            string postamble = codeContent.Substring(lastBlockEnd);
            if (!string.IsNullOrWhiteSpace(postamble))
            {
                int postambleTokens = await _tokenizer.EstimateTokenCountAsync(postamble);
                var postambleMetadata = new Dictionary<string, string>
                {
                    ["userId"] = userId,
                    ["projectId"] = projectId,
                    ["filePath"] = filePath,
                    ["fileName"] = Path.GetFileName(filePath),
                    ["fileType"] = fileExtension,
                    ["language"] = language,
                    ["blockType"] = "postamble",
                    ["chunkIndex"] = globalChunkIndex.ToString()
                };

                allChunks.Add(new ChunkResult(
                    postamble.Trim(),
                    postambleMetadata,
                    filePath,
                    globalChunkIndex++,
                    postambleTokens));
            }
        }

        _logger.LogInformation("Generated {ChunkCount} chunks for code content: {FilePath}",
            allChunks.Count, filePath);

        return allChunks;
    }

    /// <summary>
    /// Finds code blocks (classes, functions) in the content
    /// </summary>
    private async Task<List<(string type, string name, string content, int startPosition)>> FindCodeBlocksAsync(
        string codeContent, 
        string fileExtension)
    {
        var blocks = new List<(string type, string name, string content, int startPosition)>();
        
        // Find classes
        if (_classPatterns.TryGetValue(fileExtension, out var classPattern))
        {
            foreach (Match match in classPattern.Matches(codeContent))
            {
                string className = match.Groups.Count > 1 ? match.Groups[1].Value : "UnnamedClass";
                
                string classContent = await ExtractBalancedBlockAsync(
                    codeContent, 
                    match.Index, 
                    fileExtension);
                    
                if (!string.IsNullOrEmpty(classContent))
                {
                    blocks.Add(("class", className, classContent, match.Index));
                }
            }
        }
        
        // Find functions/methods
        if (_functionPatterns.TryGetValue(fileExtension, out var functionPattern))
        {
            foreach (Match match in functionPattern.Matches(codeContent))
            {
                // Handle different function pattern captures
                string functionName = match.Groups.Count > 1 
                    ? match.Groups[1].Success 
                        ? match.Groups[1].Value 
                        : match.Groups[2].Success 
                            ? match.Groups[2].Value 
                            : match.Groups[3].Success 
                                ? match.Groups[3].Value 
                                : "UnnamedFunction"
                    : "UnnamedFunction";
                
                string functionContent = await ExtractBalancedBlockAsync(
                    codeContent, 
                    match.Index, 
                    fileExtension);
                    
                if (!string.IsNullOrEmpty(functionContent))
                {
                    blocks.Add(("function", functionName, functionContent, match.Index));
                }
            }
        }
        
        return blocks;
    }

    /// <summary>
    /// Extracts a balanced code block starting from the given position
    /// </summary>
    private async Task<string> ExtractBalancedBlockAsync(
        string codeContent, 
        int startIndex, 
        string fileExtension)
    {
        if (fileExtension == ".py")
        {
            // Python uses indentation-based blocks
            return ExtractPythonBlock(codeContent, startIndex);
        }
        
        // Find the index of the opening brace/bracket
        int openBraceIndex = codeContent.IndexOf('{', startIndex);
        if (openBraceIndex == -1)
        {
            // Special case for single-line methods/functions like "public int Foo() => 42;"
            if (codeContent.Substring(startIndex).Contains("=>") && codeContent.Substring(startIndex).Contains(";"))
            {
                int endIndex = codeContent.IndexOf(';', startIndex);
                if (endIndex != -1)
                {
                    return codeContent.Substring(startIndex, endIndex - startIndex + 1);
                }
            }
            return string.Empty;
        }
        
        // Track brace nesting level
        int nestLevel = 1;
        int index = openBraceIndex + 1;
        
        while (index < codeContent.Length && nestLevel > 0)
        {
            // Skip string literals and comments
            if (index < codeContent.Length - 1)
            {
                if (codeContent[index] == '/' && codeContent[index + 1] == '/')
                {
                    // Skip single-line comment
                    int lineEnd = codeContent.IndexOf('\n', index);
                    if (lineEnd == -1) break;
                    index = lineEnd + 1;
                    continue;
                }
                else if (codeContent[index] == '/' && codeContent[index + 1] == '*')
                {
                    // Skip multi-line comment
                    int commentEnd = codeContent.IndexOf("*/", index);
                    if (commentEnd == -1) break;
                    index = commentEnd + 2;
                    continue;
                }
                else if (codeContent[index] == '"' || codeContent[index] == '\'')
                {
                    // Skip string literal
                    char quote = codeContent[index];
                    index++;
                    while (index < codeContent.Length && 
                           (codeContent[index] != quote || 
                            (index > 0 && codeContent[index - 1] == '\\')))
                    {
                        index++;
                    }
                    if (index < codeContent.Length) index++; // Skip closing quote
                    continue;
                }
            }
            
            // Update nesting level based on braces
            if (codeContent[index] == '{')
            {
                nestLevel++;
            }
            else if (codeContent[index] == '}')
            {
                nestLevel--;
            }
            
            index++;
        }
        
        // If we didn't find a balanced block, return up to some reasonable limit
        if (nestLevel > 0)
        {
            // Just extract up to a reasonable number of lines as a fallback
            int endIndex = Math.Min(startIndex + 2000, codeContent.Length);
            return codeContent.Substring(startIndex, endIndex - startIndex);
        }
        
        // Return the balanced block including the final closing brace
        return codeContent.Substring(startIndex, index - startIndex);
    }

    /// <summary>
    /// Extracts a Python code block based on indentation
    /// </summary>
    private string ExtractPythonBlock(string codeContent, int startIndex)
    {
        string[] lines = codeContent.Substring(startIndex).Split('\n');
        if (lines.Length == 0) return string.Empty;
        
        // Find the indentation level of the function/class definition
        string firstLine = lines[0];
        int baseIndentLevel = firstLine.Length - firstLine.TrimStart().Length;
        
        StringBuilder blockBuilder = new StringBuilder();
        blockBuilder.AppendLine(firstLine);
        
        // We expect the next line to establish the block's indentation level
        int blockIndentLevel = -1;
        bool foundFirstIndented = false;
        
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            
            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                blockBuilder.AppendLine(line);
                continue;
            }
            
            int currentIndent = line.Length - line.TrimStart().Length;
            
            // First non-empty line after the definition establishes the block indent level
            if (!foundFirstIndented && currentIndent > baseIndentLevel)
            {
                blockIndentLevel = currentIndent;
                foundFirstIndented = true;
            }
            
            // If we've found our block's indentation level
            if (blockIndentLevel != -1)
            {
                // If this line has less indentation than the block level, 
                // we've exited the block
                if (currentIndent < blockIndentLevel && currentIndent <= baseIndentLevel)
                {
                    break;
                }
            }
            
            // Add the line to our block
            blockBuilder.AppendLine(line);
        }
        
        return blockBuilder.ToString();
    }

    /// <summary>
    /// Sub-splits a large code block into smaller chunks
    /// </summary>
    private async Task SubSplitCodeBlockAsync(
        (string type, string name, string content, int startPosition) block,
        string language,
        Dictionary<string, string> baseMetadata,
        string filePath,
        string projectId,
        string userId,
        List<ChunkResult> finalizedChunks,
        ref int chunkIndex)
    {
        // Extract the signature (first line or declaration) of the block
        string signature = ExtractBlockSignature(block.content, block.type);
        
        // Split the block content by lines
        string[] lines = block.content.Split('\n');
        
        // Initialize variables for chunking
        StringBuilder currentChunk = new StringBuilder();
        currentChunk.AppendLine(signature);
        
        int currentTokens = await _tokenizer.EstimateTokenCountAsync(signature);
        int linesInCurrentChunk = 1;
        
        // Process each line
        for (int i = 1; i < lines.Length; i++) // Start at 1 to skip signature we already added
        {
            string line = lines[i];
            int lineTokens = await _tokenizer.EstimateTokenCountAsync(line);
            
            // If adding this line would exceed the token limit and we have content
            if (currentTokens + lineTokens > _options.MaxCodeChunkTokens && linesInCurrentChunk > 1)
            {
                // Finalize current chunk
                string chunkContent = currentChunk.ToString();
                
                var metadata = new Dictionary<string, string>(baseMetadata);
                metadata["chunkIndex"] = chunkIndex.ToString();
                metadata["isSubChunk"] = "true";
                
                finalizedChunks.Add(new ChunkResult(
                    chunkContent,
                    metadata,
                    filePath,
                    chunkIndex++,
                    await _tokenizer.EstimateTokenCountAsync(chunkContent)));
                
                // Start a new chunk with the signature and the current line
                currentChunk.Clear();
                currentChunk.AppendLine(signature);
                
                // Add overlap by including previous lines (up to CodeOverlapLines)
                int overlapStart = Math.Max(1, i - _options.CodeOverlapLines);
                for (int j = overlapStart; j < i; j++)
                {
                    // Skip signature line if it's within the overlap window
                    if (j == 0) continue;
                    
                    currentChunk.AppendLine(lines[j]);
                }
                
                // Now add the current line
                currentChunk.AppendLine(line);
                
                // Recalculate tokens
                currentTokens = await _tokenizer.EstimateTokenCountAsync(currentChunk.ToString());
                linesInCurrentChunk = 1 + (i - overlapStart) + 1; // Signature + overlap + current
            }
            else
            {
                // Add the line to the current chunk
                currentChunk.AppendLine(line);
                currentTokens += lineTokens;
                linesInCurrentChunk++;
            }
        }
        
        // Add any remaining content as the final chunk
        if (linesInCurrentChunk > 1)
        {
            string chunkContent = currentChunk.ToString();
            
            var metadata = new Dictionary<string, string>(baseMetadata);
            metadata["chunkIndex"] = chunkIndex.ToString();
            metadata["isSubChunk"] = "true";
            
            finalizedChunks.Add(new ChunkResult(
                chunkContent,
                metadata,
                filePath,
                chunkIndex++,
                await _tokenizer.EstimateTokenCountAsync(chunkContent)));
        }
    }

    /// <summary>
    /// Extracts the signature or declaration line of a code block
    /// </summary>
    private string ExtractBlockSignature(string blockContent, string blockType)
    {
        // Get the first line that defines the block
        int endOfFirstLine = blockContent.IndexOf('\n');
        if (endOfFirstLine == -1)
        {
            return blockContent; // Single line block
        }
        
        string firstLine = blockContent.Substring(0, endOfFirstLine);
        
        // For function blocks, if the signature spans multiple lines (parameters, etc.)
        // try to capture the full signature
        if (blockType == "function" && !firstLine.Contains('{') && !firstLine.Contains(':'))
        {
            // Find opening brace or Python colon, which would end the signature
            int openingBrace = blockContent.IndexOf('{');
            int colonIndex = blockContent.IndexOf(':');
            
            int signatureEnd = -1;
            if (openingBrace != -1 && colonIndex != -1)
            {
                signatureEnd = Math.Min(openingBrace, colonIndex);
            }
            else if (openingBrace != -1)
            {
                signatureEnd = openingBrace;
            }
            else if (colonIndex != -1)
            {
                signatureEnd = colonIndex;
            }
            
            if (signatureEnd != -1)
            {
                return blockContent.Substring(0, signatureEnd + 1);
            }
        }
        
        return firstLine;
    }

    /// <summary>
    /// Chunks code by lines when semantic chunking isn't possible
    /// </summary>
    private async Task<List<ChunkResult>> ChunkByLinesAsync(
        string codeContent,
        string language,
        string filePath,
        string projectId,
        string userId)
    {
        var allChunks = new List<ChunkResult>();
        int globalChunkIndex = 0;
        string fileExtension = Path.GetExtension(filePath);
        
        // Split code into lines
        string[] lines = codeContent.Split('\n');
        
        // Initialize chunking variables
        StringBuilder currentChunk = new StringBuilder();
        int currentTokens = 0;
        int linesInCurrentChunk = 0;
        
        // Process each line
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            int lineTokens = await _tokenizer.EstimateTokenCountAsync(line);
            
            // If adding this line would exceed the token limit and we have content
            if (currentTokens + lineTokens > _options.MaxCodeChunkTokens && linesInCurrentChunk > 0)
            {
                // Finalize current chunk
                string chunkContent = currentChunk.ToString();
                
                var metadata = new Dictionary<string, string>
                {
                    ["userId"] = userId,
                    ["projectId"] = projectId,
                    ["filePath"] = filePath,
                    ["fileName"] = Path.GetFileName(filePath),
                    ["fileType"] = fileExtension,
                    ["language"] = language,
                    ["chunkIndex"] = globalChunkIndex.ToString(),
                    ["chunkType"] = "line-based"
                };
                
                allChunks.Add(new ChunkResult(
                    chunkContent,
                    metadata,
                    filePath,
                    globalChunkIndex++,
                    currentTokens));
                
                // Start a new chunk
                currentChunk.Clear();
                
                // Add overlap by including previous lines (up to CodeOverlapLines)
                int overlapStart = Math.Max(0, i - _options.CodeOverlapLines);
                for (int j = overlapStart; j < i; j++)
                {
                    currentChunk.AppendLine(lines[j]);
                }
                
                // Calculate new token count with overlap
                currentTokens = await _tokenizer.EstimateTokenCountAsync(currentChunk.ToString());
                linesInCurrentChunk = i - overlapStart;
            }
            
            // Add the current line
            currentChunk.AppendLine(line);
            currentTokens += lineTokens;
            linesInCurrentChunk++;
        }
        
        // Add any remaining content as the final chunk
        if (linesInCurrentChunk > 0)
        {
            string chunkContent = currentChunk.ToString();
            
            var metadata = new Dictionary<string, string>
            {
                ["userId"] = userId,
                ["projectId"] = projectId,
                ["filePath"] = filePath,
                ["fileName"] = Path.GetFileName(filePath),
                ["fileType"] = fileExtension,
                ["language"] = language,
                ["chunkIndex"] = globalChunkIndex.ToString(),
                ["chunkType"] = "line-based"
            };
            
            allChunks.Add(new ChunkResult(
                chunkContent,
                metadata,
                filePath,
                globalChunkIndex,
                currentTokens));
        }
        
        return allChunks;
    }
}
namespace RagWorkspace.Api.Configuration;

/// <summary>
/// Configuration options for text chunking in the RAG pipeline
/// </summary>
public class ChunkingOptions
{
    /// <summary>
    /// Maximum token count for code chunks
    /// </summary>
    public int MaxCodeChunkTokens { get; set; } = 1500;
    
    /// <summary>
    /// Number of overlapping lines when sub-splitting large code units
    /// </summary>
    public int CodeOverlapLines { get; set; } = 3;
    
    /// <summary>
    /// Maximum token count for markdown chunks
    /// </summary>
    public int MaxMarkdownChunkTokens { get; set; } = 1000;
    
    /// <summary>
    /// Number of overlapping tokens when sub-splitting large markdown sections
    /// </summary>
    public int MarkdownOverlapTokens { get; set; } = 100;
    
    /// <summary>
    /// Maximum token count for configuration file chunks (JSON, YAML, etc.)
    /// </summary>
    public int MaxConfigChunkTokens { get; set; } = 750;
    
    /// <summary>
    /// Maximum token count for generic text chunks
    /// </summary>
    public int MaxTextChunkTokens { get; set; } = 500;
    
    /// <summary>
    /// Number of overlapping tokens when sub-splitting large text blocks
    /// </summary>
    public int TextOverlapTokens { get; set; } = 50;
    
    /// <summary>
    /// Maximum file size in bytes to process
    /// </summary>
    public int MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB default
}
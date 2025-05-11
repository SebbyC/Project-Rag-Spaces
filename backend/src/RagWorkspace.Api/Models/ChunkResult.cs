namespace RagWorkspace.Api.Models;

/// <summary>
/// Represents a chunk of text content with metadata for the RAG pipeline
/// </summary>
public class ChunkResult
{
    /// <summary>
    /// The actual chunked content
    /// </summary>
    public string Content { get; set; }
    
    /// <summary>
    /// Metadata associated with this chunk
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
    
    /// <summary>
    /// The relative path to the original file on the storage
    /// </summary>
    public string OriginalFilePath { get; set; }
    
    /// <summary>
    /// Zero-based index for chunks from the same file
    /// </summary>
    public int ChunkIndex { get; set; }
    
    /// <summary>
    /// Estimated token count of the content
    /// </summary>
    public int EstimatedTokenCount { get; set; }

    /// <summary>
    /// Creates a new instance of ChunkResult
    /// </summary>
    public ChunkResult()
    {
        Content = string.Empty;
        Metadata = new Dictionary<string, string>();
        OriginalFilePath = string.Empty;
        ChunkIndex = 0;
        EstimatedTokenCount = 0;
    }

    /// <summary>
    /// Creates a new instance of ChunkResult with specified values
    /// </summary>
    public ChunkResult(
        string content, 
        Dictionary<string, string> metadata, 
        string originalFilePath, 
        int chunkIndex, 
        int tokenCount)
    {
        Content = content;
        Metadata = metadata;
        OriginalFilePath = originalFilePath;
        ChunkIndex = chunkIndex;
        EstimatedTokenCount = tokenCount;
    }
    
    /// <summary>
    /// Creates a unique identifier for this chunk based on its properties
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <returns>A unique identifier string</returns>
    public string GenerateId(string projectId)
    {
        string idBase = $"{projectId}_{OriginalFilePath}_{ChunkIndex}";
        // Replace characters that are invalid in IDs with underscores
        return idBase.Replace("/", "_").Replace("\\", "_").Replace(".", "_");
    }
    
    /// <summary>
    /// Adds standard metadata fields to the chunk
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="fileExtension">The file extension</param>
    public void AddStandardMetadata(string userId, string projectId, string fileExtension)
    {
        Metadata["userId"] = userId;
        Metadata["projectId"] = projectId;
        Metadata["filePath"] = OriginalFilePath;
        Metadata["fileName"] = Path.GetFileName(OriginalFilePath);
        Metadata["fileType"] = fileExtension;
        Metadata["chunkIndex"] = ChunkIndex.ToString();
    }
}
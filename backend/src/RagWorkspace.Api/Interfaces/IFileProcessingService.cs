namespace RagWorkspace.Api.Interfaces;

public interface IFileProcessingService
{
    /// <summary>
    /// Processes a directory recursively, chunking files and generating embeddings
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="directoryPathOnShare">The relative path to the directory on the file share</param>
    Task ProcessDirectoryAsync(string userId, string projectId, string directoryPathOnShare);
    
    /// <summary>
    /// Processes a single file, chunking it and generating embeddings
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="filePathOnShare">The relative path to the file on the file share</param>
    Task ProcessFileAsync(string userId, string projectId, string filePathOnShare);
}
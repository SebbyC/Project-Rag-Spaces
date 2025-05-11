namespace RagWorkspace.Api.Interfaces;

public interface IGitHubService
{
    Task SyncRepositoryAsync(string userId, string projectId, string repoUrl);
    Task<string> CloneRepositoryAsync(string userId, string projectId, string repoUrl, string? accessToken = null);
    Task<IEnumerable<string>> GetRepositoryFilesAsync(string userId, string projectId);
    Task<Stream> GetRepositoryFileContentAsync(string userId, string projectId, string filePath);
}
using RagWorkspace.Api.Models;

namespace RagWorkspace.Api.Interfaces;

public interface IProjectService
{
    Task<Project> CreateProjectAsync(string userId, CreateProjectRequest request);
    Task<Project?> GetProjectAsync(string userId, string projectId);
    Task<IEnumerable<Project>> GetUserProjectsAsync(string userId);
    Task DeleteProjectAsync(string userId, string projectId);
    Task UpdateProjectAsync(string userId, string projectId, UpdateProjectRequest request);
}

public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ProjectType Type { get; set; } = ProjectType.Repository;
    public string? GitHubUrl { get; set; }
}

public class UpdateProjectRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? GitHubUrl { get; set; }
}
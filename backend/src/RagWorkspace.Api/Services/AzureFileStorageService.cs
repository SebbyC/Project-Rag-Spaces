using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Microsoft.Extensions.Options;
using RagWorkspace.Api.Configuration;
using RagWorkspace.Api.Interfaces;

namespace RagWorkspace.Api.Services;

public class AzureFileStorageService : IFileStorageService
{
    private readonly ShareClient _shareClient;
    private readonly FileStorageOptions _options;
    private readonly ILogger<AzureFileStorageService> _logger;

    public AzureFileStorageService(
        IOptions<FileStorageOptions> options,
        ILogger<AzureFileStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _shareClient = new ShareClient(_options.ConnectionString, _options.ShareName);
    }

    public async Task<Stream> OpenReadAsync(string path)
    {
        try
        {
            var fileClient = GetFileClient(path);
            var response = await fileClient.DownloadAsync();
            return response.Value.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening file {Path} for reading", path);
            throw;
        }
    }

    public async Task WriteAsync(string path, Stream data)
    {
        try
        {
            var fileClient = GetFileClient(path);
            
            // Create parent directories if they don't exist
            await EnsureDirectoryExistsAsync(path);
            
            await fileClient.CreateAsync(data.Length);
            await fileClient.UploadAsync(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing file {Path}", path);
            throw;
        }
    }

    public async Task DeleteAsync(string path)
    {
        try
        {
            var fileClient = GetFileClient(path);
            await fileClient.DeleteIfExistsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {Path}", path);
            throw;
        }
    }

    public async Task<IEnumerable<Interfaces.FileInfo>> ListAsync(string directory)
    {
        try
        {
            var directoryClient = GetDirectoryClient(directory);
            var files = new List<Interfaces.FileInfo>();
            
            // Create directory if it doesn't exist
            await directoryClient.CreateIfNotExistsAsync();

            await foreach (var item in directoryClient.GetFilesAndDirectoriesAsync())
            {
                ShareFileProperties? properties = null;
                
                if (!item.IsDirectory)
                {
                    var fileClient = directoryClient.GetFileClient(item.Name);
                    properties = await fileClient.GetPropertiesAsync();
                }
                
                files.Add(new Interfaces.FileInfo
                {
                    Name = item.Name,
                    Path = Path.Combine(directory, item.Name).Replace('\\', '/'),
                    IsDirectory = item.IsDirectory,
                    Size = item.FileSize ?? 0,
                    ModifiedAt = properties?.LastModified.DateTime ?? DateTime.UtcNow
                });
            }

            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing directory {Directory}", directory);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string path)
    {
        try
        {
            var fileClient = GetFileClient(path);
            var response = await fileClient.ExistsAsync();
            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if file {Path} exists", path);
            throw;
        }
    }

    public string GetAbsolutePath(string relativePath)
    {
        return Path.Combine(_options.MountPath, relativePath).Replace('\\', '/');
    }

    private ShareFileClient GetFileClient(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var directory = _shareClient.GetRootDirectoryClient();
        
        // Navigate to the parent directory
        for (int i = 0; i < segments.Length - 1; i++)
        {
            directory = directory.GetSubdirectoryClient(segments[i]);
        }
        
        // Get the file
        return directory.GetFileClient(segments[^1]);
    }

    private ShareDirectoryClient GetDirectoryClient(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var directory = _shareClient.GetRootDirectoryClient();
        
        foreach (var segment in segments)
        {
            directory = directory.GetSubdirectoryClient(segment);
        }
        
        return directory;
    }
    
    private async Task EnsureDirectoryExistsAsync(string filePath)
    {
        var segments = filePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var directory = _shareClient.GetRootDirectoryClient();
        
        // Create intermediate directories
        for (int i = 0; i < segments.Length - 1; i++)
        {
            directory = directory.GetSubdirectoryClient(segments[i]);
            await directory.CreateIfNotExistsAsync();
        }
    }
}
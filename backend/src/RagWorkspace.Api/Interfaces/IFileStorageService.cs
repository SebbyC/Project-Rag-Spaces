namespace RagWorkspace.Api.Interfaces;

public interface IFileStorageService
{
    Task<Stream> OpenReadAsync(string path);
    Task WriteAsync(string path, Stream data);
    Task DeleteAsync(string path);
    Task<IEnumerable<FileInfo>> ListAsync(string directory);
    Task<bool> ExistsAsync(string path);
    string GetAbsolutePath(string relativePath);
}

public class FileInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool IsDirectory { get; set; }
}
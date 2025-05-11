using Microsoft.Extensions.Options;
using RagWorkspace.Api.Configuration;
using RagWorkspace.Api.Interfaces;
using RagWorkspace.Api.Models;
using RagWorkspace.Api.Services.Chunkers;

namespace RagWorkspace.Api.Services;

/// <summary>
/// Service that processes files, chunks them, and generates embeddings
/// </summary>
public class FileProcessingService : IFileProcessingService
{
    private readonly IFileStorageService _fileStorage;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorService _vectorService;
    private readonly ILogger<FileProcessingService> _logger;
    private readonly ITokenizerService _tokenizer;
    private readonly ChunkingOptions _chunkingOptions;
    private readonly ApplicationDbContext _dbContext;
    
    // Specialized chunkers
    private readonly PlainTextChunker _plainTextChunker;
    private readonly MarkdownChunker _markdownChunker;
    private readonly CodeChunker _codeChunker;
    private readonly JsonYamlChunker _jsonYamlChunker;
    
    // File extensions to process (others will be ignored)
    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Code files
        ".cs", ".js", ".ts", ".jsx", ".tsx", ".py", ".java", ".go", ".rs", ".cpp", ".c", ".h", ".hpp",
        // Web files
        ".html", ".css", ".scss", ".sass", ".less",
        // Config files
        ".json", ".xml", ".yaml", ".yml", ".toml", ".ini", ".env",
        // Documentation files
        ".md", ".txt", ".rst", ".adoc"
    };

    // Files to explicitly ignore
    private static readonly HashSet<string> _ignoredFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "package-lock.json", "yarn.lock", "pnpm-lock.yaml", ".gitignore", ".gitattributes",
        ".eslintrc", ".prettierrc", ".editorconfig", "*.min.js", "*.min.css"
    };
    
    // Language mappings
    private static readonly Dictionary<string, string> _languageMappings = new()
    {
        [".cs"] = "csharp",
        [".js"] = "javascript",
        [".ts"] = "typescript",
        [".jsx"] = "jsx",
        [".tsx"] = "tsx",
        [".py"] = "python",
        [".java"] = "java",
        [".go"] = "go",
        [".rs"] = "rust",
        [".cpp"] = "cpp",
        [".cc"] = "cpp",
        [".c"] = "c",
        [".h"] = "cpp",
        [".hpp"] = "cpp",
        [".html"] = "html",
        [".css"] = "css",
        [".scss"] = "scss",
        [".sass"] = "scss",
        [".less"] = "less",
        [".md"] = "markdown",
        [".json"] = "json",
        [".xml"] = "xml",
        [".yaml"] = "yaml",
        [".yml"] = "yaml",
        [".toml"] = "toml",
        [".ini"] = "ini",
        [".env"] = "text"
    };

    public FileProcessingService(
        IFileStorageService fileStorage,
        IEmbeddingProvider embeddingProvider,
        IVectorService vectorService,
        ApplicationDbContext dbContext,
        ILogger<FileProcessingService> logger,
        ITokenizerService tokenizer,
        IOptions<ChunkingOptions> chunkingOptions,
        PlainTextChunker plainTextChunker,
        MarkdownChunker markdownChunker,
        CodeChunker codeChunker,
        JsonYamlChunker jsonYamlChunker)
    {
        _fileStorage = fileStorage;
        _embeddingProvider = embeddingProvider;
        _vectorService = vectorService;
        _dbContext = dbContext;
        _logger = logger;
        _tokenizer = tokenizer;
        _chunkingOptions = chunkingOptions.Value;
        
        _plainTextChunker = plainTextChunker;
        _markdownChunker = markdownChunker;
        _codeChunker = codeChunker;
        _jsonYamlChunker = jsonYamlChunker;
    }

    /// <summary>
    /// Processes a directory recursively, chunking files and generating embeddings
    /// </summary>
    public async Task ProcessDirectoryAsync(string userId, string projectId, string directoryPathOnShare)
    {
        _logger.LogInformation("Processing directory: {DirectoryPath} for project {ProjectId}",
            directoryPathOnShare, projectId);

        try
        {
            // Get all files in the directory recursively
            var allFiles = await _fileStorage.ListAsync(directoryPathOnShare);

            // Process each file that isn't a directory
            foreach (var fileInfo in allFiles.Where(f => !f.IsDirectory))
            {
                try
                {
                    // Skip non-supported file types and ignored files
                    string extension = Path.GetExtension(fileInfo.Path);
                    string fileName = Path.GetFileName(fileInfo.Path);
                    
                    if (!_supportedExtensions.Contains(extension) || 
                        _ignoredFiles.Contains(fileName) ||
                        fileInfo.Size > _chunkingOptions.MaxFileSizeBytes)
                    {
                        _logger.LogDebug("Skipping file: {FilePath} (unsupported type, ignored, or too large)",
                            fileInfo.Path);
                        continue;
                    }

                    // Process the file
                    await ProcessFileAsync(userId, projectId, fileInfo.Path);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing file {FilePath}", fileInfo.Path);
                    // Continue with other files even if one fails
                }
            }
            
            _logger.LogInformation("Finished directory processing: {DirectoryPath}", directoryPathOnShare);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing directory {DirectoryPath}", directoryPathOnShare);
            throw;
        }
    }

    /// <summary>
    /// Processes a single file, chunking it and generating embeddings
    /// </summary>
    public async Task ProcessFileAsync(string userId, string projectId, string filePathOnShare)
    {
        _logger.LogInformation("Processing file: {FilePath} for project {ProjectId}",
            filePathOnShare, projectId);

        try
        {
            // Check if file exists
            if (!await _fileStorage.ExistsAsync(filePathOnShare))
            {
                _logger.LogWarning("File does not exist: {FilePath}", filePathOnShare);
                return;
            }

            // Read the file content
            string fileContent;
            using (var stream = await _fileStorage.OpenReadAsync(filePathOnShare))
            using (var reader = new StreamReader(stream))
            {
                fileContent = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(fileContent))
            {
                _logger.LogWarning("File is empty: {FilePath}", filePathOnShare);
                return;
            }

            // Get the file extension and determine language
            string extension = Path.GetExtension(filePathOnShare).ToLowerInvariant();
            string language = GetLanguageFromExtension(extension);
            
            // Create a document record in the database if it doesn't exist
            var documentEntity = await _dbContext.Documents.FirstOrDefaultAsync(d => 
                d.ProjectId == projectId && d.Path == filePathOnShare);
                
            if (documentEntity == null)
            {
                documentEntity = new Document
                {
                    ProjectId = projectId,
                    Name = Path.GetFileName(filePathOnShare),
                    Path = filePathOnShare,
                    Type = extension,
                    Size = fileContent.Length,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                _dbContext.Documents.Add(documentEntity);
            }
            else
            {
                documentEntity.Size = fileContent.Length;
                documentEntity.UpdatedAt = DateTime.UtcNow;
            }
            
            await _dbContext.SaveChangesAsync();

            // Chunk the file content based on type
            List<ChunkResult> chunks = await ChunkContentAsync(
                fileContent, extension, language, filePathOnShare, projectId, userId);
                
            if (chunks.Count == 0)
            {
                _logger.LogWarning("No chunks generated for file: {FilePath}", filePathOnShare);
                return;
            }
            
            _logger.LogInformation("Created {ChunkCount} chunks for file {FilePath}",
                chunks.Count, filePathOnShare);

            // Process each chunk
            foreach (var chunk in chunks)
            {
                try
                {
                    // Generate embedding for this chunk
                    float[] embedding = await _embeddingProvider.GenerateEmbeddingAsync(chunk.Content);
                    
                    if (embedding.Length == 0)
                    {
                        _logger.LogWarning("Empty embedding generated for chunk {ChunkIndex} of file {FilePath}",
                            chunk.ChunkIndex, filePathOnShare);
                        continue;
                    }

                    // Create a unique ID for this chunk
                    string chunkId = chunk.GenerateId(projectId);
                    
                    // Store the embedding in Qdrant
                    var vectorDocument = new VectorDocument
                    {
                        Id = chunkId,
                        Vector = embedding,
                        Content = chunk.Content,
                        Metadata = chunk.Metadata
                    };

                    await _vectorService.StoreEmbeddingAsync(vectorDocument);
                    _logger.LogDebug("Stored embedding for chunk {ChunkIndex} of file {FilePath}",
                        chunk.ChunkIndex, filePathOnShare);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing chunk {ChunkIndex} for file {FilePath}",
                        chunk.ChunkIndex, filePathOnShare);
                }
            }

            // Update the document entity with chunk count
            documentEntity.IndexedAt = DateTime.UtcNow;
            documentEntity.ChunkCount = chunks.Count;
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Successfully processed file {FilePath} with {ChunkCount} chunks",
                filePathOnShare, chunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file {FilePath}", filePathOnShare);
            throw;
        }
    }

    /// <summary>
    /// Chunks content based on file type
    /// </summary>
    private async Task<List<ChunkResult>> ChunkContentAsync(
        string content,
        string extension,
        string language,
        string filePath,
        string projectId,
        string userId)
    {
        // Determine the appropriate chunking strategy based on file type
        if (IsMarkdownFile(extension))
        {
            return await _markdownChunker.ChunkAsync(content, filePath, projectId, userId);
        }
        else if (IsCodeFile(extension))
        {
            return await _codeChunker.ChunkAsync(content, language, filePath, projectId, userId);
        }
        else if (IsJsonFile(extension))
        {
            return await _jsonYamlChunker.ChunkJsonAsync(content, filePath, projectId, userId);
        }
        else if (IsYamlFile(extension))
        {
            return await _jsonYamlChunker.ChunkYamlAsync(content, filePath, projectId, userId);
        }
        else
        {
            // Default to plain text chunking for other file types
            return await _plainTextChunker.ChunkAsync(content, filePath, projectId, userId);
        }
    }

    /// <summary>
    /// Gets the language identifier from file extension
    /// </summary>
    private string GetLanguageFromExtension(string extension)
    {
        return _languageMappings.TryGetValue(extension, out string? language) 
            ? language 
            : "text";
    }

    /// <summary>
    /// Checks if the file is a Markdown file
    /// </summary>
    private static bool IsMarkdownFile(string extension)
    {
        return extension == ".md" || extension == ".markdown";
    }

    /// <summary>
    /// Checks if the file is a code file
    /// </summary>
    private static bool IsCodeFile(string extension)
    {
        return new[] {
            ".cs", ".js", ".ts", ".jsx", ".tsx", ".py", ".java", ".go", ".rs", ".cpp", ".c", ".h", ".hpp",
            ".html", ".css", ".scss", ".sass", ".less"
        }.Contains(extension);
    }

    /// <summary>
    /// Checks if the file is a JSON file
    /// </summary>
    private static bool IsJsonFile(string extension)
    {
        return extension == ".json";
    }

    /// <summary>
    /// Checks if the file is a YAML file
    /// </summary>
    private static bool IsYamlFile(string extension)
    {
        return extension == ".yaml" || extension == ".yml";
    }
}
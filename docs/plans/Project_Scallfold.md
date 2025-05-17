# RAG Workspace - Mega Implementation Plan

## 🎯 Project Overview

Build a production-ready RAG (Retrieval-Augmented Generation) platform that enables developers to interact with their codebases using AI. The system will support multiple LLMs (Azure OpenAI, OpenAI, Google Gemini), integrate with Azure File Share for persistent storage, and provide a seamless experience across local development and cloud deployments.

## 🏗️ Phase 1: Infrastructure Setup

### 1.1 Repository Structure
```bash
mkdir rag-workspace
cd rag-workspace
```

Create the following structure:
```text
rag-workspace/
├── backend/
│   ├── src/
│   │   └── RagWorkspace.Api/
│   │       ├── Controllers/
│   │       ├── Services/
│   │       ├── Models/
│   │       ├── Configuration/
│   │       ├── Interfaces/
│   │       └── Middleware/
│   ├── tests/
│   │   └── RagWorkspace.Api.Tests/
│   └── Dockerfile
├── frontend/
│   ├── app/
│   ├── components/
│   ├── lib/
│   ├── public/
│   └── Dockerfile
├── infrastructure/
│   ├── terraform/
│   ├── bicep/
│   └── scripts/
├── vector-db/
│   └── docker/
├── docs/
│   ├── architecture/
│   └── api/
├── .github/
│   └── workflows/
├── docker-compose.yml
├── docker-compose.azure.yml
├── .env.example
└── README.md
```

### 1.2 Environment Configuration

Create `.env.example`:
```env
# Azure OpenAI
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
AZURE_OPENAI_KEY=your-key
AZURE_OPENAI_MODEL=gpt-4o
AZURE_OPENAI_EMBEDDING_MODEL=text-embedding-3-large

# OpenAI API
OPENAI_API_KEY=your-key

# Google Vertex AI
GOOGLE_APPLICATION_CREDENTIALS=/app/credentials/gcp-credentials.json
VERTEX_PROJECT_ID=your-project-id
VERTEX_LOCATION=us-central1

# Azure Storage - File Share
AZURE_STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol=...
AZURE_FILE_SHARE_NAME=uploads
STORAGE_ACCOUNT=youraccount
STORAGE_KEY=yourkey
MOUNT_PATH=/mnt/uploads
MAX_UPLOAD_MB=250

# GitHub Integration
GITHUB_TOKEN=your-token
GITHUB_CLIENT_ID=your-client-id
GITHUB_CLIENT_SECRET=your-client-secret

# Database
DATABASE_CONNECTION_STRING=Host=postgres;Database=ragworkspace;Username=postgres;Password=yourpassword
REDIS_CONNECTION_STRING=redis:6379

# Security
JWT_SECRET=your-jwt-secret
NEXTAUTH_SECRET=your-nextauth-secret

# Application
ALLOWED_ORIGINS=http://localhost:3000
DEFAULT_LLM_PROVIDER=azure
DEFAULT_MODEL=gpt-4o
```

### 1.3 Docker Compose Configuration

Create `docker-compose.yml`:
```yaml
version: "3.9"

services:
  # Vector Database
  qdrant:
    image: qdrant/qdrant:v1.9.1
    container_name: rag-qdrant
    volumes:
      - qdrant_data:/qdrant/storage
    ports:
      - "6333:6333"
    networks:
      - rag-network

  # PostgreSQL
  postgres:
    image: postgres:15-alpine
    container_name: rag-postgres
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ragworkspace
    volumes:
      - postgres_data:/var/lib/postgresql/data
    ports:
      - "5432:5432"
    networks:
      - rag-network

  # Redis
  redis:
    image: redis:7-alpine
    container_name: rag-redis
    volumes:
      - redis_data:/data
    ports:
      - "6379:6379"
    networks:
      - rag-network

  # .NET Backend API
  backend:
    build:
      context: .
      dockerfile: backend/Dockerfile
    container_name: rag-backend
    environment:
      # All environment variables from .env
      - AZURE_OPENAI_ENDPOINT=${AZURE_OPENAI_ENDPOINT}
      - AZURE_OPENAI_KEY=${AZURE_OPENAI_KEY}
      - AZURE_STORAGE_CONNECTION_STRING=${AZURE_STORAGE_CONNECTION_STRING}
      - MOUNT_PATH=${MOUNT_PATH}
      # ... rest of env vars
    volumes:
      - azurefiles:${MOUNT_PATH}
      - ${GOOGLE_APPLICATION_CREDENTIALS}:/app/credentials/gcp-credentials.json:ro
    ports:
      - "8080:8080"
    depends_on:
      - qdrant
      - postgres
      - redis
    networks:
      - rag-network

  # Next.js Frontend
  frontend:
    build:
      context: .
      dockerfile: frontend/Dockerfile
    container_name: rag-frontend
    environment:
      - NEXT_PUBLIC_API_URL=http://localhost:8080
      - NEXTAUTH_URL=http://localhost:3000
      - NEXTAUTH_SECRET=${NEXTAUTH_SECRET}
    ports:
      - "3000:3000"
    depends_on:
      - backend
    networks:
      - rag-network

volumes:
  qdrant_data:
  postgres_data:
  redis_data:
  azurefiles:
    driver: local
    driver_opts:
      type: cifs
      o: "mfsymlinks,vers=3.0,username=${STORAGE_ACCOUNT},password=${STORAGE_KEY},addr=${STORAGE_ACCOUNT}.file.core.windows.net"
      device: "//${STORAGE_ACCOUNT}.file.core.windows.net/${AZURE_FILE_SHARE_NAME}"

networks:
  rag-network:
    driver: bridge
```

### 1.4 Azure-specific Docker Compose

Create `docker-compose.azure.yml`:
```yaml
version: "3.9"

services:
  backend:
    volumes:
      - ${MOUNT_PATH}:${MOUNT_PATH}
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
```

## 🚀 Phase 2: Backend Implementation

### 2.1 Core Project Structure

Create `backend/src/RagWorkspace.Api/RagWorkspace.Api.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <!-- Core packages -->
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.0.0" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
    
    <!-- Authentication -->
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
    
    <!-- Azure -->
    <PackageReference Include="Azure.AI.OpenAI" Version="1.0.0-beta.17" />
    <PackageReference Include="Azure.Storage.Files.Shares" Version="12.17.1" />
    <PackageReference Include="Azure.Identity" Version="1.10.4" />
    
    <!-- OpenAI -->
    <PackageReference Include="OpenAI" Version="1.10.0" />
    
    <!-- Google Cloud -->
    <PackageReference Include="Google.Cloud.AIPlatform.V1" Version="3.5.0" />
    
    <!-- Database -->
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
    <PackageReference Include="StackExchange.Redis" Version="2.7.10" />
    
    <!-- Vector Database -->
    <PackageReference Include="Qdrant.Client" Version="1.7.0" />
    
    <!-- GitHub -->
    <PackageReference Include="Octokit" Version="9.1.0" />
    
    <!-- Real-time -->
    <PackageReference Include="Microsoft.AspNetCore.SignalR" Version="1.1.0" />
    
    <!-- Utilities -->
    <PackageReference Include="AutoMapper.Extensions.Microsoft.DependencyInjection" Version="12.0.1" />
    <PackageReference Include="FluentValidation.AspNetCore" Version="11.3.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
    <PackageReference Include="MediatR" Version="12.2.0" />
  </ItemGroup>
</Project>
```

### 2.2 Configuration Classes

Create `backend/src/RagWorkspace.Api/Configuration/AppConfiguration.cs`:
```csharp
namespace RagWorkspace.Api.Configuration;

public class AppConfiguration
{
    public AzureOpenAIOptions AzureOpenAI { get; set; } = new();
    public OpenAIOptions OpenAI { get; set; } = new();
    public GoogleVertexAIOptions GoogleVertexAI { get; set; } = new();
    public FileStorageOptions FileStorage { get; set; } = new();
    public QdrantOptions Qdrant { get; set; } = new();
    public GitHubOptions GitHub { get; set; } = new();
    public JwtOptions Jwt { get; set; } = new();
}

public class AzureOpenAIOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = "gpt-4o";
    public string EmbeddingModel { get; set; } = "text-embedding-3-large";
}

public class OpenAIOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = "gpt-4";
}

public class GoogleVertexAIOptions
{
    public string ProjectId { get; set; } = string.Empty;
    public string Location { get; set; } = "us-central1";
    public string ModelName { get; set; } = "gemini-2.5-pro";
    public string CredentialsPath { get; set; } = string.Empty;
}

public class FileStorageOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ShareName { get; set; } = "uploads";
    public string MountPath { get; set; } = "/mnt/uploads";
    public int MaxUploadSizeMB { get; set; } = 250;
}

public class QdrantOptions
{
    public string Url { get; set; } = "http://qdrant:6333";
    public string CollectionName { get; set; } = "code-embeddings";
}

public class GitHubOptions
{
    public string Token { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

public class JwtOptions
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "rag-workspace";
    public string Audience { get; set; } = "rag-workspace-api";
    public int ExpirationMinutes { get; set; } = 60;
}
```

### 2.3 Core Interfaces

Create `backend/src/RagWorkspace.Api/Interfaces/ILLMService.cs`:
```csharp
namespace RagWorkspace.Api.Interfaces;

public interface ILLMService
{
    Task<LLMResponse> GenerateCompletionAsync(LLMRequest request);
    Task<float[]> GenerateEmbeddingAsync(string text);
    string GetProviderName();
}

public class LLMRequest
{
    public List<ChatMessage> Messages { get; set; } = new();
    public string? Model { get; set; }
    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 2000;
    public bool Stream { get; set; } = false;
}

public class LLMResponse
{
    public string Content { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
}

public class ChatMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}
```

Create `backend/src/RagWorkspace.Api/Interfaces/IFileStorageService.cs`:
```csharp
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
```

Create `backend/src/RagWorkspace.Api/Interfaces/IVectorService.cs`:
```csharp
namespace RagWorkspace.Api.Interfaces;

public interface IVectorService
{
    Task<string> StoreEmbeddingAsync(VectorDocument document);
    Task<IEnumerable<VectorSearchResult>> SearchAsync(float[] queryVector, int limit = 10, Dictionary<string, string>? filters = null);
    Task DeleteAsync(string id);
    Task<bool> CollectionExistsAsync(string collectionName);
    Task CreateCollectionAsync(string collectionName, int vectorSize);
}

public class VectorDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public float[] Vector { get; set; } = Array.Empty<float>();
    public Dictionary<string, string> Metadata { get; set; } = new();
    public string Content { get; set; } = string.Empty;
}

public class VectorSearchResult
{
    public string Id { get; set; } = string.Empty;
    public float Score { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public string Content { get; set; } = string.Empty;
}
```

### 2.4 Service Implementations

Create `backend/src/RagWorkspace.Api/Services/AzureOpenAIService.cs`:
```csharp
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using RagWorkspace.Api.Configuration;
using RagWorkspace.Api.Interfaces;

namespace RagWorkspace.Api.Services;

public class AzureOpenAIService : ILLMService
{
    private readonly OpenAIClient _client;
    private readonly AzureOpenAIOptions _options;
    private readonly ILogger<AzureOpenAIService> _logger;

    public AzureOpenAIService(
        IOptions<AzureOpenAIOptions> options,
        ILogger<AzureOpenAIService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new OpenAIClient(
            new Uri(_options.Endpoint),
            new AzureKeyCredential(_options.ApiKey));
    }

    public async Task<LLMResponse> GenerateCompletionAsync(LLMRequest request)
    {
        try
        {
            var chatMessages = request.Messages
                .Select(m => new ChatRequestMessage(
                    m.Role == "system" ? ChatRole.System :
                    m.Role == "assistant" ? ChatRole.Assistant :
                    ChatRole.User, m.Content))
                .ToList();

            var options = new ChatCompletionsOptions(
                request.Model ?? _options.ModelName,
                chatMessages)
            {
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens
            };

            var response = await _client.GetChatCompletionsAsync(options);
            var completion = response.Value;

            return new LLMResponse
            {
                Content = completion.Choices[0].Message.Content,
                TokensUsed = completion.Usage.TotalTokens,
                Model = request.Model ?? _options.ModelName,
                Provider = GetProviderName()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating completion with Azure OpenAI");
            throw;
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        try
        {
            var options = new EmbeddingsOptions(_options.EmbeddingModel, new[] { text });
            var response = await _client.GetEmbeddingsAsync(options);
            return response.Value.Data[0].Embedding.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding with Azure OpenAI");
            throw;
        }
    }

    public string GetProviderName() => "azure-openai";
}
```

Create `backend/src/RagWorkspace.Api/Services/GeminiService.cs`:
```csharp
using Google.Cloud.AIPlatform.V1;
using Google.Protobuf;
using Microsoft.Extensions.Options;
using RagWorkspace.Api.Configuration;
using RagWorkspace.Api.Interfaces;

namespace RagWorkspace.Api.Services;

public class GeminiService : ILLMService
{
    private readonly GoogleVertexAIOptions _options;
    private readonly PredictionServiceClient _client;
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(
        IOptions<GoogleVertexAIOptions> options,
        ILogger<GeminiService> logger)
    {
        _options = options.Value;
        _logger = logger;
        
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", _options.CredentialsPath);
        _client = PredictionServiceClient.Create();
    }

    public async Task<LLMResponse> GenerateCompletionAsync(LLMRequest request)
    {
        try
        {
            var endpoint = $"projects/{_options.ProjectId}/locations/{_options.Location}/publishers/google/models/{_options.ModelName}";
            
            var prompt = string.Join("\n", request.Messages.Select(m => $"{m.Role}: {m.Content}"));
            
            var parameters = new Value
            {
                StructValue = new Struct
                {
                    Fields =
                    {
                        ["temperature"] = Value.ForNumber(request.Temperature),
                        ["maxOutputTokens"] = Value.ForNumber(request.MaxTokens),
                        ["topK"] = Value.ForNumber(40),
                        ["topP"] = Value.ForNumber(0.95)
                    }
                }
            };

            var instances = new[]
            {
                new Value
                {
                    StructValue = new Struct
                    {
                        Fields = { ["content"] = Value.ForString(prompt) }
                    }
                }
            };

            var response = await _client.PredictAsync(endpoint, instances, parameters);
            var prediction = response.Predictions[0];
            var content = prediction.StructValue.Fields["content"].StringValue;

            return new LLMResponse
            {
                Content = content,
                TokensUsed = 0, // Gemini doesn't return token count in standard predict API
                Model = _options.ModelName,
                Provider = GetProviderName()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating completion with Gemini");
            throw;
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        // Gemini doesn't expose embeddings directly in the same way
        // You'd need to use a different model or approach
        throw new NotImplementedException("Embedding generation not implemented for Gemini");
    }

    public string GetProviderName() => "google-gemini";
}
```

Create `backend/src/RagWorkspace.Api/Services/AzureFileStorageService.cs`:
```csharp
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

    public async Task<IEnumerable<FileInfo>> ListAsync(string directory)
    {
        try
        {
            var directoryClient = GetDirectoryClient(directory);
            var files = new List<FileInfo>();

            await foreach (var item in directoryClient.GetFilesAndDirectoriesAsync())
            {
                files.Add(new FileInfo
                {
                    Name = item.Name,
                    Path = Path.Combine(directory, item.Name),
                    IsDirectory = item.IsDirectory,
                    Size = item.FileSize ?? 0
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
        return Path.Combine(_options.MountPath, relativePath);
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
}
```

Create `backend/src/RagWorkspace.Api/Services/QdrantVectorService.cs`:
```csharp
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using RagWorkspace.Api.Configuration;
using RagWorkspace.Api.Interfaces;

namespace RagWorkspace.Api.Services;

public class QdrantVectorService : IVectorService
{
    private readonly QdrantClient _client;
    private readonly QdrantOptions _options;
    private readonly ILogger<QdrantVectorService> _logger;

    public QdrantVectorService(
        IOptions<QdrantOptions> options,
        ILogger<QdrantVectorService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new QdrantClient(_options.Url);
    }

    public async Task<string> StoreEmbeddingAsync(VectorDocument document)
    {
        try
        {
            var points = new List<PointStruct>
            {
                new PointStruct
                {
                    Id = new PointId { Uuid = document.Id },
                    Vectors = document.Vector,
                    Payload = document.Metadata.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new Value { StringValue = kvp.Value }
                    )
                }
            };

            await _client.UpsertAsync(_options.CollectionName, points);
            return document.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing embedding");
            throw;
        }
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchAsync(
        float[] queryVector, 
        int limit = 10, 
        Dictionary<string, string>? filters = null)
    {
        try
        {
            Filter? qdrantFilter = null;
            if (filters != null && filters.Any())
            {
                var conditions = filters.Select(kvp => new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = kvp.Key,
                        Match = new Match { Keyword = kvp.Value }
                    }
                }).ToList();

                qdrantFilter = new Filter
                {
                    Must = { conditions }
                };
            }

            var results = await _client.SearchAsync(
                _options.CollectionName,
                queryVector,
                limit: (ulong)limit,
                filter: qdrantFilter);

            return results.Select(r => new VectorSearchResult
            {
                Id = r.Id.Uuid,
                Score = r.Score,
                Metadata = r.Payload.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.StringValue)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching vectors");
            throw;
        }
    }

    public async Task DeleteAsync(string id)
    {
        try
        {
            await _client.DeleteAsync(
                _options.CollectionName,
                new List<PointId> { new PointId { Uuid = id } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting vector {Id}", id);
            throw;
        }
    }

    public async Task<bool> CollectionExistsAsync(string collectionName)
    {
        try
        {
            var collections = await _client.ListCollectionsAsync();
            return collections.Collections.Any(c => c.Name == collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking collection existence");
            throw;
        }
    }

    public async Task CreateCollectionAsync(string collectionName, int vectorSize)
    {
        try
        {
            await _client.CreateCollectionAsync(
                collectionName,
                new VectorParams { Size = (ulong)vectorSize, Distance = Distance.Cosine });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating collection {CollectionName}", collectionName);
            throw;
        }
    }
}
```

### 2.5 Controllers

Create `backend/src/RagWorkspace.Api/Controllers/ChatController.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RagWorkspace.Api.Interfaces;
using RagWorkspace.Api.Models;

namespace RagWorkspace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatService chatService,
        ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        try
        {
            var userId = User.Identity?.Name ?? "anonymous";
            var response = await _chatService.ProcessChatAsync(userId, request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return StatusCode(500, new { error = "An error occurred processing your request" });
        }
    }

    [HttpPost("stream")]
    public async Task StreamChat([FromBody] ChatRequest request)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Add("Cache-Control", "no-cache");
        Response.Headers.Add("Connection", "keep-alive");

        var userId = User.Identity?.Name ?? "anonymous";
        
        await foreach (var chunk in _chatService.StreamChatAsync(userId, request))
        {
            await Response.WriteAsync($"data: {chunk}\n\n");
            await Response.Body.FlushAsync();
        }
    }
}
```

Create `backend/src/RagWorkspace.Api/Controllers/FileController.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RagWorkspace.Api.Interfaces;
using RagWorkspace.Api.Services;

namespace RagWorkspace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FileController : ControllerBase
{
    private readonly IFileStorageService _fileStorage;
    private readonly IFileProcessingService _fileProcessor;
    private readonly ILogger<FileController> _logger;

    public FileController(
        IFileStorageService fileStorage,
        IFileProcessingService fileProcessor,
        ILogger<FileController> logger)
    {
        _fileStorage = fileStorage;
        _fileProcessor = fileProcessor;
        _logger = logger;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(250_000_000)] // 250MB limit
    public async Task<IActionResult> Upload(IFormFile file)
    {
        try
        {
            var userId = User.Identity?.Name ?? "anonymous";
            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = $"{userId}/uploads/{fileName}";

            // Save file to Azure File Share
            using var stream = file.OpenReadStream();
            await _fileStorage.WriteAsync(filePath, stream);

            // Start processing in background
            _ = _fileProcessor.ProcessFileAsync(userId, filePath);

            return Accepted(new { path = filePath, status = "processing" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file");
            return StatusCode(500, new { error = "Error uploading file" });
        }
    }

    [HttpGet("{*path}")]
    public async Task<IActionResult> Download(string path)
    {
        try
        {
            var userId = User.Identity?.Name ?? "anonymous";
            var fullPath = $"{userId}/{path}";

            if (!await _fileStorage.ExistsAsync(fullPath))
            {
                return NotFound();
            }

            var stream = await _fileStorage.OpenReadAsync(fullPath);
            var fileName = Path.GetFileName(path);
            
            return File(stream, "application/octet-stream", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file");
            return StatusCode(500, new { error = "Error downloading file" });
        }
    }

    [HttpDelete("{*path}")]
    public async Task<IActionResult> Delete(string path)
    {
        try
        {
            var userId = User.Identity?.Name ?? "anonymous";
            var fullPath = $"{userId}/{path}";

            await _fileStorage.DeleteAsync(fullPath);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file");
            return StatusCode(500, new { error = "Error deleting file" });
        }
    }
}
```

Create `backend/src/RagWorkspace.Api/Controllers/ProjectController.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RagWorkspace.Api.Interfaces;
using RagWorkspace.Api.Models;

namespace RagWorkspace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IGitHubService _gitHubService;
    private readonly ILogger<ProjectController> _logger;

    public ProjectController(
        IProjectService projectService,
        IGitHubService gitHubService,
        ILogger<ProjectController> logger)
    {
        _projectService = projectService;
        _gitHubService = gitHubService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
    {
        try
        {
            var userId = User.Identity?.Name ?? "anonymous";
            var project = await _projectService.CreateProjectAsync(userId, request);
            return CreatedAtAction(nameof(GetProject), new { id = project.Id }, project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project");
            return StatusCode(500, new { error = "Error creating project" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProject(string id)
    {
        try
        {
            var userId = User.Identity?.Name ?? "anonymous";
            var project = await _projectService.GetProjectAsync(userId, id);
            
            if (project == null)
            {
                return NotFound();
            }

            return Ok(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project");
            return StatusCode(500, new { error = "Error getting project" });
        }
    }

    [HttpPost("{id}/sync-github")]
    public async Task<IActionResult> SyncGitHub(string id, [FromBody] SyncGitHubRequest request)
    {
        try
        {
            var userId = User.Identity?.Name ?? "anonymous";
            await _gitHubService.SyncRepositoryAsync(userId, id, request.RepoUrl);
            return Accepted(new { status = "syncing" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing GitHub repository");
            return StatusCode(500, new { error = "Error syncing repository" });
        }
    }
}
```

### 2.6 Background Services

Create `backend/src/RagWorkspace.Api/Services/FileProcessingService.cs`:
```csharp
using RagWorkspace.Api.Interfaces;
using System.IO.Compression;

namespace RagWorkspace.Api.Services;

public class FileProcessingService : IFileProcessingService
{
    private readonly IFileStorageService _fileStorage;
    private readonly ILLMService _llmService;
    private readonly IVectorService _vectorService;
    private readonly ILogger<FileProcessingService> _logger;

    public FileProcessingService(
        IFileStorageService fileStorage,
        ILLMService llmService,
        IVectorService vectorService,
        ILogger<FileProcessingService> logger)
    {
        _fileStorage = fileStorage;
        _llmService = llmService;
        _vectorService = vectorService;
        _logger = logger;
    }

    public async Task ProcessFileAsync(string userId, string filePath)
    {
        try
        {
            var extension = Path.GetExtension(filePath).ToLower();
            
            switch (extension)
            {
                case ".zip":
                    await ProcessZipFileAsync(userId, filePath);
                    break;
                case ".md":
                case ".txt":
                    await ProcessTextFileAsync(userId, filePath);
                    break;
                case ".cs":
                case ".js":
                case ".py":
                case ".java":
                    await ProcessCodeFileAsync(userId, filePath);
                    break;
                default:
                    _logger.LogWarning("Unsupported file type: {Extension}", extension);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file {FilePath}", filePath);
            throw;
        }
    }

    private async Task ProcessZipFileAsync(string userId, string filePath)
    {
        var extractPath = Path.Combine(Path.GetDirectoryName(filePath)!, "extracted", Path.GetFileNameWithoutExtension(filePath));
        
        using var zipStream = await _fileStorage.OpenReadAsync(filePath);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        
        foreach (var entry in archive.Entries)
        {
            if (entry.Length == 0) continue;
            
            var entryPath = Path.Combine(extractPath, entry.FullName);
            var directory = Path.GetDirectoryName(entryPath)!;
            
            // Extract file
            using var entryStream = entry.Open();
            using var memoryStream = new MemoryStream();
            await entryStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            
            await _fileStorage.WriteAsync(entryPath, memoryStream);
            
            // Process extracted file
            await ProcessFileAsync(userId, entryPath);
        }
    }

    private async Task ProcessTextFileAsync(string userId, string filePath)
    {
        using var stream = await _fileStorage.OpenReadAsync(filePath);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        
        // Split into chunks (simple implementation - improve for production)
        var chunks = ChunkText(content, 1000);
        
        foreach (var chunk in chunks)
        {
            var embedding = await _llmService.GenerateEmbeddingAsync(chunk);
            
            var document = new VectorDocument
            {
                Vector = embedding,
                Content = chunk,
                Metadata = new Dictionary<string, string>
                {
                    ["userId"] = userId,
                    ["filePath"] = filePath,
                    ["fileType"] = "text"
                }
            };
            
            await _vectorService.StoreEmbeddingAsync(document);
        }
    }

    private async Task ProcessCodeFileAsync(string userId, string filePath)
    {
        using var stream = await _fileStorage.OpenReadAsync(filePath);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        
        // For code files, we might want to extract functions/classes
        // This is a simple implementation - use proper AST parsing in production
        var chunks = ChunkCode(content);
        
        foreach (var chunk in chunks)
        {
            var embedding = await _llmService.GenerateEmbeddingAsync(chunk);
            
            var document = new VectorDocument
            {
                Vector = embedding,
                Content = chunk,
                Metadata = new Dictionary<string, string>
                {
                    ["userId"] = userId,
                    ["filePath"] = filePath,
                    ["fileType"] = "code",
                    ["language"] = GetLanguageFromExtension(Path.GetExtension(filePath))
                }
            };
            
            await _vectorService.StoreEmbeddingAsync(document);
        }
    }

    private List<string> ChunkText(string text, int chunkSize)
    {
        var chunks = new List<string>();
        var words = text.Split(' ');
        var currentChunk = new List<string>();
        var currentSize = 0;

        foreach (var word in words)
        {
            if (currentSize + word.Length > chunkSize && currentChunk.Any())
            {
                chunks.Add(string.Join(" ", currentChunk));
                currentChunk.Clear();
                currentSize = 0;
            }
            
            currentChunk.Add(word);
            currentSize += word.Length + 1;
        }

        if (currentChunk.Any())
        {
            chunks.Add(string.Join(" ", currentChunk));
        }

        return chunks;
    }

    private List<string> ChunkCode(string code)
    {
        // Simple line-based chunking - improve with AST parsing
        var lines = code.Split('\n');
        var chunks = new List<string>();
        var currentChunk = new List<string>();
        var currentSize = 0;
        const int maxChunkSize = 50; // lines

        foreach (var line in lines)
        {
            currentChunk.Add(line);
            currentSize++;

            if (currentSize >= maxChunkSize)
            {
                chunks.Add(string.Join("\n", currentChunk));
                currentChunk.Clear();
                currentSize = 0;
            }
        }

        if (currentChunk.Any())
        {
            chunks.Add(string.Join("\n", currentChunk));
        }

        return chunks;
    }

    private string GetLanguageFromExtension(string extension)
    {
        return extension.ToLower() switch
        {
            ".cs" => "csharp",
            ".js" => "javascript",
            ".py" => "python",
            ".java" => "java",
            ".go" => "go",
            ".rs" => "rust",
            _ => "unknown"
        };
    }
}
```

### 2.7 Startup Configuration

Create `backend/src/RagWorkspace.Api/Program.cs`:
```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RagWorkspace.Api.Configuration;
using RagWorkspace.Api.Data;
using RagWorkspace.Api.Interfaces;
using RagWorkspace.Api.Middleware;
using RagWorkspace.Api.Services;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/rag-workspace-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Configure services
builder.Services.Configure<AppConfiguration>(builder.Configuration);
builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.Configure<OpenAIOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<GoogleVertexAIOptions>(builder.Configuration.GetSection("GoogleVertexAI"));
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection("FileStorage"));
builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection("GitHub"));

// Add controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
    {
        policy.WithOrigins(builder.Configuration["AllowedOrigins"]?.Split(',') ?? new[] { "*" })
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Configure Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["JWT_SECRET"] ?? throw new InvalidOperationException("JWT_SECRET not configured")))
        };
    });

// Configure Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// Configure SignalR
builder.Services.AddSignalR();

// Register services
builder.Services.AddScoped<ILLMService, LLMServiceFactory>();
builder.Services.AddScoped<AzureOpenAIService>();
builder.Services.AddScoped<OpenAIService>();
builder.Services.AddScoped<GeminiService>();
builder.Services.AddScoped<IFileStorageService, AzureFileStorageService>();
builder.Services.AddScoped<IVectorService, QdrantVectorService>();
builder.Services.AddScoped<IFileProcessingService, FileProcessingService>();
builder.Services.AddScoped<IGitHubService, GitHubService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IRAGService, RAGService>();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection") ?? "")
    .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "");

var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseCors("AllowedOrigins");

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<ErrorHandlingMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health");

// Map SignalR hubs
app.MapHub<ChatHub>("/hubs/chat");

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    
    // Initialize vector database collections
    var vectorService = scope.ServiceProvider.GetRequiredService<IVectorService>();
    if (!await vectorService.CollectionExistsAsync("code-embeddings"))
    {
        await vectorService.CreateCollectionAsync("code-embeddings", 1536); // OpenAI embedding size
    }
}

app.Run();
```

### 2.8 Database Models

Create `backend/src/RagWorkspace.Api/Data/ApplicationDbContext.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using RagWorkspace.Api.Models;

namespace RagWorkspace.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<ChatSession> ChatSessions { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<Document> Documents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Project>()
            .HasOne(p => p.Owner)
            .WithMany(u => u.Projects)
            .HasForeignKey(p => p.OwnerId);

        modelBuilder.Entity<ChatSession>()
            .HasOne(s => s.User)
            .WithMany(u => u.ChatSessions)
            .HasForeignKey(s => s.UserId);

        modelBuilder.Entity<ChatSession>()
            .HasOne(s => s.Project)
            .WithMany(p => p.ChatSessions)
            .HasForeignKey(s => s.ProjectId);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Session)
            .WithMany(s => s.Messages)
            .HasForeignKey(m => m.SessionId);

        modelBuilder.Entity<Document>()
            .HasOne(d => d.Project)
            .WithMany(p => p.Documents)
            .HasForeignKey(d => d.ProjectId);
    }
}
```

Create `backend/src/RagWorkspace.Api/Models/DomainModels.cs`:
```csharp
namespace RagWorkspace.Api.Models;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
}

public class Project
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public ProjectType Type { get; set; }
    public string? GitHubUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public User Owner { get; set; } = null!;
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
}

public enum ProjectType
{
    Repository,
    Documentation,
    Mixed
}

public class ChatSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public User User { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public string Role { get; set; } = "user"; // user, assistant, system
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public ChatSession Session { get; set; } = null!;
}

public class Document
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public Project Project { get; set; } = null!;
}
```

## 🎨 Phase 3: Frontend Implementation

### 3.1 Next.js Setup

Create `frontend/package.json`:
```json
{
  "name": "rag-workspace-frontend",
  "version": "0.1.0",
  "private": true,
  "scripts": {
    "dev": "next dev",
    "build": "next build",
    "start": "next start",
    "lint": "next lint",
    "type-check": "tsc --noEmit"
  },
  "dependencies": {
    "next": "14.0.4",
    "react": "^18.2.0",
    "react-dom": "^18.2.0",
    "typescript": "^5.3.3",
    "@types/node": "^20.10.0",
    "@types/react": "^18.2.45",
    "@types/react-dom": "^18.2.18",
    "tailwindcss": "^3.4.0",
    "@tailwindcss/forms": "^0.5.7",
    "@tailwindcss/typography": "^0.5.10",
    "axios": "^1.6.2",
    "@tanstack/react-query": "^5.12.2",
    "next-auth": "^4.24.5",
    "socket.io-client": "^4.7.2",
    "lucide-react": "^0.294.0",
    "clsx": "^2.0.0",
    "zod": "^3.22.4",
    "react-hot-toast": "^2.4.1",
    "zustand": "^4.4.7",
    "react-markdown": "^9.0.1",
    "react-syntax-highlighter": "^15.5.0",
    "@microsoft/signalr": "^8.0.0"
  },
  "devDependencies": {
    "eslint": "^8.55.0",
    "eslint-config-next": "14.0.4",
    "@typescript-eslint/eslint-plugin": "^6.13.0",
    "@typescript-eslint/parser": "^6.13.0",
    "prettier": "^3.1.0",
    "prettier-plugin-tailwindcss": "^0.5.7"
  }
}
```

### 3.2 Core Layout and Pages

Create `frontend/app/layout.tsx`:
```tsx
import type { Metadata } from 'next'
import { Inter } from 'next/font/google'
import './globals.css'
import { Providers } from '@/components/providers'
import { Toaster } from 'react-hot-toast'

const inter = Inter({ subsets: ['latin'] })

export const metadata: Metadata = {
  title: 'RAG Workspace',
  description: 'AI-powered code assistant with deep project understanding',
}

export default function RootLayout({
  children,
}: {
  children: React.ReactNode
}) {
  return (
    <html lang="en">
      <body className={inter.className}>
        <Providers>
          {children}
          <Toaster position="bottom-right" />
        </Providers>
      </body>
    </html>
  )
}
```

Create `frontend/app/page.tsx`:
```tsx
import Link from 'next/link'
import { Button } from '@/components/ui/button'

export default function Home() {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center p-24">
      <div className="z-10 max-w-5xl w-full items-center justify-between font-mono text-sm">
        <h1 className="text-4xl font-bold mb-8 text-center">RAG Workspace</h1>
        <p className="text-xl text-center mb-8">
          AI-powered code assistant with deep project understanding
        </p>
        <div className="flex gap-4 justify-center">
          <Link href="/auth/signin">
            <Button>Sign In</Button>
          </Link>
          <Link href="/dashboard">
            <Button variant="outline">Dashboard</Button>
          </Link>
        </div>
      </div>
    </main>
  )
}
```

Create `frontend/app/dashboard/page.tsx`:
```tsx
'use client'

import { useSession } from 'next-auth/react'
import { redirect } from 'next/navigation'
import { DashboardLayout } from '@/components/layouts/dashboard-layout'
import { ProjectList } from '@/components/projects/project-list'
import { CreateProjectButton } from '@/components/projects/create-project-button'

export default function Dashboard() {
  const { data: session, status } = useSession()

  if (status === 'loading') {
    return <div>Loading...</div>
  }

  if (!session) {
    redirect('/auth/signin')
  }

  return (
    <DashboardLayout>
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-2xl font-bold">Projects</h1>
        <CreateProjectButton />
      </div>
      <ProjectList />
    </DashboardLayout>
  )
}
```

Create `frontend/app/chat/[projectId]/page.tsx`:
```tsx
'use client'

import { useParams } from 'next/navigation'
import { DashboardLayout } from '@/components/layouts/dashboard-layout'
import { ChatInterface } from '@/components/chat/chat-interface'
import { FileExplorer } from '@/components/files/file-explorer'

export default function ChatPage() {
  const params = useParams()
  const projectId = params.projectId as string

  return (
    <DashboardLayout>
      <div className="flex h-[calc(100vh-4rem)] gap-4">
        <div className="w-64 border-r">
          <FileExplorer projectId={projectId} />
        </div>
        <div className="flex-1">
          <ChatInterface projectId={projectId} />
        </div>
      </div>
    </DashboardLayout>
  )
}
```

### 3.3 Core Components

Create `frontend/components/chat/chat-interface.tsx`:
```tsx
'use client'

import { useState, useRef, useEffect } from 'react'
import { MessageList } from './message-list'
import { MessageInput } from './message-input'
import { useChat } from '@/hooks/use-chat'
import { ModelSelector } from './model-selector'

interface ChatInterfaceProps {
  projectId: string
}

export function ChatInterface({ projectId }: ChatInterfaceProps) {
  const [model, setModel] = useState('azure-gpt-4')
  const { messages, sendMessage, isLoading } = useChat(projectId)
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  const handleSend = async (content: string) => {
    await sendMessage(content, model)
  }

  return (
    <div className="flex flex-col h-full">
      <div className="flex justify-between items-center p-4 border-b">
        <h2 className="text-lg font-semibold">Chat</h2>
        <ModelSelector value={model} onChange={setModel} />
      </div>
      
      <div className="flex-1 overflow-y-auto p-4">
        <MessageList messages={messages} />
        <div ref={bottomRef} />
      </div>
      
      <div className="p-4 border-t">
        <MessageInput onSend={handleSend} disabled={isLoading} />
      </div>
    </div>
  )
}
```

Create `frontend/components/chat/message-list.tsx`:
```tsx
import React from 'react'
import { Message } from '@/types/chat'
import { MessageItem } from './message-item'

interface MessageListProps {
  messages: Message[]
}

export function MessageList({ messages }: MessageListProps) {
  return (
    <div className="space-y-4">
      {messages.map((message) => (
        <MessageItem key={message.id} message={message} />
      ))}
    </div>
  )
}
```

Create `frontend/components/chat/message-item.tsx`:
```tsx
import React from 'react'
import ReactMarkdown from 'react-markdown'
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter'
import { vscDarkPlus } from 'react-syntax-highlighter/dist/esm/styles/prism'
import { Message } from '@/types/chat'
import { cn } from '@/lib/utils'
import { User, Bot } from 'lucide-react'

interface MessageItemProps {
  message: Message
}

export function MessageItem({ message }: MessageItemProps) {
  const isUser = message.role === 'user'

  return (
    <div
      className={cn(
        'flex gap-4 p-4 rounded-lg',
        isUser ? 'bg-gray-50' : 'bg-white'
      )}
    >
      <div className="flex-shrink-0">
        {isUser ? (
          <User className="w-6 h-6" />
        ) : (
          <Bot className="w-6 h-6 text-blue-600" />
        )}
      </div>
      <div className="flex-1 overflow-hidden">
        <ReactMarkdown
          className="prose prose-sm max-w-none"
          components={{
            code({ node, inline, className, children, ...props }) {
              const match = /language-(\w+)/.exec(className || '')
              return !inline && match ? (
                <SyntaxHighlighter
                  {...props}
                  style={vscDarkPlus}
                  language={match[1]}
                  PreTag="div"
                >
                  {String(children).replace(/\n$/, '')}
                </SyntaxHighlighter>
              ) : (
                <code {...props} className={className}>
                  {children}
                </code>
              )
            },
          }}
        >
          {message.content}
        </ReactMarkdown>
      </div>
    </div>
  )
}
```

Create `frontend/components/files/file-uploader.tsx`:
```tsx
'use client'

import { useCallback, useState } from 'react'
import { useDropzone } from 'react-dropzone'
import { Upload, File, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { api } from '@/lib/api'
import toast from 'react-hot-toast'

interface FileUploaderProps {
  projectId: string
  onUploadComplete?: () => void
}

export function FileUploader({ projectId, onUploadComplete }: FileUploaderProps) {
  const [uploading, setUploading] = useState(false)
  const [files, setFiles] = useState<File[]>([])

  const onDrop = useCallback((acceptedFiles: File[]) => {
    setFiles(acceptedFiles)
  }, [])

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    accept: {
      'application/zip': ['.zip'],
      'text/markdown': ['.md'],
      'text/plain': ['.txt'],
      'text/x-python': ['.py'],
      'text/javascript': ['.js'],
      'text/x-java': ['.java'],
      'text/x-csharp': ['.cs'],
    },
    maxSize: 250 * 1024 * 1024, // 250MB
  })

  const handleUpload = async () => {
    if (files.length === 0) return

    setUploading(true)
    try {
      for (const file of files) {
        const formData = new FormData()
        formData.append('file', file)
        formData.append('projectId', projectId)

        await api.post('/files/upload', formData, {
          headers: {
            'Content-Type': 'multipart/form-data',
          },
        })
      }

      toast.success('Files uploaded successfully')
      setFiles([])
      onUploadComplete?.()
    } catch (error) {
      toast.error('Error uploading files')
      console.error(error)
    } finally {
      setUploading(false)
    }
  }

  const removeFile = (index: number) => {
    setFiles(files.filter((_, i) => i !== index))
  }

  return (
    <div className="space-y-4">
      <div
        {...getRootProps()}
        className={cn(
          'border-2 border-dashed rounded-lg p-8 text-center cursor-pointer transition-colors',
          isDragActive ? 'border-blue-500 bg-blue-50' : 'border-gray-300 hover:border-gray-400'
        )}
      >
        <input {...getInputProps()} />
        <Upload className="mx-auto h-12 w-12 text-gray-400" />
        <p className="mt-2 text-sm text-gray-600">
          {isDragActive
            ? 'Drop the files here...'
            : 'Drag & drop files here, or click to select'}
        </p>
        <p className="mt-1 text-xs text-gray-500">
          Supports ZIP, Markdown, and code files (max 250MB)
        </p>
      </div>

      {files.length > 0 && (
        <div className="space-y-2">
          {files.map((file, index) => (
            <div
              key={index}
              className="flex items-center justify-between p-2 bg-gray-50 rounded"
            >
              <div className="flex items-center gap-2">
                <File className="h-4 w-4 text-gray-400" />
                <span className="text-sm">{file.name}</span>
                <span className="text-xs text-gray-500">
                  ({(file.size / 1024 / 1024).toFixed(2)} MB)
                </span>
              </div>
              <button
                onClick={() => removeFile(index)}
                className="text-red-500 hover:text-red-700"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
          ))}

          <Button
            onClick={handleUpload}
            disabled={uploading}
            className="w-full"
          >
            {uploading ? 'Uploading...' : 'Upload Files'}
          </Button>
        </div>
      )}
    </div>
  )
}
```

### 3.4 API Client and Hooks

Create `frontend/lib/api.ts`:
```typescript
import axios from 'axios'

const baseURL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080'

export const api = axios.create({
  baseURL,
  headers: {
    'Content-Type': 'application/json',
  },
})

// Add auth token to requests
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// Handle auth errors
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      // Redirect to login
      window.location.href = '/auth/signin'
    }
    return Promise.reject(error)
  }
)
```

Create `frontend/hooks/use-chat.ts`:
```typescript
import { useState, useEffect, useCallback } from 'react'
import { HubConnectionBuilder, HubConnection } from '@microsoft/signalr'
import { api } from '@/lib/api'
import { Message } from '@/types/chat'

export function useChat(projectId: string) {
  const [messages, setMessages] = useState<Message[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [connection, setConnection] = useState<HubConnection | null>(null)

  useEffect(() => {
    // Initialize SignalR connection
    const newConnection = new HubConnectionBuilder()
      .withUrl(`${process.env.NEXT_PUBLIC_API_URL}/hubs/chat`)
      .withAutomaticReconnect()
      .build()

    newConnection.on('ReceiveMessage', (message: Message) => {
      setMessages((prev) => [...prev, message])
    })

    newConnection.start().then(() => {
      setConnection(newConnection)
    })

    return () => {
      newConnection.stop()
    }
  }, [])

  const sendMessage = useCallback(
    async (content: string, model: string) => {
      setIsLoading(true)
      try {
        const response = await api.post('/chat', {
          projectId,
          content,
          model,
        })

        // Message will be received via SignalR
      } catch (error) {
        console.error('Error sending message:', error)
      } finally {
        setIsLoading(false)
      }
    },
    [projectId]
  )

  return {
    messages,
    sendMessage,
    isLoading,
  }
}
```

## 🚀 Phase 4: Deployment

### 4.1 GitHub Actions CI/CD

Create `.github/workflows/deploy.yml`:
```yaml
name: Deploy to Azure

on:
  push:
    branches: [main]
  workflow_dispatch:

env:
  AZURE_WEBAPP_NAME: rag-workspace
  AZURE_WEBAPP_PACKAGE_PATH: '.'
  DOTNET_VERSION: '8.0.x'
  NODE_VERSION: '20.x'

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: Setup Node.js
      uses: actions/setup-node@v4
      with:
        node-version: ${{ env.NODE_VERSION }}

    - name: Build Backend
      run: |
        cd backend
        dotnet restore
        dotnet build --configuration Release
        dotnet publish -c Release -o ./publish

    - name: Build Frontend
      run: |
        cd frontend
        npm ci
        npm run build

    - name: Login to Azure Container Registry
      uses: azure/docker-login@v1
      with:
        login-server: ${{ secrets.REGISTRY_LOGIN_SERVER }}
        username: ${{ secrets.REGISTRY_USERNAME }}
        password: ${{ secrets.REGISTRY_PASSWORD }}

    - name: Build and push Docker images
      run: |
        docker build -t ${{ secrets.REGISTRY_LOGIN_SERVER }}/rag-backend:${{ github.sha }} -f backend/Dockerfile .
        docker build -t ${{ secrets.REGISTRY_LOGIN_SERVER }}/rag-frontend:${{ github.sha }} -f frontend/Dockerfile .
        docker push ${{ secrets.REGISTRY_LOGIN_SERVER }}/rag-backend:${{ github.sha }}
        docker push ${{ secrets.REGISTRY_LOGIN_SERVER }}/rag-frontend:${{ github.sha }}

    - name: Deploy to Azure Container Apps
      uses: azure/container-apps-deploy-action@v1
      with:
        appSourcePath: ${{ github.workspace }}
        azureCredentials: ${{ secrets.AZURE_CREDENTIALS }}
        imageToDeploy: ${{ secrets.REGISTRY_LOGIN_SERVER }}/rag-backend:${{ github.sha }}
        containerAppName: rag-backend
        resourceGroup: ${{ secrets.RESOURCE_GROUP }}
```

### 4.2 Infrastructure as Code

Create `infrastructure/terraform/main.tf`:
```hcl
terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "main" {
  name     = "rag-workspace-rg"
  location = "East US"
}

resource "azurerm_storage_account" "main" {
  name                     = "ragworkspacestorage"
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
}

resource "azurerm_storage_share" "uploads" {
  name                 = "uploads"
  storage_account_name = azurerm_storage_account.main.name
  quota                = 100
}

resource "azurerm_container_app_environment" "main" {
  name                       = "rag-workspace-env"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
}

resource "azurerm_container_app_environment_storage" "uploads" {
  name                         = "uploads"
  container_app_environment_id = azurerm_container_app_environment.main.id
  account_name                 = azurerm_storage_account.main.name
  share_name                   = azurerm_storage_share.uploads.name
  access_key                   = azurerm_storage_account.main.primary_access_key
  access_mode                  = "ReadWrite"
}

resource "azurerm_container_app" "backend" {
  name                         = "rag-backend"
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"

  template {
    container {
      name   = "backend"
      image  = "myregistry.azurecr.io/rag-backend:latest"
      cpu    = 0.5
      memory = "1Gi"

      volume_mounts {
        name      = "uploads"
        path      = "/mnt/uploads"
      }

      env {
        name  = "AZURE_STORAGE_CONNECTION_STRING"
        value = azurerm_storage_account.main.primary_connection_string
      }
    }

    volume {
      name         = "uploads"
      storage_type = "AzureFile"
      storage_name = azurerm_container_app_environment_storage.uploads.name
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }
}
```

## 🧪 Phase 5: Testing Strategy

### 5.1 Backend Unit Tests

Create `backend/tests/RagWorkspace.Api.Tests/Services/AzureFileStorageServiceTests.cs`:
```csharp
using Microsoft.Extensions.Options;
using Moq;
using RagWorkspace.Api.Configuration;
using RagWorkspace.Api.Services;
using Xunit;

namespace RagWorkspace.Api.Tests.Services;

public class AzureFileStorageServiceTests
{
    private readonly Mock<IOptions<FileStorageOptions>> _optionsMock;
    private readonly Mock<ILogger<AzureFileStorageService>> _loggerMock;
    private readonly AzureFileStorageService _service;

    public AzureFileStorageServiceTests()
    {
        _optionsMock = new Mock<IOptions<FileStorageOptions>>();
        _loggerMock = new Mock<ILogger<AzureFileStorageService>>();
        
        _optionsMock.Setup(x => x.Value).Returns(new FileStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ShareName = "test-share",
            MountPath = "/mnt/test"
        });

        _service = new AzureFileStorageService(_optionsMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void GetAbsolutePath_ShouldCombinePathsCorrectly()
    {
        // Arrange
        var relativePath = "user1/file.txt";

        // Act
        var result = _service.GetAbsolutePath(relativePath);

        // Assert
        Assert.Equal("/mnt/test/user1/file.txt", result);
    }
}
```

### 5.2 Frontend Tests

Create `frontend/__tests__/components/chat/message-item.test.tsx`:
```typescript
import { render, screen } from '@testing-library/react'
import { MessageItem } from '@/components/chat/message-item'
import { Message } from '@/types/chat'

describe('MessageItem', () => {
  it('renders user message correctly', () => {
    const message: Message = {
      id: '1',
      role: 'user',
      content: 'Hello, world!',
      createdAt: new Date().toISOString(),
    }

    render(<MessageItem message={message} />)
    
    expect(screen.getByText('Hello, world!')).toBeInTheDocument()
  })

  it('renders assistant message with markdown', () => {
    const message: Message = {
      id: '2',
      role: 'assistant',
      content: '**Bold text** and `code`',
      createdAt: new Date().toISOString(),
    }

    render(<MessageItem message={message} />)
    
    expect(screen.getByText('Bold text')).toBeInTheDocument()
    expect(screen.getByText('code')).toBeInTheDocument()
  })
})
```

### 5.3 Integration Tests

Create `backend/tests/RagWorkspace.Api.Tests/Integration/ChatControllerIntegrationTests.cs`:
```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using Xunit;

namespace RagWorkspace.Api.Tests.Integration;

public class ChatControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ChatControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Chat_ShouldRequireAuthentication()
    {
        // Arrange
        var request = new { content = "Hello" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

## 📋 Phase 6: Documentation

### 6.1 API Documentation

Create `docs/api/README.md`:
```markdown
# RAG Workspace API Documentation

## Authentication

All API endpoints require JWT authentication. Include the token in the Authorization header:

```
Authorization: Bearer <token>
```

## Endpoints

### Chat

- `POST /api/chat` - Send a chat message
- `POST /api/chat/stream` - Stream chat responses

### Files

- `POST /api/files/upload` - Upload a file
- `GET /api/files/{path}` - Download a file
- `DELETE /api/files/{path}` - Delete a file

### Projects

- `POST /api/projects` - Create a project
- `GET /api/projects/{id}` - Get project details
- `POST /api/projects/{id}/sync-github` - Sync with GitHub repository

## WebSocket Events

Connect to `/hubs/chat` for real-time chat updates.

### Events

- `ReceiveMessage` - New message received
- `ProcessingStatus` - File processing status updates
```

### 6.2 Development Guide

Create `docs/DEVELOPMENT.md`:
```markdown
# Development Guide

## Prerequisites

- .NET 8 SDK
- Node.js 20+
- Docker and Docker Compose
- Azure CLI (for cloud deployment)

## Local Development

1. Clone the repository
2. Copy `.env.example` to `.env` and configure
3. Run `./scripts/dev.sh` to start all services

## Architecture

The application follows a microservices architecture:

- **Backend API**: .NET 8 Web API
- **Frontend**: Next.js 14
- **Vector DB**: Qdrant
- **Storage**: Azure File Share
- **Cache**: Redis
- **Database**: PostgreSQL

## Testing

```bash
# Backend tests
cd backend
dotnet test

# Frontend tests
cd frontend
npm test
```

## Deployment

Deployments are automated via GitHub Actions on merge to main branch.
```

## 🎯 Implementation Checklist

### Phase 1: Infrastructure ✓
- [x] Repository structure
- [x] Docker configuration
- [x] Environment setup
- [x] Azure File Share integration

### Phase 2: Backend ✓
- [x] Core project setup
- [x] Service implementations
- [x] Controller implementations
- [x] Database models
- [x] Background services

### Phase 3: Frontend ✓
- [x] Next.js setup
- [x] Core components
- [x] API integration
- [x] Real-time features

### Phase 4: Deployment ✓
- [x] CI/CD pipeline
- [x] Infrastructure as Code
- [x] Container deployment

### Phase 5: Testing ✓
- [x] Unit tests
- [x] Integration tests
- [x] E2E test strategy

### Phase 6: Documentation ✓
- [x] API documentation
- [x] Development guide
- [x] Architecture documentation

## 🚀 Next Steps

1. **Initialize Repository**: Run the setup scripts to create the project structure
2. **Configure Services**: Set up Azure resources and API keys
3. **Implement Core Features**: Follow the implementation plan phase by phase
4. **Deploy to Azure**: Use the provided IaC templates and CI/CD pipeline
5. **Test & Iterate**: Ensure all components work together seamlessly

This comprehensive implementation plan provides everything needed to build a production-ready RAG Workspace with multi-LLM support, Azure File Share integration, and real-time collaboration features.


Okay, let's consolidate everything into a comprehensive `project-start-implentation-plan.md`. This plan will serve as your roadmap from foundational setup to a feature-rich RAG co-workspace.

```markdown
# Project-RAG-CoWorkspace: Implementation Plan

**Version:** 1.0
**Date:** May 10, 2025
**Target Domain:** [https://project-rag.com](https://project-rag.com)

## 1. Project Vision & Overview

### 1.1. Project Title
Project-RAG-CoWorkspace

### 1.2. Brief Description
An AI-powered, flexible co-workspace designed for developers. It enables deep interaction with project codebases through a Retrieval-Augmented Generation (RAG) architecture, multi-LLM support, GitHub integration, persistent file storage with Azure File Share, and a Next.js web interface.

### 1.3. North Star Vision 🌟
The ultimate goal is to create an AI pair-programmer that deeply understands an entire codebase and collaborates with developers in real-time. The system aims to significantly boost developer productivity by handling tasks like project bootstrapping, context-aware code generation, multi-file refactoring, and providing insightful explanations about any part of the project. It will feel like having an incredibly knowledgeable and always-available AI teammate.

## 2. Core Features

*   **Retrieval-Augmented Generation (RAG):** Ground LLM responses using project files (code, documentation) and uploaded text.
*   **Multi-LLM Support:** Switchable LLMs: Google Gemini 2.5 Pro, Azure OpenAI (GPT-4, GPT-4o), OpenAI API.
*   **Context Overload:** Handle large context windows for deep codebase understanding.
*   **GitHub Integration:** Connect, index, and interact with entire GitHub repositories; analyze structure, files, and (eventually) commit history.
*   **Azure File Share Integration:** Persistent storage for cloned GitHub repos and user-uploaded files (zipped repos, markdown).
*   **Vector Database:** Qdrant for efficient semantic search of embeddings.
*   **Flexible Cowork Space:** User registration, (planned) authentication & persistent memory.
*   **Modern Tech Stack:** Next.js (Frontend), .NET 8 (Backend), Docker.
*   **Real-Time Streaming:** LLM token responses streamed to the UI.
*   **File Uploads & Management:** Support for various file types relevant to developers.

## 3. High-Level Architecture

The system will consist of a Next.js frontend, a .NET 8 backend API, a Qdrant vector database, and Azure File Share for persistent storage. LLM interactions will be routed to various external services (Azure OpenAI, Vertex AI, OpenAI API).

```mermaid
graph TD
    User[Developer User] -->|Interacts via project-rag.com| Frontend[Next.js UI]
    Frontend -->|REST API / SignalR| BackendDotNetAPI[.NET 8 API]

    subgraph "Backend Services & Storage"
        BackendDotNetAPI -->|LLM Calls| LLMRouter[LLM Router]
        LLMRouter --> AzureOpenAIService[Azure OpenAI Service]
        LLMRouter --> VertexAIService[Google Vertex AI (Gemini)]
        LLMRouter --> OpenAIServiceAPI[OpenAI API]

        BackendDotNetAPI -->|Semantic Search / Store Embeddings| QdrantDB[Qdrant Vector DB]
        BackendDotNetAPI -->|Store/Retrieve Project Files, Cloned Repos| AzureFileShare[Azure File Share]
        BackendDotNetAPI -->|User Data, Project Metadata| PostgreSQL[PostgreSQL Database]
        BackendDotNetAPI -->|Session Cache, Message Queue (Future)| RedisCache[Redis Cache]
    end

    BackendDotNetAPI -->|OAuth, API Calls| GitHubAPI[GitHub API]
    GitHubAPI -->|Provides Repo Data to| AzureFileShare
    AzureFileShare -->|Source for Indexing by Backend| QdrantDB

```

## 4. Technology Stack

*   **Frontend:** Next.js 14, React 18, TypeScript, Tailwind CSS, Axios, NextAuth.js, Zustand (or React Query/Context), SignalR Client.
*   **Backend:** .NET 8 (ASP.NET Core Web API), C#.
    *   **AI/LLM:** Azure.AI.OpenAI, Google.Cloud.AIPlatform.V1, OpenAI SDK.
    *   **Storage:** Azure.Storage.Files.Shares.
    *   **Database:** Entity Framework Core, Npgsql.EntityFrameworkCore.PostgreSQL.
    *   **Vector DB Client:** Qdrant.Client.
    *   **Real-time:** Microsoft.AspNetCore.SignalR.
    *   **Authentication:** JWT Bearer.
    *   **Other:** Serilog, AutoMapper, FluentValidation, MediatR, Octokit.
*   **Vector Database:** Qdrant.
*   **Persistent File Storage:** Azure File Share.
*   **Relational Database:** PostgreSQL.
*   **Caching/Messaging (Optional Future):** Redis.
*   **Containerization:** Docker, Docker Compose.
*   **CI/CD:** GitHub Actions.
*   **Cloud Platform:** Microsoft Azure (Container Apps, Storage Accounts, Azure DB for PostgreSQL, ACR, Key Vault).

## 5. Phase 1: Foundational Setup & Local Environment

### 5.1. Repository Initialization
*   **Root Directory:** `rag-workspace/`
*   **Key Files/Folders at Root:**
    *   `.gitignore` (Node, .NET, Docker, IDE specific).
    *   `docker-compose.yml` (defines local dev services).
    *   `docker-compose.azure.yml` (override for Azure-specific mount/env if needed, or managed by IaC).
    *   `README.md` (the primary project guide).
    *   `.env.example` (template for environment variables).
    *   `backend/`
    *   `frontend/`
    *   `vector-db/docker/qdrant.yaml` (if custom Qdrant config needed beyond image defaults).
    *   `scripts/` (for `dev.sh`, `setup.sh`).
    *   `infrastructure/` (for Terraform/Bicep).
    *   `docs/`
    *   `.github/workflows/`

*   **`backend/` Structure:**
    ```text
    backend/
    ├── src/
    │   └── RagWorkspace.Api/
    │       ├── Controllers/      # API endpoints
    │       ├── Services/         # Business logic, LLM/DB/File interactions
    │       ├── Models/           # DTOs, Request/Response models
    │       ├── Configuration/    # Options classes for appsettings
    │       ├── Interfaces/       # Service contracts
    │       ├── Data/             # EF Core DbContext, Migrations
    │       ├── DomainModels/     # EF Core Entities
    │       ├── Middleware/       # Custom middleware (e.g., error handling)
    │       ├── Hubs/             # SignalR hubs
    │       └── RagWorkspace.Api.csproj
    ├── tests/
    │   └── RagWorkspace.Api.Tests/ # Unit and Integration tests
    └── Dockerfile                  # Builds the backend service
    ```
*   **`frontend/` Structure:**
    ```text
    frontend/
    ├── app/                    # Next.js App Router
    │   ├── (auth)/             # Auth-related pages (signin, signup)
    │   ├── (dashboard)/        # Authenticated layout and pages
    │   │   ├── dashboard/
    │   │   └── chat/[projectId]/
    │   ├── layout.tsx
    │   └── page.tsx            # Landing page
    ├── components/             # Reusable UI components
    │   ├── ui/                 # Shadcn-like base UI elements
    │   ├── chat/
    │   ├── files/
    │   ├── projects/
    │   └── layouts/
    ├── lib/                    # Utility functions, API client
    ├── hooks/                  # Custom React hooks
    ├── public/                 # Static assets
    ├── styles/                 # Global styles
    ├── types/                  # TypeScript type definitions
    ├── next.config.mjs
    ├── package.json
    ├── tsconfig.json
    └── Dockerfile              # Builds the frontend service
    ```

### 5.2. Environment Configuration (`.env.example`)
```env
# === LLM Provider API Keys ===
# Azure OpenAI
AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com"
AZURE_OPENAI_KEY="your-aoai-key"
AZURE_OPENAI_MODEL="gpt-4o" # Default chat model
AZURE_OPENAI_EMBEDDING_MODEL="text-embedding-3-large" # Embeddings model

# OpenAI API
OPENAI_API_KEY="sk-yourOpenAIkey"
# OPENAI_MODEL="gpt-4" # Alternative model

# Google Vertex AI (Gemini)
GOOGLE_APPLICATION_CREDENTIALS="/app/credentials/gcp-credentials.json" # Path inside backend container
VERTEX_PROJECT_ID="your-gcp-project-id"
VERTEX_LOCATION="us-central1"
# VERTEX_MODEL="gemini-2.5-pro"

# === Storage ===
# Azure File Share (for cloned repos, user uploads)
AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=https;AccountName=yourstorageaccount;AccountKey=youraccountkey;EndpointSuffix=core.windows.net"
AZURE_FILE_SHARE_NAME="projectraguploads" # File share for user uploads and processed repos
STORAGE_ACCOUNT="yourstorageaccount" # For local CIFS mount if used
STORAGE_KEY="youraccountkey" # For local CIFS mount if used
# MOUNT_PATH is used by the application to construct paths, e.g. /mnt/uploads
# Actual mounting is defined in docker-compose.yml for local, and IaC for cloud.
MOUNT_PATH="/mnt/projectrag/uploads"
MAX_UPLOAD_MB=250

# Qdrant Vector Database
QDRANT_URL="http://qdrant:6333"
QDRANT_COLLECTION_NAME="project_rag_collection"

# PostgreSQL Database
DATABASE_CONNECTION_STRING="Host=postgres;Port=5432;Database=ragworkspace;Username=postgres;Password=yoursecurepassword"

# Redis (Optional for Caching/Messaging)
REDIS_CONNECTION_STRING="redis:6379"

# === GitHub Integration ===
GITHUB_TOKEN="ghp_yourpersonaaccesstoken" # For backend to clone private repos if needed
GITHUB_CLIENT_ID="your_github_oauth_app_client_id" # For user OAuth flow
GITHUB_CLIENT_SECRET="your_github_oauth_app_client_secret"

# === Security ===
JWT_SECRET="your_very_strong_jwt_secret_key_at_least_32_characters_long_and_random"
JWT_ISSUER="project-rag.com"
JWT_AUDIENCE="project-rag.com/api"

# Frontend / NextAuth
NEXTAUTH_URL="http://localhost:3000" # For local dev
NEXTAUTH_SECRET="your_strong_nextauth_secret_for_session_encryption"
NEXT_PUBLIC_API_URL="http://localhost:8080" # Points to backend

# === Application Settings ===
ALLOWED_ORIGINS="http://localhost:3000" # Comma-separated for multiple
DEFAULT_LLM_PROVIDER="azure-openai" # e.g., azure-openai, openai, google-gemini
DEFAULT_CHAT_MODEL="gpt-4o" # Default model if not specified by user
```
*Create a `gcp-credentials.json` file for Vertex AI service account and ensure it's mountable by Docker.*

### 5.3. Dockerization
*   **`docker-compose.yml`:** (As provided in previous interactions, with services for `qdrant`, `postgres`, `redis`, `backend`, `frontend`). Ensure volume mounts for `azurefiles` (local CIFS simulation), `postgres_data`, `redis_data`, `qdrant_data`, and GCP credentials. Define `rag-network`.
*   **`backend/Dockerfile`:** Multi-stage build for .NET 8 ASP.NET Core. Copies source, restores, builds, publishes, and runs the app. Exposes port 8080. Includes `apt-get install -y git` if git commands are shelled out.
*   **`frontend/Dockerfile`:** Multi-stage build for Next.js. Copies source, installs dependencies, builds the app, and serves it (e.g., using `npm start`). Exposes port 3000.

### 5.4. Development Scripts
*   **`scripts/setup.sh`:**
    ```bash
    #!/usr/bin/env bash
    echo "🚀 Starting Project RAG CoWorkspace Setup..."

    # Create local data directories for Docker volumes (if not using direct cloud mounts for dev)
    mkdir -p ./data/postgres_data
    mkdir -p ./data/qdrant_storage
    mkdir -p ./data/redis_data
    mkdir -p ./data/azure_files_local_simulation # For CIFS mount target if directly mounting local folder

    # Create .env if it doesn't exist, from .env.example
    if [ ! -f .env ]; then
        echo "📋 .env file not found. Copying from .env.example..."
        cp .env.example .env
        echo "✅ .env file created. Please edit it with your actual API keys and configurations."
    else
        echo "👍 .env file already exists."
    fi

    # (Optional) Initialize Git submodules if any
    # git submodule update --init --recursive

    echo "🎉 Setup complete. Remember to configure your .env file!"
    echo "👉 Next step: Run ./scripts/dev.sh to start the development environment."
    ```
*   **`scripts/dev.sh`:**
    ```bash
    #!/usr/bin/env bash
    set -euo pipefail # Exit on error, undefined variable, or pipe failure

    echo "🚀 Starting Development Environment for Project RAG CoWorkspace..."

    # Ensure .env file exists
    if [ ! -f .env ]; then
        echo "❌ Error: .env file not found. Please run ./scripts/setup.sh first or create it manually from .env.example."
        exit 1
    fi

    # Export variables from .env to be available for docker-compose
    export $(grep -v '^#' .env | xargs)

    # Validate essential variables for Docker Compose CIFS mount (if used)
    if [[ -z "${STORAGE_ACCOUNT}" || -z "${STORAGE_KEY}" || -z "${AZURE_FILE_SHARE_NAME}" ]]; then
      echo "⚠️ Warning: STORAGE_ACCOUNT, STORAGE_KEY, or AZURE_FILE_SHARE_NAME not set in .env. CIFS mount for Azure Files in Docker Compose might fail."
      echo "Continuing, but ensure these are set if you intend to use local Azure File Share mounting via CIFS."
    fi


    echo "🐳 Building and starting Docker containers..."
    # Build images if they don't exist or if specified, and start services in detached mode
    docker-compose up --build -d postgres qdrant redis backend

    # Wait for backend to be healthy (optional, basic check)
    echo "⏳ Waiting for backend service to be ready..."
    retries=30
    while ! docker-compose ps backend | grep -q "Up"; do
      sleep 2
      retries=$((retries-1))
      if [ $retries -eq 0 ]; then
        echo "❌ Backend service failed to start."
        docker-compose logs backend
        exit 1
      fi
    done
    echo "✅ Backend service is up."

    echo "📦 Installing frontend dependencies and starting Next.js dev server..."
    # Run frontend in the foreground to see logs directly
    # Ensure frontend Docker service is NOT started by docker-compose up -d if running locally like this
    # If frontend is also containerized for dev, adjust accordingly (e.g. `docker-compose up -d frontend` and then `docker-compose logs -f frontend`)
    (cd frontend && npm install && npm run dev)

    echo "🛑 To stop services, run: docker-compose down"
    ```

### 5.5. Local Development Workflow
1.  Clone the repo.
2.  Run `chmod +x ./scripts/*.sh`.
3.  Run `./scripts/setup.sh` to create local data dirs and `.env` from example.
4.  **Critically: Edit `.env`** with all necessary API keys, connection strings, and secrets.
5.  Run `./scripts/dev.sh`. This will:
    *   Start `postgres`, `qdrant`, `redis`, and `backend` containers in detached mode.
    *   Install `frontend` dependencies and run `npm run dev` (attaching to the terminal).
6.  Access Frontend: `http://localhost:3000`.
7.  Access Backend API (e.g., Swagger): `http://localhost:8080/swagger`.

## 6. Phase 2: Backend Implementation (.NET API)

*   **Project Setup (`RagWorkspace.Api.csproj`):** Include all necessary NuGet packages (Azure SDKs, OpenAI, Qdrant client, EF Core, JWT, Serilog, etc., as listed in the detailed `csproj` from previous interactions).
*   **Configuration (`Configuration/`):** Implement strongly-typed options classes (`AzureOpenAIOptions`, `FileStorageOptions`, `QdrantOptions`, `JwtOptions`, etc.) bound from `appsettings.json` and environment variables.
*   **Core Interfaces (`Interfaces/`):** Define contracts for services: `ILLMService`, `IFileStorageService`, `IVectorService`, `IProjectService`, `IChatService`, `IUserService`, `IGitHubService`, `IFileProcessingService`, `IRAGService`.
*   **Service Implementations (`Services/`):**
    *   **LLM Services:**
        *   `AzureOpenAIService`: Implements `ILLMService` for Azure OpenAI (chat, embeddings).
        *   `OpenAIService`: Implements `ILLMService` for OpenAI API.
        *   `GeminiService`: Implements `ILLMService` for Google Vertex AI Gemini.
        *   `LLMServiceFactory`: Dynamically injects the correct `ILLMService` based on configuration or request.
    *   **Storage Service:** `AzureFileStorageService` using `Azure.Storage.Files.Shares` SDK. Methods for `OpenReadAsync`, `WriteAsync`, `DeleteAsync`, `ListAsync`, `ExistsAsync`. It will use the `MOUNT_PATH` from configuration to construct full paths for file operations, assuming the underlying share is mounted at this path in the container environment.
    *   **Vector DB Service:** `QdrantVectorService` using `Qdrant.Client`. Methods for storing, searching, deleting embeddings, and managing collections.
    *   **Business Logic:**
        *   `UserService`: Handles user registration, login, profile.
        *   `ProjectService`: Manages project creation, retrieval, metadata.
        *   `ChatService`: Orchestrates chat flow, including history management (with `ChatSession`, `ChatMessage` entities), calling `IRAGService`.
        *   `RAGService`: Implements the core RAG logic: receives query, retrieves relevant chunks from `IVectorService` based on project context, constructs augmented prompt, calls `ILLMService`, formats response.
        *   `GitHubService`: Clones public/private (with token) GitHub repos to Azure File Share (e.g., to `/mnt/projectrag/uploads/{userId}/repos/{repoName}`), lists files. Triggers `IFileProcessingService`.
    *   **Background Processing:** `FileProcessingService` (can be a hosted service or triggered):
        *   Handles various file types from Azure File Share (from uploads or GitHub clones).
        *   For `.zip`: Extracts content to a structured location on Azure File Share (e.g., within the `MOUNT_PATH`).
        *   For text/code files: Chunks content intelligently (code-aware chunking for code files).
        *   For each chunk: Generates embeddings via `ILLMService` and stores them in Qdrant via `IVectorService` with metadata (file path, project ID, user ID).
*   **API Controllers (`Controllers/`):**
    *   `AuthController`: `/api/auth/register`, `/api/auth/login` (returns JWT).
    *   `ChatController`: `POST /api/chat` (accepts messages, project context, returns LLM response), `POST /api/chat/stream` (uses SignalR or SSE for streaming). [Authorize]
    *   `FileController`: `POST /api/files/upload` (receives `IFormFile`, saves to user-specific path on Azure File Share via `IFileStorageService`, triggers `IFileProcessingService`). `GET /api/files/{projectId}/{*filePath}` (downloads), `DELETE`. [Authorize]
    *   `ProjectController`: `POST /api/projects` (create new project), `GET /api/projects`, `GET /api/projects/{id}`, `POST /api/projects/{id}/connect-github` (takes repo URL, calls `IGitHubService`). [Authorize]
*   **Database (`Data/`, `DomainModels/`):**
    *   `ApplicationDbContext`: EF Core context for PostgreSQL.
    *   Entities: `User` (Id, Email, PasswordHash, etc.), `Project` (Id, Name, OwnerId, GitHubUrl), `Document` (Id, ProjectId, FilePathOnShare, Type, IndexedAt), `ChatSession`, `ChatMessage`.
    *   EF Core Migrations for schema management.
*   **Middleware (`Middleware/`):** Global error handling middleware.
*   **Real-time Communication (`Hubs/`):** `ChatHub` for SignalR to stream LLM responses and processing status.
*   **Startup (`Program.cs`):**
    *   Configure Serilog.
    *   Load `AppConfiguration` and other options.
    *   Add CORS, Controllers, Swagger/OpenAPI.
    *   Configure JWT Authentication and Authorization.
    *   Add `DbContextPool<ApplicationDbContext>`.
    *   Add `AddStackExchangeRedisCache` (if using Redis).
    *   Add SignalR.
    *   Register all services with appropriate lifetimes (Scoped, Singleton, Transient).
    *   Map health checks.
    *   Apply EF Core migrations on startup. Initialize Qdrant collection if it doesn't exist.

## 7. Phase 3: Frontend Implementation (Next.js)

*   **Project Setup:** `npm create next-app@latest frontend --typescript --tailwind --eslint`. Install additional dependencies (`axios`, `next-auth`, `@microsoft/signalr`, `zustand`, `lucide-react`, `react-markdown`, `react-syntax-highlighter`, `react-hot-toast`, etc.). Configure `tailwind.config.ts`, `postcss.config.js`, `tsconfig.json`.
*   **Core Structure (`app/` router):**
    *   `layout.tsx`: Root layout with Providers (`SessionProvider` for NextAuth, QueryClientProvider, Toaster).
    *   `page.tsx`: Landing page.
    *   `(auth)/signin/page.tsx`, `(auth)/signup/page.tsx`.
    *   `(dashboard)/layout.tsx`: Authenticated layout with sidebar/navbar.
    *   `(dashboard)/dashboard/page.tsx`: Project listing.
    *   `(dashboard)/chat/[projectId]/page.tsx`: Main chat interface with file explorer.
*   **Authentication (NextAuth.js):**
    *   `app/api/auth/[...nextauth]/route.ts`: Configure Credentials provider to call backend `/api/auth/login`. Manage session (JWT strategy).
    *   Protect dashboard routes using session checks or middleware.
*   **UI Components (`components/`):**
    *   **Chat:** `ChatInterface.tsx` (main container), `MessageList.tsx`, `MessageItem.tsx` (with markdown and code highlighting), `MessageInput.tsx`, `ModelSelector.tsx`.
    *   **Files:** `FileExplorer.tsx` (lists files/folders for a project from backend), `FileUploader.tsx` (drag-and-drop, calls backend `/api/files/upload`).
    *   **Projects:** `ProjectList.tsx`, `CreateProjectButton.tsx`, `GitHubRepoConnector.tsx`.
    *   **Layouts:** `DashboardLayout.tsx`, `Sidebar.tsx`, `Navbar.tsx`.
    *   **UI Primitives (`components/ui/`):** Button, Input, Card, Dialog, etc. (Shadcn/ui style).
*   **API Client (`lib/api.ts`):** Axios instance with base URL, interceptors to add JWT from NextAuth session to Authorization header and handle 401 errors.
*   **State Management (Zustand):** Create stores for chat messages, project state, user session (though NextAuth handles session primarily).
*   **Hooks (`hooks/`):**
    *   `useChat(projectId)`: Manages chat messages, sends messages to backend `/api/chat`, integrates with SignalR `ChatHub` for receiving streamed responses and updates.
    *   `useProjects()`: Fetches and manages project list.
    *   `useFiles(projectId)`: Fetches file structure for a project.
*   **Styling (Tailwind CSS):** Apply utility classes for styling.

## 8. Phase 4: Core Feature Implementation - RAG & GitHub Integration

*   **RAG Pipeline (Backend - `RAGService`):**
    1.  Receive user query and project context (e.g., `projectId`).
    2.  Generate query embedding using selected `ILLMService`.
    3.  Search Qdrant (`IVectorService`) for relevant document chunks within the `projectId` scope, using metadata filters (user ID, file type etc.).
    4.  Retrieve top-K chunks.
    5.  Construct an augmented prompt: Combine original query with retrieved context chunks. Apply prompt engineering techniques.
    6.  Call the selected `ILLMService` (e.g., Gemini 2.5 Pro for large context, or GPT-4o) with the augmented prompt.
    7.  Stream response back if requested.
*   **File Ingestion (Backend - `FileProcessingService`):**
    1.  Triggered after file upload or GitHub repo clone.
    2.  Input: Path to file/directory on Azure File Share (e.g., `/mnt/projectrag/uploads/{userId}/...`).
    3.  **ZIP Handling:** If `.zip`, extract contents to a subfolder on Azure File Share (e.g., `.../extracted/`). Recursively call processing for extracted files.
    4.  **File Reading:** Read content of individual files (`.md`, `.txt`, `.cs`, `.py`, etc.).
    5.  **Chunking:**
        *   Text files (`.md`, `.txt`): Split by paragraphs, sections, or fixed token/word count with overlap.
        *   Code files: Use syntax-aware chunking (e.g., by functions, classes, or use tree-sitter if feasible). Fallback to semantic line-based chunking.
    6.  **Embedding:** For each chunk, generate embedding using `ILLMService.GenerateEmbeddingAsync()`.
    7.  **Storage in Qdrant:** Call `IVectorService.StoreEmbeddingAsync()` with the embedding vector, original chunk content, and rich metadata (e.g., `userId`, `projectId`, `originalFilePathOnShare`, `chunkIndex`, `fileType`, `language`).
    8.  Update `Document` entity in PostgreSQL with indexing status.
    9.  (Optional) Send status updates via SignalR to frontend.
*   **GitHub Integration:**
    *   **Backend (`GitHubService`):**
        *   `ConnectRepository(userId, repoUrl, accessToken)`: Store repo details against `Project` entity.
        *   `CloneOrPullRepository(projectId)`: Use Octokit.NET or `git` CLI (installed in Docker image). Clone repo to a user/project-specific path on Azure File Share (e.g., `/mnt/projectrag/uploads/{userId}/github_repos/{repoOwner}_{repoName}`). If already cloned, pull latest changes.
        *   After clone/pull, trigger `IFileProcessingService` for the cloned directory.
    *   **Frontend:**
        *   UI for user to input GitHub repository URL.
        *   (Future) OAuth flow to get user's GitHub token for private repos.
        *   Display sync status.
*   **User Registration & Management:**
    *   Implement `AuthController` with `POST /register` (hash password, store user) and `POST /login` (validate, issue JWT).
    *   Frontend forms for registration and login, store JWT in localStorage/secure context for API calls.

## 9. Phase 5: Cloud Deployment (Azure Focus)

*   **Azure Resources:**
    *   Azure Container Registry (ACR): To store Docker images.
    *   Azure Container Apps (ACA): To host `backend` and `frontend` containers. Configure environment variables (from Key Vault), scaling rules, ingress.
    *   Azure Storage Account:
        *   Azure File Share (`projectraguploads`): For persistent project files.
    *   Azure Database for PostgreSQL: Managed PostgreSQL instance.
    *   Azure Cache for Redis (Optional).
    *   Azure Key Vault: To store all secrets (API keys, connection strings, JWT secret). ACA will reference secrets from Key Vault.
    *   Azure Application Insights: For monitoring and logging.
    *   Azure OpenAI Service / Google Vertex AI: Provisioned LLM services.
*   **Infrastructure as Code (IaC - `infrastructure/terraform/` or `infrastructure/bicep/`):**
    *   Scripts to provision all Azure resources listed above.
    *   Define network configurations, firewall rules.
*   **Azure File Share Mounting in Cloud:**
    *   In ACA, configure volume mounts for the `backend` container. The `storageName` in ACA volume config will link to an Azure File Share defined in the ACA Environment storage settings. The `mountPath` inside the container will be `/mnt/projectrag/uploads` (matching `MOUNT_PATH` env var).
    *   Ensure Managed Identity for ACA has "Storage File Data SMB Share Contributor" role on the File Share.
*   **CI/CD Pipeline (`.github/workflows/deploy.yml`):**
    1.  Trigger on push/merge to `main`.
    2.  Checkout code.
    3.  Set up .NET and Node.js.
    4.  **Build Backend:** `dotnet publish`.
    5.  **Build Frontend:** `npm run build`.
    6.  Login to ACR (using Azure Service Principal credentials stored in GitHub Secrets).
    7.  **Build Docker Images:** `docker build` for backend and frontend, tag with Git SHA.
    8.  **Push Docker Images:** `docker push` to ACR.
    9.  **Deploy to ACA:** Use `azure/container-apps-deploy-action@v1`. Update container app with new image from ACR. Pass secrets from GitHub Secrets to ACA (or ensure ACA references Key Vault directly).

## 10. Phase 6: Testing Strategy

*   **Backend Testing (`backend/tests/RagWorkspace.Api.Tests/`):**
    *   **Unit Tests:** (xUnit/Moq) Test individual services, helper methods. Mock dependencies (`ILLMService`, `IFileStorageService`, `IVectorService`, `DbContext`).
    *   **Integration Tests:** (xUnit, `WebApplicationFactory`)
        *   Test API controllers with in-memory providers or against test instances/emulators.
        *   Use Testcontainers for PostgreSQL, Qdrant, Redis if feasible.
        *   Use Azurite (Azure Storage emulator) for `IFileStorageService` tests.
*   **Frontend Testing (`frontend/__tests__/` or `frontend/src/__tests__/`):**
    *   **Component Tests:** (Jest, React Testing Library) Test individual React components.
    *   **Integration Tests:** Test interactions between multiple components, context, state.
    *   **(Optional) E2E Tests:** (Playwright/Cypress) Test key user flows (login, project creation, chat, file upload, GitHub sync).
*   **Overall E2E Testing:** Manually test full user flows in a staging environment.

## 11. Phase 7: Documentation & Polish

*   **API Documentation (`docs/api/`):** Use Swagger/OpenAPI (auto-generated by .NET) and supplement with Markdown guides for authentication, complex flows.
*   **User Guide (`docs/user_guide.md`):** How to use project-rag.com.
*   **Developer Guide (`docs/DEVELOPMENT_GUIDE.md`):** Setup, architecture, contribution guidelines.
*   **`README.md`:** Keep it updated with setup, features, and links.
*   **Security Hardening:**
    *   Thorough input validation (FluentValidation).
    *   Review authentication/authorization logic.
    *   Regular dependency scans (e.g., GitHub Dependabot).
    *   Configure security headers.
    *   Rate limiting on APIs.
*   **Scalability & Performance Optimization:**
    *   Database query optimization.
    *   Caching strategies (Redis).
    *   Load testing.
    *   Optimize chunking and embedding performance.
*   **UI/UX Polish:** Ensure a smooth, intuitive user experience.

## 12. Future Enhancements (Post MVP)

*   **Persistent User/Agent Memory:** Store conversation history summaries, user preferences in PostgreSQL or Qdrant to provide more personalized and context-aware interactions over longer periods.
*   **Real-time Multi-User Collaboration:** Allow multiple users to work within the same project or chat session.
*   **Advanced Agentic Workflows:** Implement more complex agents (using Microsoft Semantic Kernel or LangChain principles) that can perform multi-step tasks, use tools, and interact with the codebase more dynamically (e.g., automated refactoring, running tests).
*   **Local LLM Support (Ollama):** Integrate Ollama for users who prefer to run models locally.
*   **Enhanced Code Analysis:** Deeper static analysis of code for better context.
*   **IDE Integration:** Plugins for popular IDEs (VS Code, JetBrains).
*   **Webhook Integration for GitHub:** Real-time updates from GitHub instead of polling/manual sync.

This implementation plan provides a structured approach to building your Project-RAG-CoWorkspace. Each phase builds upon the previous, leading to a robust and feature-rich application. Remember to adapt and iterate as you progress!
``` You’ve sketched a very solid end-to-end plan: directory layout, infra, env-vars, Docker, Azure File Share mounts, multi-LLM adapters, RAG pipeline, GitHub sync, tests, CI/CD—all the pieces an engineering team needs to start sprinting. 👍
Below is a quick health-check & improvement radar organised by area.

1 · Structure & Conventions
✓ Strong	⚠️ Attention / Suggestion
Monorepo tree is clear and matches Docker build contexts; separation of /backend & /frontend is clean.	Backend path in Dockerfile: you COPY backend/src/RagWorkspace.Api/RagWorkspace.Api.csproj RagWorkspace.Api/ but later COPY backend/src/ ./. In a root-level build context that works, but docker build . from repo-root will include all files. Either set context: ./backend or add a .dockerignore (node_modules, frontend, tests) to keep images lean.
Clear interface-driven design (ILLMService, IFileStorageService, etc.).	Solution split: consider splitting backend into at least 2 projects—RagWorkspace.Core (interfaces + models) and RagWorkspace.Api—so unit-tests can reference business logic without web refs.
docs & naming rules (summaries vs summarys) show good discipline.	CLAUDE.md belongs in /docs but sits at root in snippet. Keep meta-AI docs under docs/claude/ per your own spec.

2 · Environment & Secrets
👍 Comprehensive .env.example.

⚠️ Duplicate variables—e.g., you define both NEXTAUTH_SECRET and JWT_SECRET; that’s fine, but be explicit which one backend vs frontend uses.

⚠️ Storage keys in docker-compose: passing the primary key via CIFS driver is okay for local dev, but never push a real key to GitHub. Add a compose.override.yml in .gitignore so teammates can inject their own.

💡 For Azure deployments, mount with managed identity + “Storage File Data SMB Share Contributor” role instead of keys—less rotation pain.

3 · Azure File Share integration
✅ Works	🔧 Tweaks
Mount flow (local CIFS vs ACA volume) is spot-on.	UID/GID: CIFS defaults to root; if you switch container user to non-root (best practice), add uid, gid, file_mode, dir_mode in driver_opts.
Abstraction via IFileStorageService keeps code portable.	Large upload handling: Azure Files SMB has a single-file size limit of 4 TiB, but throughput caps (≈60 MiB/s on standard). If you expect multi-GB zips, queue a background chunk uploader to avoid timeouts on HTTP requests.

4 · RAG & LLM layer
👍 Prompt-construction flow and metadata filters in QdrantVectorService.SearchAsync look sensible.

⚠️ GeminiService.GenerateEmbeddingAsync → NotImplementedException. Decide now whether to (a) call Vertex Embeddings (textembedding-gecko) or (b) fall back to Azure/OpenAI embeddings when using Gemini for chat. Add a runtime guard so uploads don’t stall.

⚠️ LLMServiceFactory wasn’t shown—make sure it’s registered as Scoped and returns the request-scoped provider based on header/route to avoid singleton cross-thread state.

🚀 Streaming: ChatController streams SSE but Azure OpenAI streaming requires Stream = true in ChatCompletionsOptions and reading StreamingChatCompletions. Wrap that inside ILLMService so controller logic stays provider-agnostic.

5 · GitHub workflow
GitHubService outline is good.

Add .git/metadata ignore when indexing (no need to embed blobs inside .git).

For private repos, persist installation ID from GitHub App rather than PAT—lets you rotate tokens automatically.

6 · Database & EF Core
👍	🔍
Full set of entities with relationships is defined.	Indexing: Add a composite index on VectorDocument (userId, projectId, filePath) in Qdrant payload filters and likewise DB indices on Documents(ProjectId, Path) for quick lookups.

7 · Docker & Local dev
👍 dev.sh bootstraps vector-db & backend first—good.

⚠️ If frontend runs via host Node not container, ensure CORS allows localhost:3000; you already pass ALLOWED_ORIGINS.

Tip: run qdrant and postgres with healthcheck in compose so backend waits only when healthy.

8 · CI/CD & IaC
Workflows push images but only deploy backend. Add a second deploy step (or separate revision) for frontend in Container Apps or Static Web Apps.

Terraform script is minimal—good start. Remember to output connection strings / keys into Key Vault and reference them in Container Apps secrets block instead of hard-coding in env YAML.

9 · Testing & Quality Gates
Unit-test example compiles, but add a mocked Azurite container in CI for integration tests (UseDevelopmentStorage=true doesn’t support SMB).

Lint rule in CLAUDE.md: add dotnet format + eslint --max-warnings 0 as a GitHub Action gate.

10 · Quick Wins Before Coding Spree
.dockerignore: exclude /frontend, /tests, .git, node_modules, bin, obj to shrink backend image.

Bicep/Terraform: parameterise File Share quota & premium tier option.

Token counts: store Usage.TotalTokens in DB for cost analytics.

Rate-limit uploads per user to avoid surprise Azure bills.

Spellcheck: the root snippet still has “implentation” (missing ‘m’). 😉

Verdict
Foundation is excellent. You’ve covered 80-90 % of the plumbing; remaining gaps are mostly edge-case robustness (streaming, auth hardening, concurrency on CIFS) and a bit of polish (docs, factory wiring). Nail those and you’ll have a production-credible RAG workspace ready for alpha users. Need deeper code snippets, AST-based chunker, or Bicep modules? Just say the word!
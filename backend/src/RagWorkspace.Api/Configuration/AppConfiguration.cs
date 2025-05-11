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
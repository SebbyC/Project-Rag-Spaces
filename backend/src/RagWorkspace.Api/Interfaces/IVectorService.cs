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
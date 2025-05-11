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
                    Vectors = new Vectors { Vector = new Vector { Data = { document.Vector } } },
                    Payload = document.Metadata.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new Value { StringValue = kvp.Value }
                    )
                }
            };

            // Add content to payload as well
            points[0].Payload.Add("content", new Value { StringValue = document.Content });

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
                new Vector { Data = { queryVector } },
                limit: (ulong)limit,
                filter: qdrantFilter,
                withPayload: true);

            return results.Select(r => new VectorSearchResult
            {
                Id = r.Id.Uuid,
                Score = r.Score,
                Metadata = r.Payload
                    .Where(kvp => kvp.Key != "content")
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.StringValue),
                Content = r.Payload.TryGetValue("content", out var content) 
                    ? content.StringValue 
                    : string.Empty
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
                new PointsSelector { Points = { new PointId { Uuid = id } } });
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
            
            // Create optimized index for fast search by userId and projectId
            await _client.CreatePayloadIndexAsync(
                collectionName,
                "userId",
                new PayloadIndexParams { 
                    DataType = PayloadSchemaType.Keyword,
                    FilterableAndIndexed = true
                });
            
            await _client.CreatePayloadIndexAsync(
                collectionName,
                "projectId",
                new PayloadIndexParams { 
                    DataType = PayloadSchemaType.Keyword,
                    FilterableAndIndexed = true
                });
            
            await _client.CreatePayloadIndexAsync(
                collectionName,
                "filePath",
                new PayloadIndexParams { 
                    DataType = PayloadSchemaType.Keyword,
                    FilterableAndIndexed = true
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating collection {CollectionName}", collectionName);
            throw;
        }
    }
}
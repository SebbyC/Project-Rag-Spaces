namespace RagWorkspace.Api.Interfaces;

public interface IRAGService
{
    Task<(string augmentedPrompt, List<VectorSearchResult> context)> GetAugmentedPromptAsync(string query, string projectId, string userId, int maxResults = 5);
    Task<LLMResponse> GenerateRagResponseAsync(LLMRequest request, string projectId, string userId);
    IAsyncEnumerable<string> StreamRagResponseAsync(LLMRequest request, string projectId, string userId);
}
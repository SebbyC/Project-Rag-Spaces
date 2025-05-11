using System.Text;
using RagWorkspace.Api.Interfaces;

namespace RagWorkspace.Api.Services;

public class RAGService : IRAGService
{
    private readonly IVectorService _vectorService;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly LLMServiceFactory _llmServiceFactory;
    private readonly ITokenBudgetResolver _tokenBudgetResolver;
    private readonly ILogger<RAGService> _logger;

    // System prompts for different contexts
    private const string BASE_SYSTEM_PROMPT = 
        "You are an AI assistant helping with a codebase. Use the provided context to answer the question. " +
        "If you don't know the answer or can't find relevant information in the context, say so clearly " +
        "rather than making up information. Cite specific files and line numbers when referencing code.";
        
    private const string CODE_SYSTEM_PROMPT = 
        "You are an AI assistant helping with a codebase. Use the provided context to answer the question. " +
        "When explaining code, focus on its purpose and how it works. " +
        "If asked to implement something, provide idiomatic code following the project's conventions. " +
        "Cite specific files and line numbers when referencing code from the context. " +
        "If you don't know the answer or can't find relevant information in the context, say so clearly.";
        
    private const string CONTEXT_FORMAT = 
        "Context from {0} (relevance score: {1:F2}):\n```{2}\n{3}\n```\n\n";

    public RAGService(
        IVectorService vectorService,
        IEmbeddingProvider embeddingProvider,
        LLMServiceFactory llmServiceFactory,
        ITokenBudgetResolver tokenBudgetResolver,
        ILogger<RAGService> logger)
    {
        _vectorService = vectorService;
        _embeddingProvider = embeddingProvider;
        _llmServiceFactory = llmServiceFactory;
        _tokenBudgetResolver = tokenBudgetResolver;
        _logger = logger;
    }

    public async Task<(string augmentedPrompt, List<VectorSearchResult> context)> GetAugmentedPromptAsync(
        string query, string projectId, string userId, int maxResults = 5)
    {
        _logger.LogInformation("Generating augmented prompt for query: {Query}", query);
        
        try
        {
            // Generate embedding for the query
            float[] queryEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(query);
            
            if (queryEmbedding.Length == 0)
            {
                _logger.LogWarning("Failed to generate embedding for query: {Query}", query);
                return (query, new List<VectorSearchResult>());
            }
            
            // Create filters for this user's project
            var filters = new Dictionary<string, string>
            {
                { "userId", userId },
                { "projectId", projectId }
            };
            
            // Search for relevant context
            var searchResults = await _vectorService.SearchAsync(
                queryEmbedding,
                limit: Math.Max(5, maxResults * 2), // Get more results than needed to allow filtering
                filters: filters
            );
            
            List<VectorSearchResult> relevantResults = searchResults.ToList();
            
            // If no results found, return the original query
            if (!relevantResults.Any())
            {
                _logger.LogInformation("No relevant context found for query: {Query}", query);
                return (FormatPromptWithoutContext(query), new List<VectorSearchResult>());
            }
            
            // Sort by relevance score and take top N
            relevantResults = relevantResults
                .OrderByDescending(r => r.Score)
                .Take(maxResults)
                .ToList();
                
            _logger.LogInformation("Found {ResultCount} relevant context chunks for query", relevantResults.Count);
            
            // Construct the augmented prompt with context
            string augmentedPrompt = FormatPromptWithContext(query, relevantResults);
            
            return (augmentedPrompt, relevantResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating augmented prompt for query: {Query}", query);
            return (FormatPromptWithoutContext(query), new List<VectorSearchResult>());
        }
    }

    private string FormatPromptWithContext(string query, List<VectorSearchResult> contextResults)
    {
        // Select appropriate system prompt based on query content
        string systemPrompt = SelectSystemPrompt(query);
        
        // Build the context section
        StringBuilder contextBuilder = new StringBuilder();
        
        foreach (var result in contextResults)
        {
            string fileExtension = result.Metadata.TryGetValue("fileType", out var fileType) ? fileType : "";
            string filePath = result.Metadata.TryGetValue("filePath", out var path) ? path : "unknown";
            
            contextBuilder.AppendFormat(
                CONTEXT_FORMAT,
                filePath,
                result.Score,
                DetermineLanguage(fileExtension),
                result.Content
            );
        }
        
        // Construct the final prompt
        return $"{systemPrompt}\n\n" +
               $"CONTEXT BEGIN\n{contextBuilder}\nCONTEXT END\n\n" +
               $"USER QUESTION: {query}\n\n" +
               $"Provide a clear and helpful response. Remember to reference specific files and line numbers from the context when relevant.";
    }

    private string FormatPromptWithoutContext(string query)
    {
        // When no context is available, use a simpler prompt
        return $"{BASE_SYSTEM_PROMPT}\n\n" +
               $"Note: I couldn't find any specific context in the codebase related to your query.\n\n" +
               $"USER QUESTION: {query}\n\n" +
               $"Provide the best response you can with general knowledge, but make it clear when you're not referencing project-specific information.";
    }

    private string SelectSystemPrompt(string query)
    {
        // Choose appropriate system prompt based on query type
        if (query.Contains("implement") || query.Contains("create") || query.Contains("write code") ||
            query.Contains("function") || query.Contains("class") || query.Contains("method"))
        {
            return CODE_SYSTEM_PROMPT;
        }
        
        return BASE_SYSTEM_PROMPT;
    }

    private string DetermineLanguage(string fileExtension)
    {
        return fileExtension.ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".jsx" => "jsx",
            ".tsx" => "tsx",
            ".py" => "python",
            ".java" => "java",
            ".go" => "go",
            ".rs" => "rust",
            ".cpp" or ".cc" => "cpp",
            ".c" => "c",
            ".h" or ".hpp" => "cpp",
            ".html" => "html",
            ".css" => "css",
            ".scss" or ".sass" => "scss",
            ".md" => "markdown",
            ".json" => "json",
            ".xml" => "xml",
            ".yaml" or ".yml" => "yaml",
            _ => ""
        };
    }

    public async Task<LLMResponse> GenerateRagResponseAsync(LLMRequest request, string projectId, string userId)
    {
        _logger.LogInformation("Generating RAG response for project {ProjectId}", projectId);
        
        try
        {
            // Extract the user's query from the last message in the request
            string query = request.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";
            
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogWarning("No user query found in request messages");
                return new LLMResponse { Content = "I couldn't understand your question. Could you please try again?" };
            }
            
            // Get augmented prompt with relevant context
            var (augmentedPrompt, context) = await GetAugmentedPromptAsync(query, projectId, userId);
            
            // Create a new request with the augmented prompt
            var newRequest = new LLMRequest
            {
                Model = request.Model,
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
                Stream = false
            };
            
            // Add all messages except the last user message
            foreach (var message in request.Messages.Where(m => m.Role != "user" || m != request.Messages.Last(msg => msg.Role == "user")))
            {
                newRequest.Messages.Add(message);
            }
            
            // Add the augmented user message
            newRequest.Messages.Add(new ChatMessage
            {
                Role = "user",
                Content = augmentedPrompt
            });
            
            // Get the LLM service and generate the response
            var llm = _llmServiceFactory.ResolveChatProvider(request.Model);
            var response = await llm.GenerateCompletionAsync(newRequest);
            
            _logger.LogInformation("Generated RAG response with {ContextCount} context chunks", context.Count);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating RAG response");
            return new LLMResponse { Content = "I encountered an error while processing your request. Please try again." };
        }
    }

    public async IAsyncEnumerable<string> StreamRagResponseAsync(LLMRequest request, string projectId, string userId)
    {
        try
        {
            // Extract the user's query from the last message in the request
            string query = request.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";
            
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogWarning("No user query found in request messages for streaming");
                yield return "I couldn't understand your question. Could you please try again?";
                yield break;
            }
            
            // Get augmented prompt with relevant context
            var (augmentedPrompt, context) = await GetAugmentedPromptAsync(query, projectId, userId);
            
            // Create a new request with the augmented prompt
            var newRequest = new LLMRequest
            {
                Model = request.Model,
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
                Stream = true // Ensure streaming is enabled
            };
            
            // Add all messages except the last user message
            foreach (var message in request.Messages.Where(m => m.Role != "user" || m != request.Messages.Last(msg => msg.Role == "user")))
            {
                newRequest.Messages.Add(message);
            }
            
            // Add the augmented user message
            newRequest.Messages.Add(new ChatMessage
            {
                Role = "user",
                Content = augmentedPrompt
            });
            
            // Get the LLM service and stream the response
            var llm = _llmServiceFactory.ResolveChatProvider(request.Model);
            
            // Stream the response chunks
            await foreach (var chunk in llm.GenerateCompletionStreamAsync(newRequest))
            {
                yield return chunk;
            }
            
            _logger.LogInformation("Completed streaming RAG response with {ContextCount} context chunks", context.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming RAG response");
            yield return "I encountered an error while processing your request. Please try again.";
        }
    }
}
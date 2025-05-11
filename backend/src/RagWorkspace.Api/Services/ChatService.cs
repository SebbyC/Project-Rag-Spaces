using Microsoft.EntityFrameworkCore;
using RagWorkspace.Api.Interfaces;
using RagWorkspace.Api.Models;

namespace RagWorkspace.Api.Services;

public class ChatService : IChatService
{
    private readonly IRAGService _ragService;
    private readonly LLMServiceFactory _llmServiceFactory;
    private readonly ITokenBudgetResolver _tokenBudgetResolver;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IRAGService ragService,
        LLMServiceFactory llmServiceFactory,
        ITokenBudgetResolver tokenBudgetResolver,
        ApplicationDbContext dbContext,
        ILogger<ChatService> logger)
    {
        _ragService = ragService;
        _llmServiceFactory = llmServiceFactory;
        _tokenBudgetResolver = tokenBudgetResolver;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ChatMessageResponse> ProcessChatAsync(string userId, ChatRequest request)
    {
        _logger.LogInformation("Processing chat request for user {UserId}, project {ProjectId}", userId, request.ProjectId);
        
        try
        {
            // Get or create a chat session
            var session = await GetOrCreateSessionAsync(userId, request.ProjectId, request.SessionId);
            
            // Create and save the user message
            var userMessage = new ChatMessage
            {
                SessionId = session.Id,
                Role = "user",
                Content = request.Content,
                CreatedAt = DateTime.UtcNow
            };
            
            _dbContext.ChatMessages.Add(userMessage);
            await _dbContext.SaveChangesAsync();
            
            // Prepare the LLM request with conversation history
            var conversationHistory = await GetRecentMessageHistoryAsync(session.Id, request.Model);
            var llmRequest = new LLMRequest
            {
                Model = request.Model,
                Messages = conversationHistory,
                Stream = false
            };
            
            // Generate the response (with or without RAG)
            LLMResponse llmResponse;
            if (request.UseRag)
            {
                llmResponse = await _ragService.GenerateRagResponseAsync(llmRequest, request.ProjectId, userId);
            }
            else
            {
                var llm = _llmServiceFactory.ResolveChatProvider(request.Model);
                llmResponse = await llm.GenerateCompletionAsync(llmRequest);
            }
            
            // Create and save the assistant message
            var assistantMessage = new ChatMessage
            {
                SessionId = session.Id,
                Role = "assistant",
                Content = llmResponse.Content,
                CreatedAt = DateTime.UtcNow
            };
            
            _dbContext.ChatMessages.Add(assistantMessage);
            
            // Update session's last activity
            session.UpdatedAt = DateTime.UtcNow;
            if (string.IsNullOrEmpty(session.Title) && !string.IsNullOrEmpty(request.Content))
            {
                // Generate a title from the first user message
                session.Title = GenerateSessionTitle(request.Content);
            }
            
            await _dbContext.SaveChangesAsync();
            
            // Return response
            return new ChatMessageResponse
            {
                Id = assistantMessage.Id,
                Role = assistantMessage.Role,
                Content = assistantMessage.Content,
                CreatedAt = assistantMessage.CreatedAt,
                Model = llmResponse.Model,
                Provider = llmResponse.Provider
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            throw;
        }
    }

    public async IAsyncEnumerable<string> StreamChatAsync(string userId, ChatRequest request)
    {
        _logger.LogInformation("Streaming chat response for user {UserId}, project {ProjectId}", userId, request.ProjectId);
        
        try
        {
            // Get or create a chat session
            var session = await GetOrCreateSessionAsync(userId, request.ProjectId, request.SessionId);
            
            // Create and save the user message
            var userMessage = new ChatMessage
            {
                SessionId = session.Id,
                Role = "user",
                Content = request.Content,
                CreatedAt = DateTime.UtcNow
            };
            
            _dbContext.ChatMessages.Add(userMessage);
            await _dbContext.SaveChangesAsync();
            
            // Prepare the LLM request with conversation history
            var conversationHistory = await GetRecentMessageHistoryAsync(session.Id, request.Model);
            var llmRequest = new LLMRequest
            {
                Model = request.Model,
                Messages = conversationHistory,
                Stream = true
            };
            
            // Create a StringBuilder to accumulate assistant response
            var responseBuilder = new StringBuilder();
            
            // Stream the response
            if (request.UseRag)
            {
                // Use RAG for generating the response
                await foreach (var chunk in _ragService.StreamRagResponseAsync(llmRequest, request.ProjectId, userId))
                {
                    responseBuilder.Append(chunk);
                    yield return chunk;
                }
            }
            else
            {
                // Use the LLM service directly
                var llm = _llmServiceFactory.ResolveChatProvider(request.Model);
                await foreach (var chunk in llm.GenerateCompletionStreamAsync(llmRequest))
                {
                    responseBuilder.Append(chunk);
                    yield return chunk;
                }
            }
            
            // After streaming completes, save the assistant message
            var assistantMessage = new ChatMessage
            {
                SessionId = session.Id,
                Role = "assistant",
                Content = responseBuilder.ToString(),
                CreatedAt = DateTime.UtcNow
            };
            
            _dbContext.ChatMessages.Add(assistantMessage);
            
            // Update session's last activity
            session.UpdatedAt = DateTime.UtcNow;
            if (string.IsNullOrEmpty(session.Title) && !string.IsNullOrEmpty(request.Content))
            {
                // Generate a title from the first user message
                session.Title = GenerateSessionTitle(request.Content);
            }
            
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming chat response");
            yield return $"Error: {ex.Message}";
        }
    }

    public async Task<List<ChatMessageResponse>> GetChatHistoryAsync(string userId, string sessionId)
    {
        _logger.LogInformation("Getting chat history for session {SessionId}", sessionId);
        
        try
        {
            // Verify the user has access to this session
            var session = await _dbContext.ChatSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);
                
            if (session == null)
            {
                _logger.LogWarning("Session {SessionId} not found for user {UserId}", sessionId, userId);
                return new List<ChatMessageResponse>();
            }
            
            // Get all messages for this session, ordered by creation time
            var messages = await _dbContext.ChatMessages
                .Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
                
            // Map to response objects
            var responses = messages.Select(m => new ChatMessageResponse
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreatedAt
            }).ToList();
            
            return responses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat history for session {SessionId}", sessionId);
            throw;
        }
    }

    private async Task<ChatSession> GetOrCreateSessionAsync(string userId, string projectId, string? sessionId)
    {
        ChatSession? session = null;
        
        // If sessionId is provided, try to find it
        if (!string.IsNullOrEmpty(sessionId))
        {
            session = await _dbContext.ChatSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);
                
            if (session != null)
            {
                return session;
            }
        }
        
        // Create a new session
        session = new ChatSession
        {
            UserId = userId,
            ProjectId = projectId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        _dbContext.ChatSessions.Add(session);
        await _dbContext.SaveChangesAsync();
        
        return session;
    }

    private async Task<List<ChatMessage>> GetRecentMessageHistoryAsync(string sessionId, string? modelName = null)
    {
        // Get all messages for this session, ordered by creation time
        var allMessages = await _dbContext.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
            
        if (!allMessages.Any())
        {
            return new List<ChatMessage>();
        }
        
        // If under token budget, return all messages
        int tokenBudget = _tokenBudgetResolver.GetConversationSummaryCutoff(modelName ?? "default");
        int estimatedTokenCount = allMessages.Sum(m => _tokenBudgetResolver.EstimateTokenCount(m.Content));
        
        if (estimatedTokenCount <= tokenBudget)
        {
            return allMessages;
        }
        
        // Otherwise, select most recent messages that fit within budget
        List<ChatMessage> recentMessages = new List<ChatMessage>();
        int runningTokenCount = 0;
        
        // Always include the system message if present
        var systemMessage = allMessages.FirstOrDefault(m => m.Role == "system");
        if (systemMessage != null)
        {
            recentMessages.Add(systemMessage);
            runningTokenCount += _tokenBudgetResolver.EstimateTokenCount(systemMessage.Content);
        }
        
        // Add messages from the end until we hit the budget
        foreach (var message in allMessages.Where(m => m.Role != "system").Reverse())
        {
            int messageTokens = _tokenBudgetResolver.EstimateTokenCount(message.Content);
            
            if (runningTokenCount + messageTokens <= tokenBudget)
            {
                recentMessages.Insert(systemMessage != null ? 1 : 0, message);
                runningTokenCount += messageTokens;
            }
            else
            {
                break;
            }
        }
        
        return recentMessages;
    }

    private string GenerateSessionTitle(string firstMessage)
    {
        // Simple title generation - take first few words
        int titleLength = Math.Min(50, firstMessage.Length);
        string title = firstMessage.Substring(0, titleLength).Trim();
        
        if (firstMessage.Length > titleLength)
        {
            title += "...";
        }
        
        return title;
    }
}
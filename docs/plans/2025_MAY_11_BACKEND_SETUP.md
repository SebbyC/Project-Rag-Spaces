
**Core Strategy: Single Embedding Model**

For simplicity and consistency, we'll assume a single, primary embedding model is used throughout the application for:
1.  Embedding document chunks during file processing.
2.  Embedding user queries for RAG retrieval.

Let's designate **Azure OpenAI's `text-embedding-3-large`** (or `text-embedding-ada-002` for a lower-cost start) as this primary model. This means the `AzureOpenAIService` will be responsible for providing these embeddings. Other LLM services (`OpenAIService`, `GeminiService`) might primarily focus on chat completions, and if they *were* to generate embeddings, it would be an alternative, but our core RAG pipeline will rely on the embeddings from our designated provider.

**Detailed Pseudo-code & Embedding Instructions**

Let's break down the backend services and controllers.

---

**Phase 2: Backend Implementation (.NET API) - Pseudo-code Focus**

**1. `Program.cs` (Startup Configuration)**
   *Pseudo-code Intent:* Configure all services, authentication, database, and middleware.

   ```csharp
   // Program.cs
   public class Program
   {
       public static async Task Main(string[] args)
       {
           var builder = WebApplication.CreateBuilder(args);

           // --- 1. Logging (Serilog) ---
           // builder.Host.UseSerilog(...);

           // --- 2. Configuration ---
           // builder.Services.Configure<AppConfiguration>(builder.Configuration);
           // builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection("AzureOpenAI"));
           // ... other options classes (OpenAI, GoogleVertexAI, FileStorage, Qdrant, GitHub, Jwt)

           // --- 3. Core Services ---
           // builder.Services.AddControllers();
           // builder.Services.AddEndpointsApiExplorer(); // For Swagger
           // builder.Services.AddSwaggerGen(...); // Configure Swagger for JWT

           // --- 4. CORS ---
           // builder.Services.AddCors(options => options.AddPolicy("AllowedOrigins", ...));

           // --- 5. Authentication (JWT) ---
           // builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
           //     .AddJwtBearer(options => { /* ... Use JwtOptions ... */ });
           // builder.Services.AddAuthorization();

           // --- 6. Database (PostgreSQL with EF Core) ---
           // builder.Services.AddDbContext<ApplicationDbContext>(options =>
           //     options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

           // --- 7. Caching (Redis - Optional) ---
           // builder.Services.AddStackExchangeRedisCache(options => { /* ... */ });

           // --- 8. Real-time (SignalR) ---
           // builder.Services.AddSignalR();

           // --- 9. Application Services (Dependency Injection) ---
           // // LLM Services
           // builder.Services.AddHttpClient(); // For SDKs that need it
           // builder.Services.AddScoped<AzureOpenAIService>();
           // builder.Services.AddScoped<OpenAIService>();
           // builder.Services.AddScoped<GeminiService>();
           // builder.Services.AddScoped<LLMServiceFactory>(); // Resolves which ILLMService to use
           // builder.Services.AddScoped<ILLMService>(sp => sp.GetRequiredService<LLMServiceFactory>().ResolveDefault()); // Default provider
           // builder.Services.AddScoped<IEmbeddingProvider>(sp => sp.GetRequiredService<AzureOpenAIService>()); // Explicitly Azure for embeddings

           // // Storage & Vector DB
           // builder.Services.AddSingleton<IFileStorageService, AzureFileStorageService>(); // Singleton if client is thread-safe and stateless
           // builder.Services.AddSingleton<IVectorService, QdrantVectorService>();     // Singleton for Qdrant client

           // // Business Logic Services
           // builder.Services.AddScoped<IUserService, UserService>();
           // builder.Services.AddScoped<IProjectService, ProjectService>();
           // builder.Services.AddScoped<IGitHubService, GitHubService>();
           // builder.Services.AddScoped<IFileProcessingService, FileProcessingService>();
           // builder.Services.AddScoped<IRAGService, RAGService>();
           // builder.Services.AddScoped<IChatService, ChatService>();
           // builder.Services.AddScoped<IConversationSummarizerService, ConversationSummarizerService>();
           // builder.Services.AddSingleton<ITokenBudgetResolver, TokenBudgetResolver>();

           // (AutoMapper, FluentValidation if used)

           // --- 10. Health Checks ---
           // builder.Services.AddHealthChecks().AddNpgSql(...).AddRedis(...);

           var app = builder.Build();

           // --- 11. Middleware Pipeline ---
           // if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
           // app.UseSerilogRequestLogging();
           // // app.UseHttpsRedirection(); // Consider for production
           // app.UseCors("AllowedOrigins");
           // app.UseAuthentication();
           // app.UseAuthorization();
           // app.UseMiddleware<ErrorHandlingMiddleware>();
           // app.MapControllers();
           // app.MapHub<ChatHub>("/hubs/chat");
           // app.MapHealthChecks("/health");

           // --- 12. Database Migrations & Initial Data Seeding ---
           // using (var scope = app.Services.CreateScope())
           // {
           //     var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
           //     await dbContext.Database.MigrateAsync();

           //     var vectorService = scope.ServiceProvider.GetRequiredService<IVectorService>();
           //     var qdrantOptions = scope.ServiceProvider.GetRequiredService<IOptions<QdrantOptions>>().Value;
           //     if (!await vectorService.CollectionExistsAsync(qdrantOptions.CollectionName))
           //     {
           //         // IMPORTANT: Get vector size from the embedding model configuration
           //         var embeddingOptions = scope.ServiceProvider.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
           //         // This is a simplification; ideally, you'd have a dedicated embedding config.
           //         // For text-embedding-3-large it's 3072, for text-embedding-ada-002 it's 1536
           //         int vectorSize = (embeddingOptions.EmbeddingModel == "text-embedding-3-large") ? 3072 : 1536;
           //         await vectorService.CreateCollectionAsync(qdrantOptions.CollectionName, vectorSize);
           //     }
           // }

           await app.RunAsync();
       }
   }
   ```
   *Text Embedding Link:* Note the Qdrant collection creation needs the `vectorSize`. This comes directly from the chosen embedding model. An `IEmbeddingProvider` interface is introduced to make it clear who provides embeddings.

---

**2. `IEmbeddingProvider` Interface (New or can be part of `ILLMService`)**
   *Pseudo-code Intent:* A dedicated contract for generating embeddings. This makes the single embedding strategy clearer.

   ```csharp
   // Interfaces/IEmbeddingProvider.cs
   public interface IEmbeddingProvider
   {
       Task<float[]> GenerateEmbeddingAsync(string text);
       int GetEmbeddingVectorSize(); // Important for Qdrant setup
   }
   ```

---

**3. `AzureOpenAIService.cs` (Implements `ILLMService` and `IEmbeddingProvider`)**
   *Pseudo-code Intent:* Handles chat completions and *our primary text embeddings*.

   ```csharp
   // Services/AzureOpenAIService.cs
   public class AzureOpenAIService : ILLMService, IEmbeddingProvider
   {
       private readonly OpenAIClient _client;
       private readonly AzureOpenAIOptions _options;
       private readonly ILogger<AzureOpenAIService> _logger;

       public AzureOpenAIService(IOptions<AzureOpenAIOptions> options, ILogger<AzureOpenAIService> logger, HttpClient httpClient)
       {
           _options = options.Value;
           _logger = logger;
           // Consider passing HttpClient from IHttpClientFactory if making many calls
           _client = new OpenAIClient(new Uri(_options.Endpoint), new AzureKeyCredential(_options.ApiKey) /*, new OpenAIClientOptions { Transport = new HttpClientTransport(httpClient) } */);
       }

       public async Task<LLMResponse> GenerateCompletionAsync(LLMRequest request)
       {
           // ... (chat completion logic as previously defined)
           // Create ChatCompletionsOptions with request.Model or _options.ModelName
           // Call _client.GetChatCompletionsAsync or GetChatCompletionsStreamingAsync
           // Return LLMResponse
           return new LLMResponse(); // Placeholder
       }

       public async Task<IAsyncEnumerable<string>> StreamCompletionAsync(LLMRequest request)
       {
           // Create ChatCompletionsOptions (ensure request.Stream is true or set here)
           // var streamingChatCompletions = await _client.GetChatCompletionsStreamingAsync(deploymentOrModelName: request.Model ?? _options.ModelName, options);
           // await foreach (StreamingChatCompletionsUpdate chatUpdate in streamingChatCompletions)
           // {
           //     if (!string.IsNullOrEmpty(chatUpdate.ContentUpdate))
           //     {
           //         yield return chatUpdate.ContentUpdate;
           //     }
           // }
           yield break; // Placeholder
       }


       // --- TEXT EMBEDDING IMPLEMENTATION ---
       public async Task<float[]> GenerateEmbeddingAsync(string text)
       {
           if (string.IsNullOrEmpty(text)) return Array.Empty<float>();
           try
           {
               var embeddingsOptions = new EmbeddingsOptions(_options.EmbeddingModel, new[] { text });
               Response<Embeddings> response = await _client.GetEmbeddingsAsync(embeddingsOptions);
               // OpenAI SDK returns a list of embeddings, one for each input string.
               // Since we pass one string, we take the first (and only) result.
               return response.Value.Data[0].Embedding.ToArray();
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, "Error generating embedding with Azure OpenAI for text: {TextSnippet}", text.Substring(0, Math.Min(text.Length, 100)));
               throw; // Or return empty/handle error appropriately
           }
       }

       public int GetEmbeddingVectorSize()
       {
           // This should be configurable or dynamically determined, but for simplicity:
           if (_options.EmbeddingModel.Contains("text-embedding-3-large")) return 3072;
           if (_options.EmbeddingModel.Contains("text-embedding-ada-002")) return 1536;
           _logger.LogWarning("Unknown embedding model for vector size: {ModelName}. Defaulting to 1536.", _options.EmbeddingModel);
           return 1536; // Default or throw exception
       }

       public string GetProviderName() => "azure-openai";
   }
   ```
   **Text Embedding Specific Instructions:**
    1.  **Model Name:** The `_options.EmbeddingModel` (e.g., "text-embedding-3-large") is crucial. It's configured via `appsettings.json` or environment variables.
    2.  **Client:** Uses the `Azure.AI.OpenAI.OpenAIClient`.
    3.  **`EmbeddingsOptions`:** This object takes the deployment name (or model ID) and an `IEnumerable<string>` of texts to embed. We send one text at a time in this example.
    4.  **`_client.GetEmbeddingsAsync()`:** This SDK call makes the API request to Azure OpenAI.
    5.  **Accessing the Vector:** The result `response.Value.Data` is a list. `Data` corresponds to the first input string. `.Embedding` is the `ReadOnlyMemory<float>`, which we convert to `float[]` using `.ToArray()`. This `float[]` is the vector.
    6.  **`GetEmbeddingVectorSize()`:** This method is added to `IEmbeddingProvider`. It's vital for Qdrant when creating the collection to specify the correct vector dimensionality.

---

**4. `LLMServiceFactory.cs`**
   *Pseudo-code Intent:* Selects the correct `ILLMService` implementation (for chat or embeddings).

   ```csharp
   // Services/LLMServiceFactory.cs
   public class LLMServiceFactory
   {
       private readonly IServiceProvider _serviceProvider;
       private readonly IOptions<AppConfiguration> _appConfig; // To get DEFAULT_LLM_PROVIDER

       public LLMServiceFactory(IServiceProvider serviceProvider, IOptions<AppConfiguration> appConfig)
       {
           _serviceProvider = serviceProvider;
           _appConfig = appConfig;
       }

       public ILLMService ResolveChatProvider(string? requestedProviderName = null)
       {
           string providerKey = requestedProviderName ?? _appConfig.Value.DefaultChatProvider; // Assume DefaultChatProvider in AppConfig
           switch (providerKey?.ToLowerInvariant())
           {
               case "azure-openai":
                   return _serviceProvider.GetRequiredService<AzureOpenAIService>();
               case "openai":
                   return _serviceProvider.GetRequiredService<OpenAIService>();
               case "google-gemini":
                   return _serviceProvider.GetRequiredService<GeminiService>();
               default:
                   _logger.LogWarning("Unknown or default LLM provider '{ProviderKey}', falling back to AzureOpenAIService for chat.", providerKey);
                   return _serviceProvider.GetRequiredService<AzureOpenAIService>();
           }
       }

       public IEmbeddingProvider ResolveEmbeddingProvider()
       {
           // For now, always use AzureOpenAIService as the IEmbeddingProvider
           // This could be made configurable later if needed (e.g. _appConfig.Value.DefaultEmbeddingProvider)
           return _serviceProvider.GetRequiredService<AzureOpenAIService>();
       }
   }
   ```
   *Text Embedding Link:* The `ResolveEmbeddingProvider()` method explicitly returns the service designated for embeddings.

---

**5. `IFileProcessingService.cs` & `FileProcessingService.cs`**
   *Pseudo-code Intent:* Handles uploaded/cloned files, chunks them, and generates/stores embeddings.

   ```csharp
   // Interfaces/IFileProcessingService.cs
   public interface IFileProcessingService
   {
       Task ProcessDirectoryAsync(string userId, string projectId, string directoryPathOnShare);
       Task ProcessFileAsync(string userId, string projectId, string filePathOnShare);
   }

   // Services/FileProcessingService.cs
   public class FileProcessingService : IFileProcessingService
   {
       private readonly IFileStorageService _fileStorage;
       private readonly IEmbeddingProvider _embeddingProvider; // Use the dedicated embedding provider
       private readonly IVectorService _vectorService;
       private readonly ApplicationDbContext _dbContext;
       private readonly ILogger<FileProcessingService> _logger;

       public FileProcessingService(IFileStorageService fs, IEmbeddingProvider ep, IVectorService vs, ApplicationDbContext db, ILogger<FileProcessingService> log)
       {
           _fileStorage = fs;
           _embeddingProvider = ep;
           _vectorService = vs;
           _dbContext = db;
           _logger = log;
       }

       public async Task ProcessDirectoryAsync(string userId, string projectId, string directoryPathOnShare)
       {
           // fullDirectoryPath = _fileStorage.GetAbsolutePath(directoryPathOnShare); // This assumes directoryPathOnShare is relative
           // For each file in fullDirectoryPath (use _fileStorage.ListAsync and recurse if needed):
           //     await ProcessFileAsync(userId, projectId, relativeFilePathToShareRoot);
       }

       public async Task ProcessFileAsync(string userId, string projectId, string filePathOnShare)
       {
           _logger.LogInformation("Processing file for embedding: User {UserId}, Project {ProjectId}, Path {FilePath}", userId, projectId, filePathOnShare);
           // string fileContent = await _fileStorage.ReadTextAsync(filePathOnShare); // Assumes ReadTextAsync method in IFileStorageService
           // List<string> chunks = ChunkContent(fileContent, filePathOnShare); // Smart chunking based on file type

           // foreach (var (chunkContent, chunkIndex) in chunks.Select((content, index) => (content, index)))
           // {
           //     if (string.IsNullOrWhiteSpace(chunkContent)) continue;
           //     try
           //     {
           //         float[] embeddingVector = await _embeddingProvider.GenerateEmbeddingAsync(chunkContent);
           //         if (embeddingVector.Length == 0)
           //         {
           //             _logger.LogWarning("Received empty embedding for chunk {ChunkIndex} of file {FilePath}", chunkIndex, filePathOnShare);
           //             continue;
           //         }

           //         var document = new VectorDocument
           //         {
           //             Id = $"{projectId}_{filePathOnShare}_{chunkIndex}".Replace("/", "_"), // Ensure unique ID
           //             Vector = embeddingVector,
           //             Content = chunkContent, // Optional: store original chunk for direct retrieval
           //             Metadata = new Dictionary<string, string>
           //             {
           //                 { "userId", userId },
           //                 { "projectId", projectId },
           //                 { "filePath", filePathOnShare }, // Relative path within project on the share
           //                 { "fileName", Path.GetFileName(filePathOnShare) },
           //                 { "fileType", Path.GetExtension(filePathOnShare) },
           //                 { "chunkIndex", chunkIndex.ToString() }
           //                 // Add other relevant metadata: language, specific code elements if identified
           //             }
           //         };
           //         await _vectorService.StoreEmbeddingAsync(document);

           //         // Update Document entity in PostgreSQL (create if not exists, update IndexedAt, Status)
           //         // var dbDoc = await _dbContext.Documents.FirstOrDefaultAsync(d => d.ProjectId == projectId && d.Path == filePathOnShare);
           //         // if (dbDoc == null) { /* create */ } else { /* update */ }
           //         // await _dbContext.SaveChangesAsync();
           //     }
           //     catch (Exception ex)
           //     {
           //         _logger.LogError(ex, "Error processing chunk {ChunkIndex} for file {FilePath}", chunkIndex, filePathOnShare);
           //     }
           // }
           _logger.LogInformation("Finished processing file: {FilePath}", filePathOnShare);
       }

       private List<string> ChunkContent(string content, string filePath)
       {
           // string extension = Path.GetExtension(filePath)?.ToLowerInvariant();
           // if (extension == ".cs" || extension == ".py" || extension == ".js" || extension == ".ts")
           // {
           //     return ChunkCode(content);
           // }
           // else
           // {
           //     return ChunkText(content); // For .md, .txt
           // }
           return new List<string>(); // Placeholder
       }

       private List<string> ChunkText(string text, int targetChunkSizeInTokens = 500, int overlapTokens = 50)
       {
           // Simple text chunking logic (can be by sentences, paragraphs, or fixed token count)
           // For token-based: use a tokenizer (e.g., SharpToken for OpenAI models)
           // Split text, then join parts ensuring each chunk is ~targetChunkSizeInTokens
           // Implement overlap logic.
           // Example: Split by sentences. Accumulate sentences until near target size.
           return new List<string> { text }; // Placeholder - VERY basic
       }

       private List<string> ChunkCode(string code)
       {
           // More complex:
           // 1. Split by classes/functions (regex or basic parsing).
           // 2. If units are too large, further split by logical blocks or line counts.
           // 3. Maintain semantic coherence.
           // For now, simple line-based chunking with a max line count.
           // int maxLinesPerChunk = 100;
           // return code.Split('\n').Select((line, index) => new { line, index })
           //            .GroupBy(x => x.index / maxLinesPerChunk)
           //            .Select(group => string.Join('\n', group.Select(x => x.line)))
           //            .ToList();
           return new List<string> { code }; // Placeholder - VERY basic
       }
   }
   ```
   **Text Embedding Specific Instructions:**
    1.  It receives an `IEmbeddingProvider` (which will be `AzureOpenAIService` in our setup).
    2.  `ChunkContent` is critical. The quality of chunks directly impacts RAG performance.
        *   **Text Files (.md, .txt):** Split by paragraphs, then perhaps by sentences. Aim for chunks of a few hundred tokens (e.g., 200-500 tokens) with some overlap (e.g., 1-2 sentences or 50 tokens) between chunks to maintain context.
        *   **Code Files (.cs, .py, etc.):** This is harder.
            *   Simple: Split by lines, grouping N lines.
            *   Better: Split by functions/methods/classes (using Regex or basic parsing).
            *   Ideal: Use a full Abstract Syntax Tree (AST) parser for the language to get meaningful semantic blocks. (This is complex to implement for multiple languages).
            *   *Start simple, iterate here.*
    3.  For each `chunkContent`, it calls `await _embeddingProvider.GenerateEmbeddingAsync(chunkContent)`.
    4.  The resulting `embeddingVector` is stored in Qdrant with rich `Metadata`. This metadata is vital for filtering searches later (e.g., "search only within this project and this user's files").
    5.  The `Id` for `VectorDocument` must be unique and ideally reconstructible or queryable if you need to update/delete specific chunks.

---

**6. `IRAGService.cs` & `RAGService.cs`**
   *Pseudo-code Intent:* Takes a user query, finds relevant context, and prepares the augmented prompt.

   ```csharp
   // Interfaces/IRAGService.cs
   public interface IRAGService
   {
       Task<(List<string> contextChunks, string augmentedPrompt)> GetAugmentedContextAndPromptAsync(string userId, string projectId, string originalQuery, string modelForPrompting);
       // GenerateRagResponseAsync & StreamRagResponseAsync might be better placed in IChatService or a new IGenerationService
       // to keep IRAGService focused on context retrieval and prompt augmentation.
   }

   // Services/RAGService.cs
   public class RAGService : IRAGService
   {
       private readonly IVectorService _vectorService;
       private readonly IEmbeddingProvider _embeddingProvider;
       private readonly ILogger<RAGService> _logger;
       private readonly ITokenBudgetResolver _tokenBudgetResolver; // To respect prompt limits

       public RAGService(IVectorService vs, IEmbeddingProvider ep, ILogger<RAGService> log, ITokenBudgetResolver tbr)
       {
           _vectorService = vs;
           _embeddingProvider = ep;
           _logger = log;
           _tokenBudgetResolver = tbr;
       }

       public async Task<(List<string> contextChunks, string augmentedPrompt)> GetAugmentedContextAndPromptAsync(
           string userId, string projectId, string originalQuery, string modelForPrompting)
       {
           // 1. Embed the user's original query
           // float[] queryEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(originalQuery);
           // if (queryEmbedding.Length == 0) return (new List<string>(), originalQuery); // Or handle error

           // 2. Search Qdrant for relevant chunks
           // var filters = new Dictionary<string, string> { { "userId", userId }, { "projectId", projectId } };
           // IEnumerable<VectorSearchResult> searchResults = await _vectorService.SearchAsync(queryEmbedding, limit: 10, filters: filters); // Adjust limit

           // 3. Select and Format Context Chunks (Rank, Filter, Trim to fit LLM context window for `modelForPrompting`)
           // List<string> selectedContextStrings = new List<string>();
           // int currentContextTokenCount = 0; // Need a tokenizer here for the target LLM
           // int maxContextTokensForRAG = _tokenBudgetResolver.GetContextBudgetForRAG(modelForPrompting); // e.g., 30-50% of total window

           // foreach (var hit in searchResults.OrderByDescending(s => s.Score)) // Simple relevance sort
           // {
           //     // string chunkText = $"Source: {hit.Metadata.GetValueOrDefault("fileName", "N/A")}, Path: {hit.Metadata.GetValueOrDefault("filePath", "N/A")}\n{hit.Metadata.GetValueOrDefault("Content", hit.Content)}"; // Use actual content if stored, or fetch
           //     // int chunkTokenCount = EstimateTokens(chunkText, modelForPrompting); // Use a tokenizer
           //     // if (currentContextTokenCount + chunkTokenCount <= maxContextTokensForRAG)
           //     // {
           //     //     selectedContextStrings.Add(chunkText);
           //     //     currentContextTokenCount += chunkTokenCount;
           //     // }
           //     // else break; // Stop if budget exceeded
           // }

           // 4. Construct the Augmented Prompt
           // string contextBlock = string.Join("\n\n---\n\n", selectedContextStrings);
           // string systemPrompt = "You are a helpful AI assistant for the project {projectId}. Use the provided context to answer the user's question. Cite sources if possible from the context (e.g., 'According to fileName.md...'). If the context doesn't provide an answer, say so.";
           // string finalAugmentedPrompt = $"{systemPrompt}\n\nBEGIN CONTEXT\n{contextBlock}\nEND CONTEXT\n\nUser Question: {originalQuery}";

           // return (selectedContextStrings, finalAugmentedPrompt);
           return (new List<string>(), originalQuery); // Placeholder
       }
   }
   ```
   **Text Embedding Specific Instructions:**
    1.  It uses `_embeddingProvider.GenerateEmbeddingAsync(originalQuery)` to get a vector for the user's current question.
    2.  This `queryEmbedding` is then used to search Qdrant.
    3.  **Crucial:** The prompt construction needs to be mindful of the *target chat LLM's* token limit. The `ITokenBudgetResolver` would provide guidance here. You need a tokenizer (like SharpToken for OpenAI, or approximations for others) to count tokens for context chunks and the overall prompt.

---

**7. `IChatService.cs` & `ChatService.cs`**
   *Pseudo-code Intent:* Orchestrates the entire chat interaction, including RAG.

   ```csharp
   // Services/ChatService.cs
   public class ChatService : IChatService
   {
       private readonly IRAGService _ragService;
       private readonly LLMServiceFactory _llmFactory; // To get the CHAT LLM
       private readonly ApplicationDbContext _dbContext;
       private readonly ILogger<ChatService> _logger;
       private readonly ITokenBudgetResolver _tokenBudgetResolver;
       private readonly IConversationSummarizerService _summarizerService;

       public ChatService(IRAGService rag, LLMServiceFactory llmFact, ApplicationDbContext db, ILogger<ChatService> log, ITokenBudgetResolver tbr, IConversationSummarizerService css)
       {
           _ragService = rag;
           _llmFactory = llmFact;
           _dbContext = db;
           _logger = log;
           _tokenBudgetResolver = tbr;
           _summarizerService = css;
       }

       public async Task<ChatMessageResponse> ProcessChatAsync(string userId, ChatRequest chatRequest)
       {
           // 1. Get/Create ChatSession, save user's ChatMessage
           // var userMessage = new DomainModels.ChatMessage { Role = "user", Content = chatRequest.Content, ... };
           // _dbContext.ChatMessages.Add(userMessage);
           // await _dbContext.SaveChangesAsync();

           // 2. Determine current conversation history to pass to LLM
           // List<ApiModels.ChatMessage> conversationHistoryForPrompt = await GetRecentMessagesForPrompt(chatRequest.SessionId, chatRequest.Model);

           // 3. Augment the LATEST user query with RAG context
           // var (ragContextChunks, augmentedUserQueryForLLM) = await _ragService.GetAugmentedContextAndPromptAsync(
           //     userId, chatRequest.ProjectId, chatRequest.Content, chatRequest.Model
           // );
           // // Replace the last user message content with the augmented one if RAG found context
           // // Or construct a new prompt that includes history + RAG context + latest query

           // // Construct the full message list for the LLM call
           // List<ApiModels.ChatMessage> messagesForLLM = conversationHistoryForPrompt.Take(conversationHistoryForPrompt.Count -1).ToList(); // History without last user query
           // messagesForLLM.Add(new ApiModels.ChatMessage { Role = "user", Content = augmentedUserQueryForLLM }); // Add augmented user query

           // 4. Get LLM for chat completion
           // ILLMService chatLlm = _llmFactory.ResolveChatProvider(chatRequest.Model);
           // var llmApiRequest = new LLMRequest { Messages = messagesForLLM, Model = chatRequest.Model, Stream = false };
           // LLMResponse llmResponse = await chatLlm.GenerateCompletionAsync(llmApiRequest);

           // 5. Save assistant's ChatMessage
           // var assistantMessage = new DomainModels.ChatMessage { Role = "assistant", Content = llmResponse.Content, ... };
           // _dbContext.ChatMessages.Add(assistantMessage);
           // await _dbContext.SaveChangesAsync();

           // 6. Check for conversation summarization
           // await CheckAndTriggerSummarization(chatRequest.SessionId, chatRequest.Model);

           // return new ChatMessageResponse { /* ... */ };
           return new ChatMessageResponse(); // Placeholder
       }

       public async IAsyncEnumerable<string> StreamChatAsync(string userId, ChatRequest chatRequest)
       {
           // Similar to ProcessChatAsync but:
           // 1. Get/Create ChatSession, save user's ChatMessage
           // 2. Get conversation history for prompt
           // 3. Augment latest user query with RAG context -> augmentedUserQueryForLLM
           // 4. Construct messagesForLLM list
           // 5. Get LLM for chat completion
           // ILLMService chatLlm = _llmFactory.ResolveChatProvider(chatRequest.Model);
           // var llmApiRequest = new LLMRequest { Messages = messagesForLLM, Model = chatRequest.Model, Stream = true };
           // StringBuilder assistantFullResponse = new StringBuilder();
           // await foreach (var chunk in chatLlm.StreamCompletionAsync(llmApiRequest)) // Assuming ILLMService has StreamCompletionAsync
           // {
           //     assistantFullResponse.Append(chunk);
           //     yield return chunk;
           // }
           // 6. Save assistant's full ChatMessage (after stream completes)
           // 7. Check for conversation summarization
           yield break; // Placeholder
       }
       
       private async Task CheckAndTriggerSummarization(string sessionId, string modelName)
       {
            // int currentTokenCount = await CalculateSessionTokenCount(sessionId, modelName); // Needs tokenizer
            // int cutoff = _tokenBudgetResolver.ConversationCutoff(modelName);
            // if (currentTokenCount > cutoff)
            // {
            //    await _summarizerService.CompactAsync(sessionId, modelName);
            //    // Potentially notify user via SignalR or next response
            // }
       }
       // ... other methods like GetChatHistoryAsync, GetRecentMessagesForPrompt
   }
   ```
   *Text Embedding Link:* This service coordinates with `IRAGService`, which itself uses `IEmbeddingProvider` to embed the *user's current query*.

---

**8. Other Services & Controllers (Higher-Level Pseudo-code):**

*   **`IUserService`, `AuthController`**:
    *   `Register`: Hash password, store user.
    *   `Login`: Verify credentials, generate JWT.
*   **`IProjectService`, `ProjectController`**:
    *   `CreateProject`: Create DB entry. Critical: Call `IFileStorageService.CreateDirectoryAsync` for project's root and subdirs (`source/`, `ai/summaries/`, `ai/implementation_plans/`, `ai/change_logs/`, `meta/`) on Azure File Share. Write initial `project.json` to `meta/` dir.
    *   `GetProjectsForUser`, `GetProjectById`.
*   **`IGitHubService` (called by `ProjectController`)**:
    *   `SyncRepository(userId, projectId, repoUrl, pat)`:
        *   Construct target path on Azure File Share: `_fileStorageService.GetAbsolutePath($"{userId}/{projectId}/source/github/{repoOwner}_{repoName}")`.
        *   Use `Octokit.NET` or `git clone/pull` CLI command to fetch repo to this path.
        *   After clone/pull, call `_fileProcessingService.ProcessDirectoryAsync(userId, projectId, relativeRepoPathOnShare)`.
*   **`FileController`**:
    *   `Upload(IFormFile file, string projectId)`:
        *   `targetPath = $"{User.Identity.Name}/{projectId}/source/uploads/{file.FileName}"`
        *   `await _fileStorage.WriteAsync(targetPath, file.OpenReadStream())`
        *   `_ = _fileProcessingService.ProcessFileAsync(User.Identity.Name, projectId, targetPath)` (fire and forget or queue).
    *   `GetTree(projectId)`: Call `_fileStorageService.ListAsync` recursively for `{User.Identity.Name}/{projectId}` to build and return the dynamic directory tree JSON.
*   **`IConversationSummarizerService`, `ConversationSummarizerService.cs`**:
    *   `CompactAsync(sessionId, modelName)`:
        *   Fetch full conversation history for `sessionId` from DB.
        *   Format it (e.g., "User: ...\nAssistant: ...").
        *   Select a summarization LLM (could be `modelName` or a specific cheaper one).
        *   Use the strict JSON summarization prompt (as discussed: `{"high_level": "...", "key_files": [], "todos": []}`).
        *   Call `ILLMService.GenerateCompletionAsync` with the history and summarization prompt.
        *   Parse the JSON response.
        *   Save the JSON summary to Azure File Share: `_fileStorage.WriteAsync($"{userId}/{projectId}/ai/summaries/{sessionId}_{timestamp}.summary.json", summaryStream)`.
        *   Trigger `_fileProcessingService.ProcessFileAsync` for this new summary file so it becomes RAG-able.
        *   Update original `ChatSession` in DB (e.g., mark as summarized, store summary file path).
        *   (Optional) Create a new `ChatSession` seeded with this summary as the first system message.

This detailed breakdown provides a solid pseudo-code foundation. The key for text embedding is centralizing the `GenerateEmbeddingAsync` logic (likely in `AzureOpenAIService` if that's your primary) and ensuring services like `FileProcessingService` and `RAGService` use this consistent provider via the `IEmbeddingProvider` interface. Remember that accurate token counting and intelligent chunking are vital companions to the embedding process for good RAG performance.

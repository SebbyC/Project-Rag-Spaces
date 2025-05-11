using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RagWorkspace.Api.Configuration;
using RagWorkspace.Api.Data;
using RagWorkspace.Api.Interfaces;
using RagWorkspace.Api.Middleware;
using RagWorkspace.Api.Models;
using RagWorkspace.Api.Services;
using RagWorkspace.Api.Services.Chunkers;
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
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<ChunkingOptions>(builder.Configuration.GetSection("Chunking"));

// Add controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
    {
        policy.WithOrigins(builder.Configuration["ALLOWED_ORIGINS"]?.Split(',') ?? new[] { "*" })
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Configure Authentication
var jwtSecret = builder.Configuration["JWT_SECRET"] ?? throw new InvalidOperationException("JWT_SECRET not configured");
var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? "rag-workspace";
var jwtAudience = builder.Configuration["JWT_AUDIENCE"] ?? "rag-workspace-api";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

// Configure Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection") ?? 
                      builder.Configuration["DATABASE_CONNECTION_STRING"]));

// Configure Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? 
                           builder.Configuration["REDIS_CONNECTION_STRING"];
});

// Configure SignalR
builder.Services.AddSignalR();

// Register services
builder.Services.AddHttpClient(); // Add HttpClient for SDK clients

// LLM Services
builder.Services.AddScoped<AzureOpenAIService>();
builder.Services.AddScoped<OpenAIService>();
builder.Services.AddScoped<GeminiService>();

// Service Factory for resolving different LLM providers
builder.Services.AddScoped<LLMServiceFactory>();
builder.Services.AddScoped<ILLMService>(sp => sp.GetRequiredService<LLMServiceFactory>().ResolveChatProvider());
builder.Services.AddScoped<IEmbeddingProvider>(sp => sp.GetRequiredService<LLMServiceFactory>().ResolveEmbeddingProvider());

// Vector storage and file storage
builder.Services.AddSingleton<IVectorService, QdrantVectorService>();
builder.Services.AddSingleton<IFileStorageService, AzureFileStorageService>();

// Token management
builder.Services.AddSingleton<ITokenBudgetResolver, TokenBudgetResolver>();

// Tokenizer service
builder.Services.AddSingleton<ITokenizerService, SharpTokenTokenizer>();

// Chunkers
builder.Services.AddScoped<PlainTextChunker>();
builder.Services.AddScoped<MarkdownChunker>();
builder.Services.AddScoped<CodeChunker>();
builder.Services.AddScoped<JsonYamlChunker>();

// Core application services
builder.Services.AddScoped<IFileProcessingService, FileProcessingService>();
builder.Services.AddScoped<IGitHubService, GitHubService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IRAGService, RAGService>();
builder.Services.AddScoped<IChatService, ChatService>();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection") ?? 
               builder.Configuration["DATABASE_CONNECTION_STRING"] ?? "")
    .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? 
              builder.Configuration["REDIS_CONNECTION_STRING"] ?? "");

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

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
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();
        
        // Initialize vector database collections
        var vectorService = services.GetRequiredService<IVectorService>();
        var embeddingProvider = services.GetRequiredService<IEmbeddingProvider>();
        var collectionName = app.Configuration.GetValue<string>("QDRANT_COLLECTION_NAME") ?? "code-embeddings";

        if (!await vectorService.CollectionExistsAsync(collectionName))
        {
            // Get vector size from embedding provider to ensure consistency
            int vectorSize = embeddingProvider.GetEmbeddingVectorSize();
            logger.LogInformation("Creating vector collection {CollectionName} with dimension {VectorSize}",
                collectionName, vectorSize);

            await vectorService.CreateCollectionAsync(collectionName, vectorSize);
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database/vector store.");
    }
}

app.Run();
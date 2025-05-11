using Microsoft.Extensions.Options;
using RagWorkspace.Api.Configuration;
using RagWorkspace.Api.Interfaces;

namespace RagWorkspace.Api.Services;

/// <summary>
/// Factory for creating LLM service instances based on configuration or request
/// </summary>
public class LLMServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AppConfiguration _appConfig;
    private readonly ILogger<LLMServiceFactory> _logger;

    public LLMServiceFactory(
        IServiceProvider serviceProvider,
        IOptions<AppConfiguration> appConfig,
        ILogger<LLMServiceFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _appConfig = appConfig.Value;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the appropriate LLM service provider for chat completions
    /// </summary>
    /// <param name="requestedProviderName">Optional provider name to override default</param>
    /// <returns>An ILLMService implementation</returns>
    public ILLMService ResolveChatProvider(string? modelName = null)
    {
        if (!string.IsNullOrEmpty(modelName))
        {
            // Azure OpenAI models
            if (modelName == _appConfig.AzureOpenAI.ModelName || modelName.StartsWith("gpt-"))
            {
                return _serviceProvider.GetRequiredService<AzureOpenAIService>();
            }

            // Google Gemini models
            if (modelName.Contains("gemini"))
            {
                return _serviceProvider.GetRequiredService<GeminiService>();
            }

            // Add other model pattern matching as needed
        }

        // Default to Azure OpenAI if available, otherwise try other configured providers
        if (!string.IsNullOrEmpty(_appConfig.AzureOpenAI.ModelName))
        {
            return _serviceProvider.GetRequiredService<AzureOpenAIService>();
        }

        // Additional fallbacks as needed
        _logger.LogWarning("No default provider configured. Falling back to AzureOpenAIService.");
        return _serviceProvider.GetRequiredService<AzureOpenAIService>();
    }

    /// <summary>
    /// Resolves the embedding provider - always returns Azure OpenAI
    /// for consistency across the application
    /// </summary>
    /// <returns>An IEmbeddingProvider implementation</returns>
    public IEmbeddingProvider ResolveEmbeddingProvider()
    {
        // We're explicitly using a single embedding provider/model for consistency
        // across all vector operations
        var provider = _serviceProvider.GetRequiredService<AzureOpenAIService>();
        _logger.LogDebug("Using embedding provider: {ProviderName} with model: {ModelName}",
            provider.GetProviderName(), _appConfig.AzureOpenAI.EmbeddingModel);
        return provider;
    }
}
}
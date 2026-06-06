using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using MultimodalUIAnalyzer.Configuration;
using MultimodalUIAnalyzer.Services;

namespace MultimodalUIAnalyzer.DependencyInjection;

public static class SemanticKernelServiceCollectionExtensions
{
    public static IServiceCollection AddSemanticKernelVision(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var aiOptions = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>()
            ?? throw new InvalidOperationException($"Missing '{AiOptions.SectionName}' configuration section.");

        services.AddKernel();
        RegisterChatCompletion(services, aiOptions);
        services.AddHttpClient<IOllamaModelService, OllamaModelService>(client =>
        {
            client.BaseAddress = new Uri(aiOptions.Ollama.Endpoint);
            client.Timeout = TimeSpan.FromMinutes(60);
        });

        services.AddSingleton<IAiProviderMetadata>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            var modelId = options.Provider switch
            {
                AiProvider.OpenAI => options.OpenAI.ModelId,
                AiProvider.Ollama => options.Ollama.ModelId,
                _ => throw new InvalidOperationException($"Unsupported AI provider '{options.Provider}'.")
            };

            var models = options.Provider switch
            {
                AiProvider.OpenAI => [options.OpenAI.ModelId],
                AiProvider.Ollama => NormalizeOllamaModels(options.Ollama),
                _ => throw new InvalidOperationException($"Unsupported AI provider '{options.Provider}'.")
            };

            return new AiProviderMetadata(
                options.Provider.ToString(),
                modelId,
                options.ServiceId,
                models,
                selectedModelId => BuildServiceId(options.ServiceId, selectedModelId));
        });

        services.AddScoped<IVisionAnalysisService, VisionAnalysisService>();

        return services;
    }

    private static void RegisterChatCompletion(IServiceCollection services, AiOptions options)
    {
        switch (options.Provider)
        {
            case AiProvider.OpenAI:
                if (string.IsNullOrWhiteSpace(options.OpenAI.ApiKey))
                {
                    throw new InvalidOperationException("Ai:OpenAI:ApiKey is required when Ai:Provider is OpenAI.");
                }

                services.AddOpenAIChatCompletion(
                    modelId: options.OpenAI.ModelId,
                    apiKey: options.OpenAI.ApiKey,
                    orgId: string.IsNullOrWhiteSpace(options.OpenAI.OrganizationId)
                        ? null
                        : options.OpenAI.OrganizationId,
                    serviceId: BuildServiceId(options.ServiceId, options.OpenAI.ModelId));
                break;

            case AiProvider.Ollama:
                foreach (var modelId in NormalizeOllamaModels(options.Ollama))
                {
                    services.AddOllamaChatCompletion(
                        modelId: modelId,
                        endpoint: new Uri(options.Ollama.Endpoint),
                        serviceId: BuildServiceId(options.ServiceId, modelId));
                }
                break;

            default:
                throw new InvalidOperationException($"Unsupported AI provider '{options.Provider}'.");
        }
    }

    private static string[] NormalizeOllamaModels(OllamaOptions options) =>
        options.Models
            .Append(options.ModelId)
            .Where(modelId => !string.IsNullOrWhiteSpace(modelId))
            .Select(modelId => modelId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string BuildServiceId(string baseServiceId, string modelId)
    {
        var safeModelId = modelId
            .Replace(':', '-')
            .Replace('/', '-')
            .Replace('\\', '-');

        return $"{baseServiceId}:{safeModelId}";
    }
}

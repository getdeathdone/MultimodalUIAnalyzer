namespace MultimodalUIAnalyzer.Services;

public interface IAiProviderMetadata
{
    string Provider { get; }

    string DefaultModelId { get; }

    string ServiceId { get; }

    IReadOnlyCollection<string> AvailableModels { get; }

    string ResolveModelId(string? requestedModelId);

    string ResolveServiceId(string modelId);
}

public sealed class AiProviderMetadata(
    string provider,
    string defaultModelId,
    string serviceId,
    IReadOnlyCollection<string> availableModels,
    Func<string, string> serviceIdFactory) : IAiProviderMetadata
{
    public string Provider { get; } = provider;

    public string DefaultModelId { get; } = defaultModelId;

    public string ServiceId { get; } = serviceId;

    public IReadOnlyCollection<string> AvailableModels { get; } = availableModels;

    public string ResolveModelId(string? requestedModelId)
    {
        if (string.IsNullOrWhiteSpace(requestedModelId))
        {
            return DefaultModelId;
        }

        var modelId = requestedModelId.Trim();

        if (!AvailableModels.Contains(modelId, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Model '{modelId}' is not configured for provider '{Provider}'.");
        }

        return AvailableModels.First(model => string.Equals(model, modelId, StringComparison.OrdinalIgnoreCase));
    }

    public string ResolveServiceId(string modelId) => serviceIdFactory(modelId);
}

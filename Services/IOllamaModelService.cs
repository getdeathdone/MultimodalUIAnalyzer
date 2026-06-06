namespace MultimodalUIAnalyzer.Services;

public interface IOllamaModelService
{
    Task EnsureModelAvailableAsync(string modelId, CancellationToken cancellationToken);

    IAsyncEnumerable<OllamaModelProgress> EnsureModelAvailableWithProgressAsync(
        string modelId,
        CancellationToken cancellationToken);
}

public sealed record OllamaModelProgress(
    string Model,
    string Status,
    long? Completed,
    long? Total,
    int? Percent,
    bool Done);

using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MultimodalUIAnalyzer.Configuration;

namespace MultimodalUIAnalyzer.Services;

public sealed class OllamaModelService(
    HttpClient httpClient,
    IOptions<AiOptions> options,
    IAiProviderMetadata providerMetadata,
    ILogger<OllamaModelService> logger) : IOllamaModelService
{
    public async Task EnsureModelAvailableAsync(string modelId, CancellationToken cancellationToken)
    {
        if (options.Value.Provider != AiProvider.Ollama)
        {
            return;
        }

        var resolvedModelId = providerMetadata.ResolveModelId(modelId);

        if (await IsModelAvailableAsync(resolvedModelId, cancellationToken))
        {
            return;
        }

        logger.LogInformation("Pulling Ollama model {ModelId}.", resolvedModelId);

        var response = await httpClient.PostAsJsonAsync(
            "/api/pull",
            new OllamaPullRequest(resolvedModelId, Stream: false),
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async IAsyncEnumerable<OllamaModelProgress> EnsureModelAvailableWithProgressAsync(
        string modelId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (options.Value.Provider != AiProvider.Ollama)
        {
            yield return new OllamaModelProgress(modelId, "Cloud provider selected", null, null, 100, Done: true);
            yield break;
        }

        var resolvedModelId = providerMetadata.ResolveModelId(modelId);

        yield return new OllamaModelProgress(
            resolvedModelId,
            "Checking local Ollama models",
            null,
            null,
            null,
            Done: false);

        if (await IsModelAvailableAsync(resolvedModelId, cancellationToken))
        {
            yield return new OllamaModelProgress(resolvedModelId, "Model already available", null, null, 100, Done: true);
            yield break;
        }

        logger.LogInformation("Pulling Ollama model {ModelId} with progress.", resolvedModelId);

        yield return new OllamaModelProgress(
            resolvedModelId,
            "Model is missing, starting download",
            null,
            null,
            0,
            Done: false);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/pull")
        {
            Content = JsonContent.Create(new OllamaPullRequest(resolvedModelId, Stream: true))
        };

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var progress = ParsePullProgress(resolvedModelId, line);
            yield return progress;

            if (progress.Done)
            {
                yield break;
            }
        }
    }

    private async Task<bool> IsModelAvailableAsync(string modelId, CancellationToken cancellationToken)
    {
        var tags = await httpClient.GetFromJsonAsync<OllamaTagsResponse>("/api/tags", cancellationToken)
            ?? new OllamaTagsResponse([]);

        return tags.Models.Any(model =>
            string.Equals(model.Name, modelId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(model.Name, $"{modelId}:latest", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record OllamaPullRequest(string Name, bool Stream);

    private sealed record OllamaTagsResponse(OllamaModelTag[] Models);

    private sealed record OllamaModelTag(string Name);

    private static OllamaModelProgress ParsePullProgress(string modelId, string jsonLine)
    {
        using var document = JsonDocument.Parse(jsonLine);
        var root = document.RootElement;

        var status = root.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString() ?? "Pulling model"
            : "Pulling model";

        var completed = TryGetInt64(root, "completed");
        var total = TryGetInt64(root, "total");
        int? percent = completed is not null && total is > 0
            ? (int)Math.Clamp(Math.Round(completed.Value * 100d / total.Value), 0, 100)
            : null;

        var done = string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);

        return new OllamaModelProgress(modelId, status, completed, total, percent, done);
    }

    private static long? TryGetInt64(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.TryGetInt64(out var number)
            ? number
            : null;
    }
}

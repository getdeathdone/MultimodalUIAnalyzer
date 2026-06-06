using System.Text.Json;

namespace MultimodalUIAnalyzer.Services;

public interface IVisionAnalysisService
{
    Task<VisionAnalysisResult> AnalyzeAsync(
        Stream imageStream,
        string mimeType,
        string? userPrompt,
        string? modelId,
        CancellationToken cancellationToken);

    Task<VisionAnalysisResult> AnalyzeAsync(
        byte[] imageBytes,
        string mimeType,
        string? userPrompt,
        string? modelId,
        CancellationToken cancellationToken);
}

public sealed record VisionAnalysisResult(
    JsonElement Json,
    string Provider,
    string ModelId,
    string RawResponse);

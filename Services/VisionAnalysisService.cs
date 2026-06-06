using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MultimodalUIAnalyzer.Services;

public sealed class VisionAnalysisService(
    Kernel kernel,
    IAiProviderMetadata providerMetadata,
    ILogger<VisionAnalysisService> logger) : IVisionAnalysisService
{
    private static readonly JsonDocumentOptions JsonDocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow
    };

    public async Task<VisionAnalysisResult> AnalyzeAsync(
        Stream imageStream,
        string mimeType,
        string? userPrompt,
        string? modelId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        await using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream, cancellationToken);

        return await AnalyzeAsync(memoryStream.ToArray(), mimeType, userPrompt, modelId, cancellationToken);
    }

    public async Task<VisionAnalysisResult> AnalyzeAsync(
        byte[] imageBytes,
        string mimeType,
        string? userPrompt,
        string? modelId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);

        if (imageBytes.Length == 0)
        {
            throw new ArgumentException("Image payload is empty.", nameof(imageBytes));
        }

        var chatHistory = BuildChatHistory(imageBytes, mimeType, userPrompt);
        var resolvedModelId = providerMetadata.ResolveModelId(modelId);
        var resolvedServiceId = providerMetadata.ResolveServiceId(resolvedModelId);

        // Resolve through Kernel so the business service remains provider-agnostic while still honoring SK service IDs.
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>(resolvedServiceId);

        var responses = await chatCompletionService.GetChatMessageContentsAsync(
            chatHistory,
            executionSettings: null,
            kernel: kernel,
            cancellationToken);

        var rawResponse = responses.FirstOrDefault()?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            throw new InvalidOperationException("AI provider returned an empty response.");
        }

        var jsonText = ExtractJson(rawResponse);

        if (TryParseJson(jsonText, out var parsedJson))
        {
            return new VisionAnalysisResult(
                parsedJson,
                providerMetadata.Provider,
                resolvedModelId,
                rawResponse);
        }

        logger.LogWarning("AI provider returned invalid JSON, attempting repair: {Response}", rawResponse);

        var repairedResponse = await RepairJsonAsync(chatCompletionService, rawResponse, cancellationToken);
        var repairedJsonText = ExtractJson(repairedResponse);

        if (TryParseJson(repairedJsonText, out var repairedJson))
        {
            return new VisionAnalysisResult(
                repairedJson,
                providerMetadata.Provider,
                resolvedModelId,
                rawResponse);
        }

        throw new InvalidOperationException($"AI provider returned invalid JSON. Raw response: {Truncate(rawResponse, 800)}");
    }

    private static ChatHistory BuildChatHistory(byte[] imageBytes, string mimeType, string? userPrompt)
    {
        var prompt = string.IsNullOrWhiteSpace(userPrompt)
            ? "Analyze this UI screenshot and return the JSON object defined by the system instructions."
            : userPrompt.Trim();

        var history = new ChatHistory();
        history.AddSystemMessage(VisionPrompts.SystemPrompt);

        // Semantic Kernel represents multimodal user input as one chat message with multiple content items.
        // TextContent carries the instruction, ImageContent carries the binary image plus its MIME type.
        var items = new ChatMessageContentItemCollection
        {
            new TextContent(prompt),
            new ImageContent(imageBytes, mimeType)
        };

        history.Add(new ChatMessageContent(AuthorRole.User, items));

        return history;
    }

    private async Task<string> RepairJsonAsync(
        IChatCompletionService chatCompletionService,
        string rawResponse,
        CancellationToken cancellationToken)
    {
        var repairHistory = new ChatHistory();
        repairHistory.AddSystemMessage("""
            You repair malformed model output into strictly valid JSON.
            Return only valid JSON. Do not use Markdown. Do not explain anything.
            If data is missing, use null or an empty array.
            """);
        repairHistory.AddUserMessage($"""
            Convert this response into valid JSON matching the UI analysis schema.
            Return JSON only:

            {rawResponse}
            """);

        var repairResponses = await chatCompletionService.GetChatMessageContentsAsync(
            repairHistory,
            executionSettings: null,
            kernel: kernel,
            cancellationToken);

        return repairResponses.FirstOrDefault()?.Content?.Trim() ?? string.Empty;
    }

    private static bool TryParseJson(string jsonText, out JsonElement json)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonText, JsonDocumentOptions);
            json = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            json = default;
            return false;
        }
    }

    private static string ExtractJson(string response)
    {
        var trimmed = response.Trim();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLineIndex = trimmed.IndexOf('\n');
            var lastFenceIndex = trimmed.LastIndexOf("```", StringComparison.Ordinal);

            if (firstNewLineIndex >= 0 && lastFenceIndex > firstNewLineIndex)
            {
                trimmed = trimmed[(firstNewLineIndex + 1)..lastFenceIndex].Trim();
            }
        }

        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return trimmed;
        }

        var objectStart = trimmed.IndexOf('{');
        var objectEnd = trimmed.LastIndexOf('}');

        if (objectStart >= 0 && objectEnd > objectStart)
        {
            return trimmed[objectStart..(objectEnd + 1)].Trim();
        }

        var arrayStart = trimmed.IndexOf('[');
        var arrayEnd = trimmed.LastIndexOf(']');

        return arrayStart >= 0 && arrayEnd > arrayStart
            ? trimmed[arrayStart..(arrayEnd + 1)].Trim()
            : trimmed;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : $"{value[..maxLength]}...";
}

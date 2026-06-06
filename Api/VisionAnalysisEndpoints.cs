using Microsoft.Extensions.Options;
using MultimodalUIAnalyzer.Configuration;
using MultimodalUIAnalyzer.Services;
using System.Text.Json;

namespace MultimodalUIAnalyzer.Api;

public static class VisionAnalysisEndpoints
{
    public static IEndpointRouteBuilder MapVisionAnalysisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/vision");

        group.MapPost("/analyze", AnalyzeAsync)
            .DisableAntiforgery()
            .WithName("AnalyzeVision")
            .WithSummary("Analyzes an uploaded UI screenshot with a configured multimodal AI provider.");

        group.MapGet("/models/ensure", EnsureModelAsync)
            .WithName("EnsureVisionModel")
            .WithSummary("Ensures the selected Ollama model is available and streams pull progress.");

        return endpoints;
    }

    private static async Task EnsureModelAsync(
        string model,
        HttpResponse response,
        IOllamaModelService ollamaModelService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("VisionAnalysisEndpoints");

        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";
        response.ContentType = "text/event-stream";

        try
        {
            await foreach (var progress in ollamaModelService.EnsureModelAvailableWithProgressAsync(model, cancellationToken))
            {
                await WriteSseAsync(response, progress, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or JsonException)
        {
            logger.LogWarning(exception, "Failed to ensure Ollama model {Model}.", model);

            await WriteSseAsync(
                response,
                new
                {
                    model,
                    status = exception.Message,
                    done = true,
                    error = true
                },
                cancellationToken);
        }
    }

    private static async Task<IResult> AnalyzeAsync(
        HttpRequest request,
        IVisionAnalysisService visionAnalysisService,
        IOllamaModelService ollamaModelService,
        IOptions<VisionAnalysisOptions> options,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("VisionAnalysisEndpoints");

        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new ProblemDetailsDto("Request must be multipart/form-data."));
        }

        IFormCollection form;

        try
        {
            // ASP.NET Core buffers multipart sections according to FormOptions configured in Program.cs.
            // Keep uploaded files as streams until the vision service copies them asynchronously.
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            logger.LogWarning(exception, "Invalid multipart upload.");
            return Results.BadRequest(new ProblemDetailsDto("Invalid multipart upload."));
        }

        var image = form.Files.GetFile("image");
        var prompt = form["prompt"].ToString();
        var modelId = form["model"].ToString();

        if (image is null || image.Length == 0)
        {
            return Results.BadRequest(new ProblemDetailsDto("Field 'image' is required."));
        }

        if (image.Length > options.Value.MaxImageBytes)
        {
            return Results.BadRequest(new ProblemDetailsDto($"Image exceeds {options.Value.MaxImageBytes} bytes."));
        }

        if (!options.Value.AllowedMimeTypes.Contains(image.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new ProblemDetailsDto($"Unsupported image MIME type '{image.ContentType}'."));
        }

        try
        {
            await ollamaModelService.EnsureModelAvailableAsync(modelId, cancellationToken);

            await using var stream = image.OpenReadStream();
            var result = await visionAnalysisService.AnalyzeAsync(
                stream,
                image.ContentType,
                prompt,
                modelId,
                cancellationToken);

            return Results.Json(new
            {
                provider = result.Provider,
                model = result.ModelId,
                analysis = result.Json
            });
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "AI provider request failed.");
            return Results.Json(
                new ProblemDetailsDto("AI provider is unavailable. If you use Ollama, verify that Ollama is running, the model is pulled, and Ai:Ollama:Endpoint is correct."),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Vision analysis failed.");
            return Results.Json(
                new ProblemDetailsDto(exception.Message),
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private sealed record ProblemDetailsDto(string Error);

    private static async Task WriteSseAsync<T>(
        HttpResponse response,
        T payload,
        CancellationToken cancellationToken)
    {
        await response.WriteAsync("data: ", cancellationToken);
        await JsonSerializer.SerializeAsync(response.Body, payload, cancellationToken: cancellationToken);
        await response.WriteAsync("\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}

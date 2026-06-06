using System.ComponentModel.DataAnnotations;

namespace MultimodalUIAnalyzer.Configuration;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    [Required]
    public AiProvider Provider { get; init; } = AiProvider.OpenAI;

    [Required]
    public string ServiceId { get; init; } = "vision-chat";

    [Required]
    public OpenAiOptions OpenAI { get; init; } = new();

    [Required]
    public OllamaOptions Ollama { get; init; } = new();
}

public sealed class OpenAiOptions
{
    [Required]
    public string ModelId { get; init; } = "gpt-4o";

    public string ApiKey { get; init; } = string.Empty;

    public string? OrganizationId { get; init; }
}

public sealed class OllamaOptions
{
    [Required]
    public string ModelId { get; init; } = "qwen2.5vl:7b";

    [Required]
    public string[] Models { get; init; } =
    [
        "qwen2.5vl:7b",
        "llama3.2-vision:11b",
        "llava-llama3:8b",
        "llava"
    ];

    [Required]
    public string Endpoint { get; init; } = "http://localhost:11434";
}

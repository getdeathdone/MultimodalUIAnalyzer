using System.ComponentModel.DataAnnotations;

namespace MultimodalUIAnalyzer.Configuration;

public sealed class VisionAnalysisOptions
{
    public const string SectionName = "VisionAnalysis";

    [Range(1, 50 * 1024 * 1024)]
    public long MaxImageBytes { get; init; } = 10 * 1024 * 1024;

    [Required]
    public string[] AllowedMimeTypes { get; init; } =
    [
        "image/png",
        "image/jpeg",
        "image/webp"
    ];
}

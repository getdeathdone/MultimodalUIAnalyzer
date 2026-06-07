using Microsoft.AspNetCore.Http.Features;
using MultimodalUIAnalyzer.Api;
using MultimodalUIAnalyzer.Configuration;
using MultimodalUIAnalyzer.DependencyInjection;
using System.Diagnostics;

const string AppUrl = "http://localhost:5088";
var shouldOpenBrowser = !args.Any(arg => arg.Equals("--no-open", StringComparison.OrdinalIgnoreCase));

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(AppUrl);

builder.Services.AddOptions<VisionAnalysisOptions>()
    .Bind(builder.Configuration.GetSection(VisionAnalysisOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.Configure<FormOptions>(options =>
{
    var maxImageBytes = builder.Configuration
        .GetSection(VisionAnalysisOptions.SectionName)
        .GetValue<long>(nameof(VisionAnalysisOptions.MaxImageBytes), 10 * 1024 * 1024);

    options.MultipartBodyLengthLimit = maxImageBytes;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCors", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        if (origins.Length == 0)
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSemanticKernelVision(builder.Configuration);

var app = builder.Build();

TryStartOllamaIfNeeded(app.Configuration);

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("DefaultCors");

app.MapGet("/install-windows.ps1", (IWebHostEnvironment environment) =>
{
    var path = Path.Combine(environment.WebRootPath, "install-windows.ps1");
    return File.Exists(path)
        ? Results.File(path, "text/plain; charset=utf-8", "install-windows.ps1")
        : Results.NotFound();
});

app.MapGet("/install-mac.sh", (IWebHostEnvironment environment) =>
{
    var path = Path.Combine(environment.WebRootPath, "install-mac.sh");
    return File.Exists(path)
        ? Results.File(path, "text/x-shellscript; charset=utf-8", "install-mac.sh")
        : Results.NotFound();
});

app.MapGet("/api/status", (IConfiguration configuration) => Results.Ok(new
{
    app = "Multimodal UI Analyzer",
    status = "running",
    provider = configuration["Ai:Provider"] ?? "Ollama",
    defaultModel = configuration["Ai:Ollama:ModelId"] ?? configuration["Ai:OpenAI:ModelId"],
    time = DateTimeOffset.UtcNow
}));

app.MapVisionAnalysisEndpoints();

if (shouldOpenBrowser)
{
    app.Lifetime.ApplicationStarted.Register(() => OpenBrowser(AppUrl));
}

app.Run();

static void OpenBrowser(string url)
{
    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
    catch
    {
        // Browser auto-open is a convenience. The server can still be opened manually.
    }
}

static void TryStartOllamaIfNeeded(IConfiguration configuration)
{
    var provider = configuration["Ai:Provider"] ?? "Ollama";
    if (!provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    var endpoint = configuration["Ai:Ollama:Endpoint"] ?? "http://localhost:11434";
    if (IsHttpEndpointAvailable($"{endpoint.TrimEnd('/')}/api/tags"))
    {
        return;
    }

    var localOllama = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        "Ollama",
        OperatingSystem.IsWindows() ? "ollama.exe" : "ollama");

    var fileName = File.Exists(localOllama) ? localOllama : "ollama";

    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = "serve",
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Minimized
        });
    }
    catch
    {
        // If Ollama is not installed, the UI will show provider errors and setup instructions.
    }
}

static bool IsHttpEndpointAvailable(string url)
{
    try
    {
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        using var response = httpClient.GetAsync(url).GetAwaiter().GetResult();
        return response.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}

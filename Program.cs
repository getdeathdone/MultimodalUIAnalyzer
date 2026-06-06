using Microsoft.AspNetCore.Http.Features;
using MultimodalUIAnalyzer.Api;
using MultimodalUIAnalyzer.Configuration;
using MultimodalUIAnalyzer.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

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
            policy.AllowAnyOrigin();
            return;
        }

        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSemanticKernelVision(builder.Configuration);

var app = builder.Build();

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("DefaultCors");

app.MapVisionAnalysisEndpoints();

app.Run();

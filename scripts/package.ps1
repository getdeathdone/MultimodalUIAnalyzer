param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "MultimodalUIAnalyzer.csproj"
$artifacts = Join-Path $root "artifacts"
$publishRoot = Join-Path $artifacts "publish"
$packageRoot = Join-Path $artifacts "packages"
$hostedDownloads = Join-Path $root "hosted-ui\downloads"
$wwwrootDownloads = Join-Path $root "wwwroot\downloads"
$docsDownloads = Join-Path $root "docs\downloads"

$runtimes = @(
    @{ Rid = "win-x64"; Launcher = "Start-Windows.bat"; Archive = "MultimodalUIAnalyzer-$Version-win-x64.zip" },
    @{ Rid = "osx-x64"; Launcher = "start-macos.command"; Archive = "MultimodalUIAnalyzer-$Version-osx-x64.zip" },
    @{ Rid = "osx-arm64"; Launcher = "start-macos.command"; Archive = "MultimodalUIAnalyzer-$Version-osx-arm64.zip" }
)

New-Item -ItemType Directory -Force -Path $publishRoot, $packageRoot, $hostedDownloads, $wwwrootDownloads, $docsDownloads | Out-Null

foreach ($runtime in $runtimes) {
    $rid = $runtime.Rid
    $publishDir = Join-Path $publishRoot $rid
    $packageDir = Join-Path $artifacts "package-$rid"

    if (Test-Path $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }

    if (Test-Path $packageDir) {
        Remove-Item -LiteralPath $packageDir -Recurse -Force
    }

    Write-Host "Publishing $rid..."
    dotnet publish $project `
        -c $Configuration `
        -r $rid `
        --self-contained true `
        -o $publishDir `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $rid with exit code $LASTEXITCODE"
    }

    New-Item -ItemType Directory -Force -Path $packageDir | Out-Null
    Copy-Item -Path (Join-Path $publishDir "*") -Destination $packageDir -Recurse -Force

    if ($rid -eq "win-x64") {
        Copy-Item -Path (Join-Path $root "scripts\launchers\Start-Windows.bat") -Destination $packageDir -Force
    }
    else {
        Copy-Item -Path (Join-Path $root "scripts\launchers\start-macos.command") -Destination $packageDir -Force
        Copy-Item -Path (Join-Path $root "scripts\launchers\start-macos.sh") -Destination $packageDir -Force
    }

    Copy-Item -Path (Join-Path $root "README-Packaged.md") -Destination (Join-Path $packageDir "README.md") -Force

    $archivePath = Join-Path $packageRoot $runtime.Archive
    if (Test-Path $archivePath) {
        Remove-Item -LiteralPath $archivePath -Force
    }

    Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $archivePath -Force
    Copy-Item -Path $archivePath -Destination (Join-Path $hostedDownloads $runtime.Archive) -Force
    Copy-Item -Path $archivePath -Destination (Join-Path $wwwrootDownloads $runtime.Archive) -Force
    Copy-Item -Path $archivePath -Destination (Join-Path $docsDownloads $runtime.Archive) -Force

    if ($rid -eq "win-x64") {
        Copy-Item -Path (Join-Path $publishDir "MultimodalUIAnalyzer.exe") -Destination (Join-Path $hostedDownloads "MultimodalUIAnalyzer-win-x64.exe") -Force
        Copy-Item -Path (Join-Path $publishDir "MultimodalUIAnalyzer.exe") -Destination (Join-Path $wwwrootDownloads "MultimodalUIAnalyzer-win-x64.exe") -Force
        Copy-Item -Path (Join-Path $publishDir "MultimodalUIAnalyzer.exe") -Destination (Join-Path $docsDownloads "MultimodalUIAnalyzer-win-x64.exe") -Force
    }

    Write-Host "Created $archivePath"
}

Write-Host ""
Write-Host "Packages are ready in: $packageRoot"
Write-Host "Hosted downloads are ready in: $hostedDownloads"
Write-Host "Local setup downloads are ready in: $wwwrootDownloads"
Write-Host "GitHub Pages downloads are ready in: $docsDownloads"

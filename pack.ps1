# BlazorMemory — Build & Pack all NuGet packages
# Run from repo root: .\pack.ps1
# Output: ./nupkgs/*.nupkg

$ErrorActionPreference = "Stop"
$outputDir = ".\nupkgs"

if (Test-Path $outputDir) { Remove-Item $outputDir -Recurse -Force }
New-Item -ItemType Directory -Path $outputDir | Out-Null

$projects = @(
    "src\BlazorMemory.Core\BlazorMemory.Core.csproj",
    "src\BlazorMemory.Storage.InMemory\BlazorMemory.Storage.InMemory.csproj",
    "src\BlazorMemory.Storage.EfCore\BlazorMemory.Storage.EfCore.csproj",
    "src\BlazorMemory.Storage.IndexedDb\BlazorMemory.Storage.IndexedDb.csproj",
    "src\BlazorMemory.Embeddings.OpenAi\BlazorMemory.Embeddings.OpenAi.csproj",
    "src\BlazorMemory.Extractor.OpenAi\BlazorMemory.Extractor.OpenAi.csproj",
    "src\BlazorMemory.Extractor.Anthropic\BlazorMemory.Extractor.Anthropic.csproj"
)

foreach ($project in $projects) {
    Write-Host "Packing $project..." -ForegroundColor Cyan
    dotnet pack $project -c Release --no-build -o $outputDir
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to pack $project"
        exit 1
    }
}

Write-Host ""
Write-Host "Packages built:" -ForegroundColor Green
Get-ChildItem $outputDir -Filter "*.nupkg" | ForEach-Object {
    Write-Host "  $($_.Name)" -ForegroundColor White
}

Write-Host ""
Write-Host "To publish to NuGet.org:" -ForegroundColor Yellow
Write-Host "  dotnet nuget push nupkgs\*.nupkg --api-key YOUR_KEY --source https://api.nuget.org/v3/index.json --skip-duplicate" -ForegroundColor White
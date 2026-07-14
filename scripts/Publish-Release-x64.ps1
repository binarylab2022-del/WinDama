param(
    [switch]$SelfContained,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $RepoRoot "WinDama1.0\WinDama.csproj"
$TestProject = Join-Path $RepoRoot "WinDama.Tests\WinDama.Tests.csproj"
$PublishDir = Join-Path $RepoRoot "artifacts\publish\WinDama-win-x64"
$PackageDir = Join-Path $RepoRoot "artifacts\packages"
$PackagePath = Join-Path $PackageDir "WinDama-1.0.1-preview-win-x64.zip"

Write-Host "WinDama Release x64 publish" -ForegroundColor Cyan
Write-Host "Repository: $RepoRoot"

if (-not $SkipTests) {
    Write-Host "Running tests..." -ForegroundColor Cyan
    dotnet test $TestProject -c Release
}

if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
New-Item -ItemType Directory -Force -Path $PackageDir | Out-Null

$selfContainedValue = if ($SelfContained) { "true" } else { "false" }

Write-Host "Publishing WPF app..." -ForegroundColor Cyan
dotnet publish $Project `
    -c Release `
    -r win-x64 `
    --self-contained $selfContainedValue `
    -o $PublishDir `
    /p:PublishSingleFile=false `
    /p:PublishReadyToRun=false

if (Test-Path $PackagePath) {
    Remove-Item $PackagePath -Force
}

Write-Host "Creating ZIP package..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $PackagePath -Force

Write-Host "Done." -ForegroundColor Green
Write-Host "Publish folder: $PublishDir"
Write-Host "ZIP package:    $PackagePath"

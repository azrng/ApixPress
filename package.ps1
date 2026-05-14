[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDirectory = "artifacts/package/win-x64",
    [switch]$NoClean,
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSCommandPath
$projectPath = Join-Path $repoRoot "src/ApixPress.App/ApixPress.App.csproj"
$resolvedOutputDirectory = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
} else {
    Join-Path $repoRoot $OutputDirectory
}

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Project file not found: $projectPath"
}

if (-not $NoClean -and (Test-Path -LiteralPath $resolvedOutputDirectory)) {
    Remove-Item -LiteralPath $resolvedOutputDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null

$publishArgs = @(
    "publish",
    $projectPath,
    "-c",
    $Configuration,
    "-r",
    $Runtime,
    "-o",
    $resolvedOutputDirectory,
    "-p:SelfContained=true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:PublishReadyToRun=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:PublishTrimmed=false"
)

if ($NoRestore) {
    $publishArgs += "--no-restore"
}

Write-Host "Publishing ApixPress..."
Write-Host "Project: $projectPath"
Write-Host "Runtime: $Runtime"
Write-Host "Output: $resolvedOutputDirectory"

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$exePath = Join-Path $resolvedOutputDirectory "ApixPress.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Publish completed but ApixPress.exe was not found: $exePath"
}

Write-Host "Done: $exePath"

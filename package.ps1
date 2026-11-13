param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

dotnet build -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$manifest = Get-Content "$root\thunderstore\manifest.json" -Raw | ConvertFrom-Json
$zip = Join-Path $root "releases\$($manifest.name)-$($manifest.version_number).zip"
New-Item -ItemType Directory -Force -Path (Split-Path $zip) | Out-Null
if (Test-Path $zip) { Remove-Item $zip }

Compress-Archive -Path `
    "$root\thunderstore\manifest.json", `
    "$root\thunderstore\README.md", `
    "$root\thunderstore\CHANGELOG.md", `
    "$root\thunderstore\icon.png", `
    "$root\thunderstore\LICENSE", `
    "$root\bin\$Configuration\$($manifest.name).dll" `
    -DestinationPath $zip

Write-Host "Package: $zip"

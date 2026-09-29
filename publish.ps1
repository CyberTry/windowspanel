# WindowsPanel build & package script
# Output: dist/WindowsPanel/WindowsPanel.exe + dist/WindowsPanel-win-x64.zip
# Prereq: .NET 8 SDK, Node.js 18+

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

Write-Host '==> [1/4] Install web dependencies...' -ForegroundColor Cyan
Push-Location 'src/WindowsPanel.Web'
if (-not (Test-Path 'node_modules')) { npm install }
if ($LASTEXITCODE -ne 0) { throw 'npm install failed' }

Write-Host '==> [2/4] Build web (Vue -> wwwroot)...' -ForegroundColor Cyan
# Workaround: esbuild temp-copy can fail with 'Access is denied' in system TEMP (AV lock).
# Use a project-local temp dir instead.
$env:TEMP = "$PWD\.tmp"
$env:TMP  = "$PWD\.tmp"
New-Item -ItemType Directory -Force -Path $env:TEMP | Out-Null
npm run build
if ($LASTEXITCODE -ne 0) { throw 'web build failed' }
Pop-Location

Write-Host '==> [3/4] Publish server (self-contained single-file exe)...' -ForegroundColor Cyan
dotnet publish src/WindowsPanel.Server -c Release
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

$publishDir = 'src/WindowsPanel.Server/bin/Release/net8.0-windows/win-x64/publish'
$distDir = Join-Path $root 'dist/WindowsPanel'

Write-Host '==> [4/4] Collect artifacts...' -ForegroundColor Cyan
if (Test-Path $distDir) { Remove-Item $distDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $distDir | Out-Null
Copy-Item (Join-Path $publishDir '*') $distDir -Recurse

# Remove pdbs to reduce size
Get-ChildItem $distDir -Filter *.pdb -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
$zipPath = Join-Path $root 'dist/WindowsPanel-win-x64.zip'
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $distDir '*') -DestinationPath $zipPath

Write-Host ''
Write-Host 'Done!' -ForegroundColor Green
Write-Host "  exe: $distDir\WindowsPanel.exe"
Write-Host "  zip: $zipPath"
Write-Host ''
Write-Host 'Usage: double-click WindowsPanel.exe (browser opens at http://127.0.0.1:9721)'

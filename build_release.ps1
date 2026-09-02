# =====================================================================
# RAMAVERSE STUDIO • 100% Automated Release Build & Packaging Script
# Generates both:
#   1. dist\RamaverseStudio-Setup.exe (1-Click Windows Setup Installer)
#   2. dist\RamaverseStudio-v1.2.0-Portable.exe (Direct Portable Standalone .exe)
# =====================================================================
param (
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot
$publishDir = Join-Path $rootDir "publish\worldwide"
$distDir = Join-Path $rootDir "dist"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "       RAMAVERSE STUDIO • PRODUCTION EXE RELEASE PACKAGER         " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan

# 1. Clean & Prepare dist directory
if (Test-Path $distDir) {
    Remove-Item $distDir -Recurse -Force
}
New-Item -ItemType Directory -Path $distDir | Out-Null

# 2. Compile & Publish Single-File Self-Contained App Binary
Write-Host "`n[1/2] Compiling Standalone Single-File .exe Binary..." -ForegroundColor Yellow
dotnet publish "$rootDir\RamaverseStudio\RamaverseStudio.csproj" -c $Configuration -p:PublishProfile=WorldwideSingleFile

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: App publish failed!" -ForegroundColor Red
    exit 1
}

$exePath = Join-Path $publishDir "RamaverseStudio.exe"
if (Test-Path $exePath) {
    $portableDest = Join-Path $distDir "RamaverseStudio-v1.2.0-Portable.exe"
    Copy-Item $exePath $portableDest -Force
    $sizeMb = [Math]::Round((Get-Item $portableDest).Length / 1MB, 2)
    Write-Host "  OK Portable Executable: $portableDest ($sizeMb MB)" -ForegroundColor Green
}

# 3. Compile Single-File Setup Installer (.exe)
Write-Host "`n[2/2] Compiling 1-Click Windows Setup Installer (.exe)..." -ForegroundColor Yellow
dotnet publish "$rootDir\RamaverseStudio.Setup\RamaverseStudio.Setup.csproj" -c $Configuration -o $distDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Setup installer publish failed!" -ForegroundColor Red
    exit 1
}

$setupExe = Join-Path $distDir "RamaverseStudio-Setup.exe"
if (Test-Path $setupExe) {
    $setupMb = [Math]::Round((Get-Item $setupExe).Length / 1MB, 2)
    Write-Host "  OK Windows Setup Installer: $setupExe ($setupMb MB)" -ForegroundColor Green
}

# Clean any leftover debug pdb files in dist
Get-ChildItem -Path $distDir -Filter "*.pdb" | Remove-Item -Force

Write-Host "`n==================================================================" -ForegroundColor Cyan
Write-Host "ALL PRODUCTION EXECUTABLES ARE READY FOR MARKET LAUNCH!" -ForegroundColor Green
Write-Host "Files available in: $distDir" -ForegroundColor White
Write-Host "==================================================================" -ForegroundColor Cyan

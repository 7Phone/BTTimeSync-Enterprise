[CmdletBinding()]
param(
    [string]$Version = "v1.0"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactRoot = Join-Path $RepoRoot "artifacts\$Version"

$ConsoleProject = Join-Path $RepoRoot "src\BTTimeSync.Console\BTTimeSync.Console.csproj"
$ServerProject  = Join-Path $RepoRoot "src\BTTimeSync.Server\BTTimeSync.Server.csproj"

$ConsolePublishDir = Join-Path $RepoRoot "src\BTTimeSync.Console\bin\Release\net10.0-windows10.0.22621.0\publish\win-x64"
$ServerPublishDir  = Join-Path $RepoRoot "src\BTTimeSync.Server\bin\Release\net10.0-windows10.0.22621.0\publish\win-x64"

$ConsoleArtifactDir = Join-Path $ArtifactRoot "Console"
$ServerArtifactDir  = Join-Path $ArtifactRoot "Server"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " BTTimeSync Release Publisher" -ForegroundColor Cyan
Write-Host " Version: $Version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ------------------------------------------------------------
# 1. Validate projects
# ------------------------------------------------------------

if (-not (Test-Path $ConsoleProject)) {
    throw "Console project not found: $ConsoleProject"
}

if (-not (Test-Path $ServerProject)) {
    throw "Server project not found: $ServerProject"
}

# ------------------------------------------------------------
# 2. Clean artifact directory
# ------------------------------------------------------------

Write-Host "[1/5] Cleaning artifact directory..." -ForegroundColor Yellow

if (Test-Path $ArtifactRoot) {
    Remove-Item $ArtifactRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $ConsoleArtifactDir -Force | Out-Null
New-Item -ItemType Directory -Path $ServerArtifactDir -Force | Out-Null

# ------------------------------------------------------------
# 3. Publish Console
# ------------------------------------------------------------

Write-Host "[2/5] Publishing Console..." -ForegroundColor Yellow

dotnet publish $ConsoleProject `
    -c Release `
    -p:PublishProfile=FolderProfile

if ($LASTEXITCODE -ne 0) {
    throw "Console publish failed."
}

# ------------------------------------------------------------
# 4. Publish Server
# ------------------------------------------------------------

Write-Host "[3/5] Publishing Server..." -ForegroundColor Yellow

dotnet publish $ServerProject `
    -c Release `
    -p:PublishProfile=FolderProfile

if ($LASTEXITCODE -ne 0) {
    throw "Server publish failed."
}

# ------------------------------------------------------------
# 5. Copy release files
# ------------------------------------------------------------

Write-Host "[4/5] Copying release files..." -ForegroundColor Yellow

$ConsoleFiles = @(
    "BTTimeSync.Console.exe",
    "BTTimeSync.Console.pdb",
    "BTTimeSync.Bluetooth.pdb",
    "BTTimeSync.Common.pdb",
    "BTTimeSync.Core.pdb"
)

$ServerFiles = @(
    "BTTimeSync.Server.exe",
    "BTTimeSync.Server.pdb",
    "BTTimeSync.Bluetooth.pdb",
    "BTTimeSync.Common.pdb",
    "BTTimeSync.Core.pdb"
)

foreach ($File in $ConsoleFiles) {
    $Source = Join-Path $ConsolePublishDir $File

    if (-not (Test-Path $Source)) {
        throw "Console release file not found: $Source"
    }

    Copy-Item $Source $ConsoleArtifactDir -Force
}

foreach ($File in $ServerFiles) {
    $Source = Join-Path $ServerPublishDir $File

    if (-not (Test-Path $Source)) {
        throw "Server release file not found: $Source"
    }

    Copy-Item $Source $ServerArtifactDir -Force
}

Write-Host "[5/5] Release package created." -ForegroundColor Green
Write-Host ""

# ------------------------------------------------------------
# Summary
# ------------------------------------------------------------

Write-Host "========================================" -ForegroundColor Green
Write-Host " Release completed successfully" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

Write-Host "Console:" -ForegroundColor Cyan
Get-ChildItem $ConsoleArtifactDir -File |
    Select-Object Name, @{Name="SizeMB";Expression={[math]::Round($_.Length / 1MB, 2)}} |
    Format-Table -AutoSize

Write-Host "Server:" -ForegroundColor Cyan
Get-ChildItem $ServerArtifactDir -File |
    Select-Object Name, @{Name="SizeMB";Expression={[math]::Round($_.Length / 1MB, 2)}} |
    Format-Table -AutoSize

Write-Host "Release directory:" -ForegroundColor Cyan
Write-Host $ArtifactRoot
Write-Host ""

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
# 1. Validate Git repository
# ------------------------------------------------------------

Write-Host "[1/6] Validating Git repository..." -ForegroundColor Yellow

Push-Location $RepoRoot

try {
    $GitStatus = git status --porcelain

    if ($LASTEXITCODE -ne 0) {
        throw "Unable to read Git repository status."
    }

    if ($GitStatus) {
        Write-Host ""
        Write-Host "Git working tree is not clean." -ForegroundColor Red
        Write-Host "Please commit or discard all changes before publishing." -ForegroundColor Red
        Write-Host ""
        Write-Host $GitStatus
        throw "Release aborted because the Git working tree is not clean."
    }

    $GitCommit = (git rev-parse --short HEAD).Trim()

    if ($LASTEXITCODE -ne 0 -or -not $GitCommit) {
        throw "Unable to determine current Git commit."
    }

    $GitBranch = (git branch --show-current).Trim()

    if ($LASTEXITCODE -ne 0 -or -not $GitBranch) {
        throw "Unable to determine current Git branch."
    }
}
finally {
    Pop-Location
}

Write-Host "Git branch : $GitBranch" -ForegroundColor Green
Write-Host "Git commit : $GitCommit" -ForegroundColor Green
Write-Host ""

# ------------------------------------------------------------
# 2. Validate projects
# ------------------------------------------------------------

Write-Host "[2/6] Validating projects..." -ForegroundColor Yellow

if (-not (Test-Path $ConsoleProject)) {
    throw "Console project not found: $ConsoleProject"
}

if (-not (Test-Path $ServerProject)) {
    throw "Server project not found: $ServerProject"
}

# ------------------------------------------------------------
# 3. Clean artifact directory
# ------------------------------------------------------------

Write-Host "[3/6] Cleaning artifact directory..." -ForegroundColor Yellow

if (Test-Path $ArtifactRoot) {
    Remove-Item $ArtifactRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $ConsoleArtifactDir -Force | Out-Null
New-Item -ItemType Directory -Path $ServerArtifactDir -Force | Out-Null

# ------------------------------------------------------------
# 4. Publish Console
# ------------------------------------------------------------

Write-Host "[4/6] Publishing Console..." -ForegroundColor Yellow

dotnet publish $ConsoleProject `
    -c Release `
    -p:PublishProfile=FolderProfile

if ($LASTEXITCODE -ne 0) {
    throw "Console publish failed."
}

# ------------------------------------------------------------
# 5. Publish Server
# ------------------------------------------------------------

Write-Host "[5/6] Publishing Server..." -ForegroundColor Yellow

dotnet publish $ServerProject `
    -c Release `
    -p:PublishProfile=FolderProfile

if ($LASTEXITCODE -ne 0) {
    throw "Server publish failed."
}

# ------------------------------------------------------------
# 6. Copy release files
# ------------------------------------------------------------

Write-Host "[6/6] Copying release files..." -ForegroundColor Yellow

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

# ------------------------------------------------------------
# Release metadata
# ------------------------------------------------------------

$ReleaseInfo = @"
BTTimeSync Release
==================

Version : $Version
Branch  : $GitBranch
Commit  : $GitCommit
Date    : $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

Console : BTTimeSync.Console.exe
Server  : BTTimeSync.Server.exe
Runtime : win-x64
Mode    : Self-contained
Format  : Single-file
"@

$ReleaseInfo | Set-Content (Join-Path $ArtifactRoot "RELEASE.txt") -Encoding UTF8

# ------------------------------------------------------------
# Summary
# ------------------------------------------------------------

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " Release completed successfully" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

Write-Host "Version : $Version" -ForegroundColor Cyan
Write-Host "Branch  : $GitBranch" -ForegroundColor Cyan
Write-Host "Commit  : $GitCommit" -ForegroundColor Cyan
Write-Host ""

Write-Host "Console:" -ForegroundColor Cyan
Get-ChildItem $ConsoleArtifactDir -File |
    Select-Object Name, @{Name="SizeMB";Expression={[math]::Round($_.Length / 1MB, 2)}} |
    Format-Table -AutoSize

Write-Host "Server:" -ForegroundColor Cyan
Get-ChildItem $ServerArtifactDir -File |
    Select-Object Name, @{Name="SizeMB";Expression={[math]::Round($_.Length / 1MB, 2)}} |
    Format-Table -AutoSize

Write-Host "Release metadata:" -ForegroundColor Cyan
Write-Host (Join-Path $ArtifactRoot "RELEASE.txt")

Write-Host ""
Write-Host "Release directory:" -ForegroundColor Cyan
Write-Host $ArtifactRoot
Write-Host ""

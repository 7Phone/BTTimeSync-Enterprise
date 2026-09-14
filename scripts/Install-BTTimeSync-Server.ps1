[CmdletBinding()]
param(
    [string]$Version = "v1.0"
)

$ErrorActionPreference = "Stop"

$InstallRoot = "C:\Program Files\BTTimeSync\Server"

$RepoRoot = Split-Path -Parent $PSScriptRoot

$SourceDir = Join-Path `
    $RepoRoot `
    "artifacts\$Version\Server"

$ExeName = "BTTimeSync.Server.exe"

$TargetExe = Join-Path `
    $InstallRoot `
    $ExeName


$TaskName = "BTTimeSync-Server"


Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " BTTimeSync Server Installer" -ForegroundColor Cyan
Write-Host " Version: $Version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""


# ------------------------------------------------------------
# Check source
# ------------------------------------------------------------

Write-Host "[1/5] Checking release package..." -ForegroundColor Yellow


$SourceExe = Join-Path `
    $SourceDir `
    $ExeName


if (-not (Test-Path $SourceExe)) {

    throw "Cannot find server executable: $SourceExe"

}


# ------------------------------------------------------------
# Create install directory
# ------------------------------------------------------------

Write-Host "[2/5] Creating install directory..." -ForegroundColor Yellow


if (-not (Test-Path $InstallRoot)) {

    New-Item `
        -ItemType Directory `
        -Path $InstallRoot `
        -Force | Out-Null

}


# ------------------------------------------------------------
# Copy files
# ------------------------------------------------------------

Write-Host "[3/5] Installing Server..." -ForegroundColor Yellow


Copy-Item `
    $SourceExe `
    $TargetExe `
    -Force


# ------------------------------------------------------------
# Create scheduled task
# ------------------------------------------------------------

Write-Host "[4/5] Creating scheduled task..." -ForegroundColor Yellow


$CurrentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name


$Action = New-ScheduledTaskAction `
    -Execute $TargetExe `
    -WorkingDirectory $InstallRoot


$Trigger = New-ScheduledTaskTrigger `
    -AtStartup


$Principal = New-ScheduledTaskPrincipal `
    -UserId $CurrentUser `
    -LogonType S4U `
    -RunLevel Highest


Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $Action `
    -Trigger $Trigger `
    -Principal $Principal `
    -Force | Out-Null


# ------------------------------------------------------------
# Start server
# ------------------------------------------------------------

Write-Host "[5/5] Starting Server..." -ForegroundColor Yellow


schtasks.exe `
    /Run `
    /TN $TaskName


Write-Host ""

Write-Host "========================================" -ForegroundColor Green
Write-Host " Server installation completed" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

Write-Host ""

Write-Host "Install path:"
Write-Host $TargetExe

Write-Host ""

Write-Host "Task:"
Write-Host $TaskName

Write-Host ""


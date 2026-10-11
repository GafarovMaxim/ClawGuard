# build.ps1 — ClawGuard Automated Build Script
[CmdletBinding()]
param(
    [string]$Version = '2.5.0.0',
    [switch]$SkipZip
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Building ClawGuard v$Version" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Verify or import PS2EXE module
$ps2exeLoaded = $false
$localModule = 'C:\Users\gfrov\AppData\Local\Temp\ps2exe\PS2EXE-master\Module\ps2exe.psm1'
if (Test-Path -LiteralPath $localModule) {
    Import-Module $localModule -Force
    $ps2exeLoaded = $true
} elseif (Get-Module -ListAvailable -Name ps2exe) {
    Import-Module ps2exe -Force
    $ps2exeLoaded = $true
} else {
    Write-Host "Installing PS2EXE module..." -ForegroundColor Yellow
    Install-Module ps2exe -Scope CurrentUser -Force -SkipPublisherCheck
    Import-Module ps2exe -Force
    $ps2exeLoaded = $true
}

# 2. Paths
$src = Join-Path $repoRoot 'ClawGuardUI.ps1'
$ico = Join-Path $repoRoot 'ClawGuard.ico'
$distDir = Join-Path $repoRoot 'dist'
if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir -Force | Out-Null }

$exeTarget = Join-Path $distDir 'ClawGuard.exe'
$portableTarget = Join-Path $distDir 'ClawGuardPortable.exe'

# Kill any running instances locking files
Get-Process -Name 'ClawGuard*' -ErrorAction SilentlyContinue | ForEach-Object {
    try { Stop-Process -Id $_.Id -Force } catch { }
}
Start-Sleep -Milliseconds 500

# 3. Compile Standard Edition
Write-Host "`n[1/3] Compiling ClawGuard.exe..." -ForegroundColor Green
Invoke-PS2EXE -InputFile $src -OutputFile $exeTarget `
    -IconFile $ico -NoConsole -STA -Title 'ClawGuard' -Description 'ClawGuard Fail-Closed VPN Kill Switch for Claude' `
    -Product 'ClawGuard' -Company 'ClawGuard' -Version $Version -Copyright 'Copyright (c) 2026 Gafarov Maxim' | Out-Null

# 4. Compile Portable Edition
Write-Host "[2/3] Compiling ClawGuardPortable.exe..." -ForegroundColor Green
Invoke-PS2EXE -InputFile $src -OutputFile $portableTarget `
    -IconFile $ico -NoConsole -STA -Title 'ClawGuard Portable' -Description 'ClawGuard Portable Fail-Closed VPN Kill Switch' `
    -Product 'ClawGuard' -Company 'ClawGuard' -Version $Version -Copyright 'Copyright (c) 2026 Gafarov Maxim' | Out-Null

# Mirror to root for local execution
Copy-Item -LiteralPath $exeTarget -Destination (Join-Path $repoRoot 'ClawGuard.exe') -Force
Copy-Item -LiteralPath $portableTarget -Destination (Join-Path $repoRoot 'ClawGuardPortable.exe') -Force

# 5. Generate Checksums and Zip packages
if (-not $SkipZip) {
    Write-Host "[3/3] Generating release packages & SHA-256 sums..." -ForegroundColor Green
    
    $zipMain = Join-Path $distDir "ClawGuard-v2.5.0-win-x64.zip"
    $zipPortable = Join-Path $distDir "ClawGuardPortable-v2.5.0-win-x64.zip"
    
    if (Test-Path $zipMain) { Remove-Item $zipMain -Force }
    if (Test-Path $zipPortable) { Remove-Item $zipPortable -Force }
    
    Compress-Archive -Path @($exeTarget, $ico, (Join-Path $repoRoot 'LICENSE')) -DestinationPath $zipMain -Force
    Compress-Archive -Path @($portableTarget, (Join-Path $repoRoot 'LICENSE')) -DestinationPath $zipPortable -Force
    
    $checksumFile = Join-Path $distDir 'SHA256SUMS.txt'
    $hashLines = @()
    Get-ChildItem -Path $distDir -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | ForEach-Object {
        $hash = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash.ToLower()
        $hashLines += "$hash  $($_.Name)"
    }
    $hashLines | Out-File -FilePath $checksumFile -Encoding utf8 -Force
    
    Write-Host "`nSHA-256 Checksums:" -ForegroundColor Magenta
    Get-Content $checksumFile | ForEach-Object { Write-Host "  $_" }
}

Write-Host "`nBuild succeeded! Artifacts are ready in: $distDir" -ForegroundColor Cyan

# build.ps1 — ClawGuard Pure Native C# Automated Build Script
[CmdletBinding()]
param(
    [string]$Version = '2.5.1.0',
    [switch]$SkipZip
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Building ClawGuard v$Version (Native C#)" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Locate Native .NET Framework C# Compiler
$frameworkDir = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
if (-not (Test-Path -LiteralPath $frameworkDir)) {
    $frameworkDir = "C:\Windows\Microsoft.NET\Framework\v4.0.30319"
}
$csc = Join-Path $frameworkDir 'csc.exe'
$wpfDir = Join-Path $frameworkDir 'WPF'

if (-not (Test-Path -LiteralPath $csc)) {
    throw "Microsoft .NET Framework 4.8 C# Compiler (csc.exe) not found at: $csc"
}

Write-Host "Compiler: $csc" -ForegroundColor DarkGray

# 2. Paths
$src = Join-Path $repoRoot 'src\Program.cs'
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

$refs = @(
    'PresentationCore.dll',
    'PresentationFramework.dll',
    'WindowsBase.dll',
    'System.Xaml.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.dll',
    'System.Core.dll'
) -join ','

# 3. Compile Standard Edition
Write-Host "`n[1/3] Compiling ClawGuard.exe..." -ForegroundColor Green
$cmdArgs = @(
    '/nologo',
    '/target:winexe',
    "/win32icon:$ico",
    "/lib:$wpfDir",
    "/out:$exeTarget",
    "/r:$refs",
    $src
)
& $csc $cmdArgs
if ($LASTEXITCODE -ne 0) { throw "Compilation of ClawGuard.exe failed with exit code $LASTEXITCODE" }

# 4. Compile Portable Edition
Write-Host "[2/3] Compiling ClawGuardPortable.exe..." -ForegroundColor Green
$cmdArgsPortable = @(
    '/nologo',
    '/target:winexe',
    "/win32icon:$ico",
    "/lib:$wpfDir",
    "/out:$portableTarget",
    "/r:$refs",
    $src
)
& $csc $cmdArgsPortable
if ($LASTEXITCODE -ne 0) { throw "Compilation of ClawGuardPortable.exe failed with exit code $LASTEXITCODE" }

# Mirror to root for local execution
Copy-Item -LiteralPath $exeTarget -Destination (Join-Path $repoRoot 'ClawGuard.exe') -Force
Copy-Item -LiteralPath $portableTarget -Destination (Join-Path $repoRoot 'ClawGuardPortable.exe') -Force

# 5. Generate Checksums and Zip packages
if (-not $SkipZip) {
    Write-Host "[3/3] Generating release packages & SHA-256 sums..." -ForegroundColor Green
    
    $cleanVer = $Version.TrimEnd('.0')
    if ($cleanVer.Split('.').Count -eq 2) { $cleanVer = "$cleanVer.0" }
    
    $zipMain = Join-Path $distDir "ClawGuard-v$cleanVer-win-x64.zip"
    $zipPortable = Join-Path $distDir "ClawGuardPortable-v$cleanVer-win-x64.zip"
    
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

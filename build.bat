@echo off
setlocal
echo ==========================================
echo   Building ClawGuard (Native C#)
echo ==========================================

set "FRAMEWORK=C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FRAMEWORK%\csc.exe" (
    set "FRAMEWORK=C:\Windows\Microsoft.NET\Framework\v4.0.30319"
)
if not exist "%FRAMEWORK%\csc.exe" (
    echo Error: .NET Framework csc.exe not found.
    exit /b 1
)

if not exist dist mkdir dist

set REFS=PresentationCore.dll,PresentationFramework.dll,WindowsBase.dll,System.Xaml.dll,System.Drawing.dll,System.Windows.Forms.dll,System.dll,System.Core.dll
set RES=/res:assets\claude_logo.png,ClawGuard.claude_logo.png /res:assets\claude_logo_red.png,ClawGuard.claude_logo_red.png
set OPTS=/nologo /target:winexe /optimize+ /debug- /platform:anycpu /win32icon:ClawGuard.ico /win32manifest:src\app.manifest %RES% /lib:"%FRAMEWORK%\WPF"

echo [1/2] Compiling ClawGuard.exe...
"%FRAMEWORK%\csc.exe" %OPTS% /out:dist\ClawGuard.exe /r:%REFS% src\*.cs
if %ERRORLEVEL% neq 0 (
    echo Compilation of ClawGuard.exe failed!
    exit /b %ERRORLEVEL%
)

echo [2/2] Compiling ClawGuardPortable.exe...
"%FRAMEWORK%\csc.exe" %OPTS% /out:dist\ClawGuardPortable.exe /r:%REFS% src\*.cs
if %ERRORLEVEL% neq 0 (
    echo Compilation of ClawGuardPortable.exe failed!
    exit /b %ERRORLEVEL%
)

copy /y dist\ClawGuard.exe ClawGuard.exe >nul
copy /y dist\ClawGuardPortable.exe ClawGuardPortable.exe >nul

echo Build succeeded! Binaries generated in dist\ and root.

if "%1"=="pack" goto do_pack
if "%1"=="--pack" goto do_pack
goto end

:do_pack
echo [Packaging Release Archives and SHA-256 Checksums...]
set "VER=2.5.2"
if not "%2"=="" set "VER=%2"
powershell -NoProfile -Command ^
    "Compress-Archive -Path dist\ClawGuard.exe, ClawGuard.ico, LICENSE -DestinationPath dist\ClawGuard-v%VER%-win-x64.zip -Force;" ^
    "Compress-Archive -Path dist\ClawGuardPortable.exe, LICENSE -DestinationPath dist\ClawGuardPortable-v%VER%-win-x64.zip -Force;" ^
    "$h = @(); Get-ChildItem dist -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | ForEach-Object { $h += ((Get-FileHash $_.FullName).Hash.ToLower() + '  ' + $_.Name) }; $h | Out-File dist\SHA256SUMS.txt -Encoding utf8 -Force"
echo Packaging completed in dist\

:end
endlocal

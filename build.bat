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

echo [1/2] Compiling ClawGuard.exe...
"%FRAMEWORK%\csc.exe" /nologo /target:winexe /win32icon:ClawGuard.ico /lib:"%FRAMEWORK%\WPF" /out:dist\ClawGuard.exe /r:%REFS% src\Program.cs
if %ERRORLEVEL% neq 0 (
    echo Compilation failed!
    exit /b %ERRORLEVEL%
)

echo [2/2] Compiling ClawGuardPortable.exe...
"%FRAMEWORK%\csc.exe" /nologo /target:winexe /win32icon:ClawGuard.ico /lib:"%FRAMEWORK%\WPF" /out:dist\ClawGuardPortable.exe /r:%REFS% src\Program.cs
if %ERRORLEVEL% neq 0 (
    echo Compilation failed!
    exit /b %ERRORLEVEL%
)

copy /y dist\ClawGuard.exe ClawGuard.exe >nul
copy /y dist\ClawGuardPortable.exe ClawGuardPortable.exe >nul

echo Build succeeded! Binaries generated in dist\ and root.
endlocal

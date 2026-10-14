# Contributing to ClawGuard

Thank you for your interest in improving ClawGuard! We welcome contributions, bug fixes, UI improvements, and feature proposals.

## Development Setup

1. **Prerequisites:**
   - Windows 10 or 11 (64-bit)
   - .NET Framework 4.8 (pre-installed on Windows 10/11)
   - Built-in Microsoft C# Compiler (`csc.exe`)

2. **Clone and Test:**
   ```powershell
   git clone https://github.com/GafarovMaxim/ClawGuard.git
   cd ClawGuard
   
   # Build native binaries
   .\build.bat
   
   # Run compiled executable
   .\dist\ClawGuard.exe
   ```

3. **Packaging Releases:**
   ```cmd
   build.bat pack
   ```

## Pull Request Guidelines

1. Fork the repo and create your feature branch: `git checkout -b feature/my-cool-feature`.
2. Keep UI additions consistent with the retro Claude-Code dark terminal aesthetic.
3. Ensure no blocking calls are made on the WPF UI thread. All network or external I/O must remain in background threads.
4. Verify that running `build.bat pack` completes cleanly without warnings or errors (0 warnings, 0 errors).
5. All contributions are licensed under the **GNU General Public License v3.0 (GPL-3.0)**.
6. Submit your pull request with a descriptive title and summary of changes.

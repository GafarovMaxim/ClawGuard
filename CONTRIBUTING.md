# Contributing to ClawGuard

Thank you for your interest in improving ClawGuard! We welcome contributions, bug fixes, UI improvements, and feature proposals.

## Development Setup

1. **Prerequisites:**
   - Windows 10 or 11 (64-bit)
   - PowerShell 5.1 or later
   - `ps2exe` module (`Install-Module ps2exe -Scope CurrentUser`)

2. **Clone and Test:**
   ```powershell
   git clone https://github.com/GafarovMaxim/ClawGuard.git
   cd ClawGuard
   
   # Run script directly in STA mode
   powershell -STA -File .\ClawGuardUI.ps1
   ```

3. **Building Executables:**
   ```powershell
   .\build.ps1
   ```

## Pull Request Guidelines

1. Fork the repo and create your feature branch: `git checkout -b feature/my-cool-feature`.
2. Keep UI additions consistent with the retro Claude-Code dark terminal aesthetic.
3. Ensure no blocking calls are made on the WPF UI thread. Any network or external I/O must remain in background threads.
4. Verify that running `.\build.ps1` completes cleanly without warnings or errors.
5. Submit your pull request with a descriptive title and summary of changes.

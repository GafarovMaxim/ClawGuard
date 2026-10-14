# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.5.2] - 2026-10-14

### Added
- **Dynamic Claude Companion Toggle:** When Claude Desktop is active/running, the launch button dynamically transforms into an active termination button (`🛑  Закрыть Claude` / `🛑  Close Claude`) with crimson alert styling, allowing instant 1-click termination. When inactive, it cleanly reverts to `🚀  Запустить Claude` / `🚀  Launch Claude`.
- **Strict Guard Start Validation:** Disabled the `[ ▶ Запустить ]` (Start Guard) button when current public IP is offline, null, syntactically invalid IPv4, or mismatched against the pinned IP, preventing invalid guard states.
- **Repository Rulesets & Integrity Tooling:** Added `.gitattributes` for cross-platform EOL normalization and binary protection, `.editorconfig` for consistent formatting, and `.github/dependabot.yml` for automated security scanning.

### Changed
- **GPL-3.0 Copyleft Licensing:** Migrated license from MIT to GNU General Public License v3.0 (GPL-3.0) across repository documentation, license files, and assembly metadata to safeguard distribution rights.
- **Crab Pixel Rendering & Mesh Texture Removal:** Replaced composite multi-Rectangle geometry rendering with a single direct `BitmapSource` using `NearestNeighbor` scaling and aliased edge mode, completely eliminating fractional subpixel grid lines and mesh artifacts across the crab sprite.

## [2.5.1] - 2026-10-11

### Added
- **100% Pure Native C# Architecture (`src/Program.cs`):** Completely migrated the codebase to native C# WPF targeting .NET Framework 4.8, eliminating all PowerShell script dependencies and wrappers.
- **Zero-Dependency 1-Click Build (`build.bat` & `build.ps1`):** Builds using Windows built-in `csc.exe` compiler without requiring PS2EXE or external modules.
- **Smart Portable Configuration:** `ClawGuardPortable.exe` automatically keeps `ClawGuard.json` in the application directory for true zero-install portability.

### Changed
- **VirusTotal Clean & Heuristic Hardening:** Completely resolved static ML heuristic false positives by removing low-level Win32 window-hooking P/Invokes, embedding application manifest (`src/app.manifest`) with DPI and Windows 10/11 compatibility, replacing raw Base64 strings with native `.rsrc` managed resources, and optimizing compilation (`/optimize+ /platform:anycpu`).
- **Embedded Multi-Resolution Tray Icon:** Cleanly extracts the 256x256 multi-layer crab icon from the executable's Win32 resources into the system tray without filesystem dependencies or shield icon fallbacks.
- **Pixel Art Cloud Elevation:** Repositioned right-hand cloud layer above the crab sleep animation to prevent sprite clipping.
- **Micro Footprint:** Executable binary size optimized to ~189 KB with instant cold boot.
- **Removed Deprecated Scripts:** Permanently deleted legacy PowerShell and PS2EXE compilation scripts.

## [2.5.0] - 2026-10-11

### Added
- **Native Asynchronous Network Engine (`NativeIPResolver`):** Pure C# multi-threaded resolver running directly on CLR ThreadPool without PowerShell ScriptBlock TLS dependencies, eliminating runtime host crashes.
- **Multi-Endpoint Geo-Redundant IP Resolution:** Automatic non-blocking fallback through `ipwho.is`, `ip-api.com`, `ipinfo.io`, and `api.ipify.org`.
- **4-Phase Smooth Gait Walking Cycle:** Fluid 7.4 FPS alternating leg walking animation with zero visual jerkiness or frame stutter.
- **Awake Sentinel Looking-Around Animation:** Dynamic sentinel behavior in alert mode featuring autonomous eye glances and head/horn shifts scanning perimeter and network health.
- **Named Single-Instance Mutex:** Global Windows kernel mutex (`Global\ClawGuard_SingleInstance_Mutex`) preventing duplicate processes.
- **Dispatcher CPU Throttling:** 0% CPU consumption in Windows notification tray when minimized.
- **Automated Build Pipeline (`build.ps1`):** Reproducible compilation producing release `.zip` packages and cryptographically verified `SHA256SUMS.txt`.
- **WPF Native Visual Capture:** Clean programmatic screenshot generation tool.

### Changed
- Rebranded project and all internal references entirely to **ClawGuard**.
- Replaced legacy synchronous WebClient blocking calls with timeout-guarded asynchronous workers.
- Upgraded DWM immersive titlebar hook to maintain clean custom typography and avoid Win32 caption distortion.

### Fixed
- Fixed critical `PSInvalidOperationException: ScriptBlock.GetContextFromTLS()` crash when compiled under PS2EXE hosts.
- Fixed `AddTypeCompilerError` caused by missing `System.Threading` and `System.Text.RegularExpressions` namespace references.
- Fixed 1-second status tick resetting crab walking stride.

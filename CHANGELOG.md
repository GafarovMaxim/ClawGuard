# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

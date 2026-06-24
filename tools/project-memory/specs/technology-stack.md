# Technology Stack

Last reviewed: 2026-06-24

Canonical source: this file
Linked from: `README.md`

This is project documentation. Keep business rules, feature algorithms, workflow
contracts, state machines, and verification guarantees in project memory; keep
stack facts, commands, runtime assumptions, and operational notes here.

## Summary

- Primary stack: .NET 8 Windows desktop app with WinUI 3 / Windows App SDK.
- Runtime model: local Windows GUI app that imports iPhone media over USB/MTP
  and can use a mock folder for smoke checks.
- Current confidence: verified from solution, project files, runbook, source
  entry points, packaging script, and tests on 2026-06-24.

## Components

| Layer | Technology | Evidence | Notes |
| --- | --- | --- | --- |
| Language/runtime | C# on .NET 8 | `iPhotoDropper.sln`, `src/iPhotoDropper.Core/iPhotoDropper.Core.csproj`, `src/iPhotoDropper.Infrastructure/iPhotoDropper.Infrastructure.csproj` | Core and Infrastructure target `net8.0`; nullable and implicit usings are enabled. |
| Desktop UI | WinUI 3 / Windows App SDK | `src/iPhotoDropper.App/iPhotoDropper.App.csproj`, `src/iPhotoDropper.App/Views/MainWindow.xaml` | App targets `net8.0-windows10.0.19041.0`, uses `Microsoft.WindowsAppSDK`, `UseWinUI`, and runs unpackaged. |
| App hosting | Microsoft Extensions Hosting, DI, logging | `src/iPhotoDropper.App/App.xaml.cs` | `Host.CreateDefaultBuilder` wires services, ViewModel, MainWindow, console logging, and daily file logging. |
| Device/media access | Windows MTP through `MediaDevices` | `src/iPhotoDropper.Infrastructure/iPhotoDropper.Infrastructure.csproj`, `IPhoneMtpDeviceService.cs`, `IPhoneMtpPhotoLibraryService.cs` | Detects Apple phone/tablet devices and scans supported photo/video extensions from storage/DCIM roots. |
| Mock device support | Local folder mock services | `src/iPhotoDropper.App/appsettings.json`, `MockUsbDeviceService.cs`, `MockPhotoLibraryService.cs`, `HybridUsbDeviceService.cs` | Default mock root is `C:\tmp\iPhotoDropperMockDevice`; mock fallback is controlled by `MockDevice:Enabled`. |
| Transfer/state storage | Filesystem copy plus JSON import history | `TransferService.cs`, `JsonTransferStateStore.cs`, `AppPaths.cs` | Runtime state lives under `%LOCALAPPDATA%\iPhotoDropper\state`; logs under `%LOCALAPPDATA%\iPhotoDropper\logs`. |
| Build/package | `dotnet build`, `dotnet publish`, Inno Setup | `tools/AGENT_RUNBOOK.md`, `tools/package/build-installer.ps1`, `packaging/inno/iPhotoDropper.iss` | Installer version currently follows app project version `0.1.2`; output goes to `artifacts\installer`. |
| Test/quality | xUnit, Microsoft.NET.Test.Sdk, coverlet collector | `tests/iPhotoDropper.Tests/iPhotoDropper.Tests.csproj`, `TransferServiceSmokeTests.cs` | Tests cover progress fallback, mock scan/import, duplicate skipping, conflict skip/replace, flat destination, timestamps, free-space failure, pause/resume/cancel, and stuck-read cleanup. |

## Commands

| Purpose | Command | Evidence |
| --- | --- | --- |
| Install workloads | `dotnet workload install microsoft-net-sdk-windowsdesktop`; `dotnet workload install microsoft-windowsappsdk` | `tools/AGENT_RUNBOOK.md` |
| Restore | `dotnet restore .\iPhotoDropper.sln` | `tools/AGENT_RUNBOOK.md`, `iPhotoDropper.sln` |
| Build | `dotnet build .\iPhotoDropper.sln` | `tools/AGENT_RUNBOOK.md`, `iPhotoDropper.sln` |
| Run debug app | `Start-Process ".\src\iPhotoDropper.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\iPhotoDropper.App.exe"` | `tools/AGENT_RUNBOOK.md`, app project target framework/runtime identifier |
| Test | `dotnet test .\iPhotoDropper.sln` | `tools/AGENT_RUNBOOK.md`, `tests/iPhotoDropper.Tests/iPhotoDropper.Tests.csproj` |
| Build installer | `.\tools\package\build-installer.ps1` | `tools/AGENT_RUNBOOK.md`, `tools/package/build-installer.ps1` |

## External Services

| Service | Role | Evidence | Boundary |
| --- | --- | --- | --- |
| iPhone/iPad over Windows MTP | Source media device connected by USB | `IPhoneMtpDeviceService.cs`, `IPhoneMtpPhotoLibraryService.cs` | Local device access only; user must trust the device in Windows/iOS. |
| Mock media folder | Local smoke-check source | `src/iPhotoDropper.App/appsettings.json`, `tools/AGENT_RUNBOOK.md` | Project-local testing convention; not a network service. |

## Runtime Data

| Data | Location | Evidence | Notes |
| --- | --- | --- | --- |
| Logs | `%LOCALAPPDATA%\iPhotoDropper\logs` | `AppPaths.cs`, `DailyFileLoggerProvider.cs`, runbook | Runtime output, not project memory. |
| Import state | `%LOCALAPPDATA%\iPhotoDropper\state\transfer-state.json` | `AppPaths.cs`, `JsonTransferStateStore.cs` | Tracks import history/state for runtime behavior. |
| Default import destination | `%USERPROFILE%\Pictures\iPhotoDropper` | `TransferViewModel.cs`, runbook | User media output. |

## Gaps

- No separate product requirements document was found; user-visible behavior is
  inferred from current UI, services, tests, and runbook.
- The runbook documents a smoke check but not a full first-run/default reset
  contract.
- The connected-projects register currently contains only the template and no
  active external project entries.

# iPhotoDropper

iPhotoDropper is a Windows desktop app for importing photos and videos from an
iPhone over USB/MTP into a local Windows folder. It is meant for a person who
wants a direct, visible transfer workflow without depending on cloud sync.

For AI agents: read [AGENTS.md](AGENTS.md) first.

## What It Does

- Detects connected Apple phones and tablets through Windows MTP.
- Can use a mock media folder for local smoke checks when `MockDevice:Enabled`
  is set in `src/iPhotoDropper.App/appsettings.json`.
- Scans supported media files from iPhone storage/DCIM roots.
- Lets the user filter photos, videos, "only new" imports, optional date
  folders, and maximum file size.
- Imports selected files into a destination folder, defaulting to
  `Pictures\iPhotoDropper`.
- Shows scan/import progress, current file progress, summary counters, and a
  short in-app log.
- Supports pause, resume, and cancel during scan/import flows.
- Copies through temporary files, checks destination free space, preserves source
  timestamps when available, and removes temporary files after failures or
  cancellation.

## Common Workflow

1. Connect and trust the iPhone in Windows.
2. Start the app.
3. Refresh devices if needed.
4. Click `Show media` / `Показать медиа` to scan.
5. Adjust filters and destination folder.
6. Click `Import` / `Импорт`.
7. Review the summary, destination pane, and logs.

## Stack

The canonical stack inventory is
[tools/project-memory/specs/technology-stack.md](tools/project-memory/specs/technology-stack.md).

Short version:

- .NET 8 solution with `Core`, `Infrastructure`, `App`, and xUnit test projects.
- WinUI 3 / Windows App SDK desktop UI targeting
  `net8.0-windows10.0.19041.0`.
- `MediaDevices` for Windows MTP device and media access.
- Microsoft Extensions hosting, dependency injection, and logging.
- Inno Setup packaging through `tools/package/build-installer.ps1`.

## Commands

Install required workloads:

```powershell
dotnet workload install microsoft-net-sdk-windowsdesktop
dotnet workload install microsoft-windowsappsdk
```

Restore, build, and run:

```powershell
dotnet restore .\iPhotoDropper.sln
dotnet build .\iPhotoDropper.sln
Start-Process ".\src\iPhotoDropper.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\iPhotoDropper.App.exe"
```

Run tests:

```powershell
dotnet test .\iPhotoDropper.sln
```

Build the installer:

```powershell
.\tools\package\build-installer.ps1
```

The installer output is expected at:

```text
artifacts\installer\iPhotoDropper-Setup-0.1.2.exe
```

## Runtime Data

The app stores local runtime data under:

```text
%LOCALAPPDATA%\iPhotoDropper\logs
%LOCALAPPDATA%\iPhotoDropper\state
%USERPROFILE%\Pictures\iPhotoDropper
```

These are user/runtime locations, not source-controlled project memory.

## Documentation Gaps

- The product purpose and user workflow are now documented from current source
  evidence, but there is no separate product requirements document.
- First-run/default reset behavior is not yet documented beyond the smoke check
  in [tools/AGENT_RUNBOOK.md](tools/AGENT_RUNBOOK.md).

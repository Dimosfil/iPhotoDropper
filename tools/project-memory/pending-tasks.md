# Pending Tasks

Use this file for active project-wide plans and multi-step work.

Keep entries concise and task-relevant. Do not store full diffs, large logs,
generated outputs, secrets, credentials, or private production data.

## Status Markers

- `[ ]` not started
- `[~]` in progress
- `[x]` done
- `[!]` blocked or needs attention

## iPhotoDropper MVP (working version)

Goal: Build a usable MVP desktop app for importing photo/video from iPhone over USB path with reliable import pipeline and recovery/logging, with a mock iPhone source for immediate local validation.

Planned changes:

- [x] Scaffolding: solution + three projects (Core, Infrastructure, Desktop App)
- [x] Domain contracts: device/media/transfer/hash/reporting models and interfaces
- [x] Infrastructure: mock USB and media source, duplicate registry, atomic transfer engine with retry + pause/resume/cancel
- [x] UI: WinUI 3 shell with device status, scan/import controls, media list, filters, destination picker, progress, and logs
- [x] Runtime wiring: DI, command handling, state/retry reporting
- [x] Documentation: install/run/build/check instructions in runbook
- [ ] Optional extension points: real iPhone service replacement without UI changes

Execution order:

- [x] 1) scaffold solution structure and shared DTO/contracts in Core
- [x] 2) implement infrastructure transfer pipeline and mock providers
- [x] 3) implement WinUI app shell + VM + command layer
- [x] 4) connect DI and run through full flow end-to-end
- [ ] 5) optional: add real iPhone backend adapter

Risks or dependencies:

- [ ] No Wi‑Fi transport by design (USB-only).
- [ ] Real iPhone integration is device/API dependent; initial release ships with mock source for stable local verification.
- [ ] Progress reliability and atomic file operations are validated by design; actual runtime behavior should be smoke-tested on target hardware.

Verification:

- [x] App builds with `dotnet build` from repo root.
- [x] Scan mock source path returns list and preview metadata.
- [x] Import writes `*.tmp` then atomic rename to final file path.
- [x] Pause/Resume/Cancel controls affect transfer state without crashes.
- [x] Summary report appears with copied/ skipped / failed counts and errors.

Current focus:

- [x] Confirm user-visible Russian strings are valid UTF-8; apparent mojibake was PowerShell output rendering.
- [x] Launch WinUI MVP against mock source.

Launch note:

- [x] App runs from `src/iPhotoDropper.App/bin/Debug/net8.0-windows10.0.19041.0/win-x64/iPhotoDropper.App.exe` with mock files in `C:\tmp\iPhotoDropperMockDevice`.
- [!] WinUI `ListView`/`ItemsControl`/`ScrollViewer` caused native `Microsoft.UI.Xaml.dll` crash `0xc000027b` on this machine; MVP UI temporarily renders media/log data as plain text blocks.

## Current UI pass

- [x] Show live connection status for a physically connected iPhone when Windows exposes it through MTP.
- [x] Rework the UI into two visible panes: left iPhone/media source, right destination/copy target.
- [x] Keep import progress visible during Start/Import without using crash-prone WinUI list controls.
- [x] Add scrollable file panes and scan/import log using read-only TextBox controls.
- [x] Log scan source, device, filters, raw count, filtered count, and sample file paths.
- [x] Add dynamic USB/MTP polling so UI status changes when iPhone connects, disconnects, or trust state changes.
- [x] Fix real iPhone detection: `MediaDevices.MediaDevice.Model` can throw before `Connect()`, so device properties must be read defensively.
- [x] Scan iPhone media under `\Internal Storage` as well as `\Internal Storage\DCIM`; Explorer on the target machine shows dated folders directly under Internal Storage.
- [x] Make MTP file metadata reads defensive; invalid iPhone timestamps must not abort scanning.
- [x] Serialize all Windows MTP access behind a shared lock so background device polling cannot collide with scan/import and trigger `0x802A0002`.
- [!] Real iPhone media access now uses `MediaDevices`/Windows Portable Devices. If the phone is locked or trust is not accepted, the UI will show no MTP access and fall back to mock only when no iPhone is visible.
- [!] After a real iPhone has been seen in the session, disconnecting it leaves the UI in "no device" state instead of switching back to mock.

## Import progress visibility

- [x] Add visible current-file and overall import progress bars near the destination/copy controls.
- [x] Keep completed progress visible instead of resetting immediately to 0%.
- [x] Run scan/import work off the UI thread while dispatching only UI updates back to WinUI.
- [x] Verify build/tests after the progress UI change.

## Apple-style UI polish

- [x] Restyle the main WinUI screen with a quiet Apple-like light theme, compact panels, and clearer hierarchy.
- [x] Keep the stable TextBox-based lists/logs to avoid the known native WinUI list crash.
- [x] Keep startup and destination-folder refresh off the UI thread to avoid "Not responding" during MTP discovery/import.
- [x] Verify build/tests after XAML changes.

## Existing file conflict handling

- [x] Detect destination file conflicts against real files already in the selected folder.
- [x] Ask whether to replace or skip, with a "for all" checkbox.
- [x] Apply replace/skip decisions in the transfer service without creating surprise duplicate names.
- [x] Verify conflict behavior with tests and build.

## Import UI responsiveness

- [x] Throttle high-frequency copy progress updates during large file transfers.
- [x] Coalesce WinUI refresh calls so one progress tick does not queue many full screen updates.
- [x] Verify app stays responsive after launch and build/tests pass.

## Import summary and stable progress UI

- [x] Show clear import totals: found, selected, copied, skipped, failed, and bytes copied.
- [x] Keep progress labels and text bars at stable dimensions so Start does not make the layout jump.
- [x] Verify build/tests after the summary and layout changes.

## Destination-scoped duplicate detection

- [x] Treat "copy only new" as duplicate detection within the selected destination folder, not globally per iPhone.
- [x] Ignore stale queued progress updates after an import completes so final totals stay visible.
- [x] Verify build/tests after the duplicate detection fix.

## Flat destination import

- [x] Import into the selected destination folder directly instead of creating date/month subfolders by default.
- [x] Use actual destination files for duplicate skips so previous subfolder imports do not block a flat retry.
- [x] Verify build/tests after the flat import change.

## Windows installer

- [x] Add an Inno Setup installer script for the WinUI desktop app.
- [x] Add a project-local packaging command that publishes the app and compiles the installer.
- [x] Verify the installer build with local Inno Setup.

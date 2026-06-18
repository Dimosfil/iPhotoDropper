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

## Real-device-only default mode

- [x] Disable automatic mock iPhone fallback in normal app startup.
- [x] Disable import controls while no real device is connected.
- [x] Verify build/tests after the device gating change.
- [x] Verify app stays responsive after launch and build/tests pass.

## Import summary and stable progress UI

- [x] Show clear import totals: found, selected, copied, skipped, failed, and bytes copied.
- [x] Keep progress labels and text bars at stable dimensions so Start does not make the layout jump.
- [x] Verify build/tests after the summary and layout changes.

## Destination-scoped duplicate detection

- [x] Treat "copy only new" as duplicate detection within the selected destination folder, not globally per iPhone.
- [x] Ignore stale queued progress updates after an import completes so final totals stay visible.
- [x] Verify build/tests after the duplicate detection fix.

## Streaming media scan UI

- [x] Add per-item scan progress from media libraries so "Show media" can populate the window while MTP enumeration is still running.
- [x] Keep scan work off the UI thread and marshal only collection/status updates through the WinUI dispatcher.
- [x] Verify build/tests after the streaming scan change.

## Flat destination import

- [x] Import into the selected destination folder directly instead of creating date/month subfolders by default.
- [x] Use actual destination files for duplicate skips so previous subfolder imports do not block a flat retry.
- [x] Verify build/tests after the flat import change.

## iTunes-style UI refresh

Goal: Rework the WinUI shell toward an iTunes-style desktop utility: quiet toolbar, source sidebar, scrollable panes, resizable window, clearer import controls, and stable progress/status surfaces.

- [x] Replace the two-card layout with a toolbar/sidebar/content structure while preserving the crash-safe TextBox-based media/log views.
- [x] Add responsive sizing/initial window sizing so the app can be resized without clipping primary controls.
- [x] Keep stable text progress bars after WinUI `ProgressBar` reproduced the native startup crash.
- [x] Verify build/tests after UI and window changes.

## Windows installer

- [x] Add an Inno Setup installer script for the WinUI desktop app.
- [x] Add a project-local packaging command that publishes the app and compiles the installer.
- [x] Verify the installer build with local Inno Setup.

## Device session reset

- [x] Clear old phone media/progress/report state when the selected device changes.
- [x] Clear previous import state at the start of a new scan.
- [x] Treat serial/manufacturer/transport changes as device changes in USB polling.
- [x] Verify build/tests and restart the app.

## Preserve original file dates on import

- [x] Carry source filesystem timestamps through scanned media metadata.
- [x] Apply source timestamps to imported destination files after successful copy.
- [x] Add regression coverage for preserved destination timestamps.
- [x] Verify build/tests after the timestamp preservation change.

## Persistent app logs

- [x] Write application/runtime log messages to a durable local log folder.
- [x] Show the log folder path in the WinUI app.
- [x] Verify build/tests after logging changes.

## Import confirmation

- [x] Show a Yes/No confirmation dialog before import starts.
- [x] Include destination free space and required selected media size in the dialog.
- [x] Verify build/tests after confirmation changes.

## Open log folder button

- [x] Add a UI button near the log folder path that opens the logs folder in Explorer.
- [x] Verify build/tests after the log-folder button change.

## MTP scan root fallback

- [x] Do not stop iPhone scanning at the first existing `DCIM` folder.
- [x] Scan storage-root and DCIM candidates with duplicate path filtering.
- [x] Verify build/tests and restart the app.

## Scan busy UI

- [x] Add explicit scanning state separate from generic busy/import state.
- [x] Show scan progress indicators while media discovery is running.
- [x] Disable scan/refresh buttons immediately during scanning.
- [x] Verify build/tests and restart the app.

## Installer publish resources

- [x] Ensure `gi install` creates a runnable publish output before compiling the installer.
- [x] Copy required WinUI `.xbf` and app `.pri` resources into publish output when `dotnet publish --output` omits them.
- [x] Rebuild installer and verify publish/installed launch.

## Import hang prevention

- [x] Keep import progress UI responsive during large real-device transfers.
- [x] Avoid rebuilding large media/log/destination text panes on every copy progress tick.
- [x] Add a per-file no-progress read watchdog so a stuck MTP stream fails the file and continues.
- [x] Add regression coverage and rebuild installer.

## Destination free-space preflight

- [x] Check destination disk free space before starting import.
- [x] Show a clear not-enough-space error instead of silently hanging or copying until failure.
- [x] Add regression coverage, bump patch version, and rebuild installer.

## Scan pause/cancel controls

- [x] Route Pause/Resume/Cancel to the active scan when media discovery is running.
- [x] Wake paused scans before cancellation so the scan task can observe its cancellation token.
- [x] Verify build/tests after scan control routing.

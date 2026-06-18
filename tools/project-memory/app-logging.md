# App Logging

## Behavior

iPhotoDropper writes application logs to a durable per-user folder:

```text
%LOCALAPPDATA%\iPhotoDropper\logs
```

The active daily log file uses this name pattern:

```text
iPhotoDropper-yyyyMMdd.log
```

The WinUI shell shows the log folder path above the on-screen journal so a user
can find diagnostic files without inspecting project files or source code.

## Logged Events

The file logger is connected to the standard `Microsoft.Extensions.Logging`
pipeline, so existing app, device, scan, import, MTP, and transfer service
messages are written to disk. The visible in-app journal messages are also
forwarded through `ILogger<TransferViewModel>` before they are inserted into the
screen log.

Each line includes local timestamp, log level, logger category, optional event
id, message, and exception details when present.

## Current Implementation Map

- `src/iPhotoDropper.App/AppPaths.cs` defines local data, log, and state
  folders.
- `src/iPhotoDropper.App/Logging/DailyFileLoggerProvider.cs` appends log lines
  to the current daily file.
- `src/iPhotoDropper.App/App.xaml.cs` registers the file logger and reuses
  `AppPaths` for transfer state storage.
- `src/iPhotoDropper.App/ViewModels/TransferViewModel.cs` exposes
  `LogFolderPath` to the UI and forwards visible log entries to `ILogger`.
- `src/iPhotoDropper.App/Views/MainWindow.xaml` and `.xaml.cs` display the log
  folder path above the journal.

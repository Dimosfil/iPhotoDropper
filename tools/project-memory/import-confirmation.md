# Import Confirmation

## Behavior

Before starting an import, the WinUI app asks the user to confirm the operation
with a `Yes/No` dialog.

The confirmation dialog shows:

- Destination folder.
- Selected file count.
- Required size for the selected media.
- Available free space on the destination drive.

If the destination drive reports less available space than the selected media
requires, the dialog still shows the information but disables the `Yes` action.
The transfer service also keeps its own free-space checks before and during
copying, because disk space can change after the dialog is confirmed.

## Current Implementation Map

- `src/iPhotoDropper.App/Views/MainWindow.xaml.cs` runs
  `ConfirmImportAsync()` from `OnImportClick()` before calling
  `TransferViewModel.ImportAsync()`.
- `TryGetAvailableFreeSpace()` resolves the destination drive and reads
  `DriveInfo.AvailableFreeSpace`.
- `src/iPhotoDropper.Infrastructure/Services/TransferService.cs` remains the
  authoritative runtime guard for disk-space failures.

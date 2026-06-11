# Agent Runbook

Every command should be copy-pasteable from the project root.

## Install

```powershell
dotnet workload install microsoft-net-sdk-windowsdesktop
dotnet workload install microsoft-windowsappsdk
```

## Run

```powershell
dotnet restore .\iPhotoDropper.sln
dotnet build .\iPhotoDropper.sln
Start-Process ".\src\iPhotoDropper.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\iPhotoDropper.App.exe"
```

## Test

```powershell
dotnet test .\iPhotoDropper.sln
```

## Build

```powershell
dotnet build .\iPhotoDropper.sln
```

## Smoke Check

```powershell
New-Item -ItemType Directory -Force "C:\tmp\iPhotoDropperMockDevice" | Out-Null
# Add jpg/png/mp4/mov files into C:\tmp\iPhotoDropperMockDevice
dotnet build .\iPhotoDropper.sln
Start-Process ".\src\iPhotoDropper.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\iPhotoDropper.App.exe"
```

Expected result:

```text
- Mock устройство отображается в UI
- Scan возвращает список файлов из C:\tmp\iPhotoDropperMockDevice
- Import завершился без сбоев и сохранил файлы в My Pictures\iPhotoDropper
- Логи и итоговый отчет не пустые
```

## Logs

```powershell
Get-ChildItem "$env:LOCALAPPDATA\iPhotoDropper\state"
Get-ChildItem "$env:USERPROFILE\Pictures\iPhotoDropper" -Recurse
```

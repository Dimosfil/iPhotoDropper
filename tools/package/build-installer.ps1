[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "0.1.0",
    [string]$InnoCompiler
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-FullPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    return [System.IO.Path]::GetFullPath($Path)
}

function Assert-UnderRoot {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $rootPath = Get-FullPath $Root
    $candidatePath = Get-FullPath $Path
    if (-not $candidatePath.StartsWith($rootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label must stay inside the project root: $candidatePath"
    }
}

function Find-InnoCompiler {
    param([string]$ExplicitPath)

    if ($ExplicitPath) {
        if (-not (Test-Path -LiteralPath $ExplicitPath -PathType Leaf)) {
            throw "Inno Setup compiler was not found at: $ExplicitPath"
        }

        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $userCompiler = Join-Path $env:USERPROFILE ".codex\bin\ISCC.exe"
    if (Test-Path -LiteralPath $userCompiler -PathType Leaf) {
        return (Resolve-Path -LiteralPath $userCompiler).Path
    }

    $pathCommand = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($pathCommand) {
        return $pathCommand.Source
    }

    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 5\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 5\ISCC.exe")
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "ISCC.exe was not found. Add Inno Setup to PATH or pass -InnoCompiler 'C:\Path\To\ISCC.exe'."
}

$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
$appProject = Join-Path $projectRoot "src\iPhotoDropper.App\iPhotoDropper.App.csproj"
$innoScript = Join-Path $projectRoot "packaging\inno\iPhotoDropper.iss"
$publishDir = Join-Path $projectRoot "artifacts\publish\iPhotoDropper\$Runtime"
$installerDir = Join-Path $projectRoot "artifacts\installer"
$compiler = Find-InnoCompiler -ExplicitPath $InnoCompiler

Assert-UnderRoot -Root $projectRoot -Path $publishDir -Label "Publish directory"
Assert-UnderRoot -Root $projectRoot -Path $installerDir -Label "Installer directory"

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $publishDir, $installerDir | Out-Null

dotnet publish $appProject `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --output $publishDir `
    /p:WindowsPackageType=None `
    /p:PublishSingleFile=false `
    /p:PublishReadyToRun=false

& $compiler `
    "/DAppVersion=$Version" `
    "/DSourceDir=$publishDir" `
    "/DOutputDir=$installerDir" `
    $innoScript

$setupPath = Join-Path $installerDir "iPhotoDropper-Setup-$Version.exe"
if (-not (Test-Path -LiteralPath $setupPath -PathType Leaf)) {
    throw "Installer build finished, but setup file was not found: $setupPath"
}

Write-Host "Installer created: $setupPath"

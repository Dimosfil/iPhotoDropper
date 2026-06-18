[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version,
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

function Get-ProjectProperty {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$Name
    )

    [xml]$projectXml = Get-Content -LiteralPath $ProjectPath -Raw
    $value = $projectXml.Project.PropertyGroup |
        ForEach-Object { $_.$Name } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1

    if (-not $value) {
        throw "Project property '$Name' was not found in: $ProjectPath"
    }

    return [string]$value
}

function Copy-WinUIResourceOutput {
    param(
        [Parameter(Mandatory = $true)][string]$BuildOutputDir,
        [Parameter(Mandatory = $true)][string]$PublishOutputDir,
        [Parameter(Mandatory = $true)][string]$AppPriFileName
    )

    if (-not (Test-Path -LiteralPath $BuildOutputDir -PathType Container)) {
        throw "Build output directory was not found: $BuildOutputDir"
    }

    $resourcesToCopy = @(Get-ChildItem -LiteralPath $BuildOutputDir -Recurse -File -Filter "*.xbf")
    $appPriPath = Join-Path $BuildOutputDir $AppPriFileName
    if (Test-Path -LiteralPath $appPriPath -PathType Leaf) {
        $resourcesToCopy += Get-Item -LiteralPath $appPriPath
    }

    if ($resourcesToCopy.Count -eq 0) {
        throw "No WinUI XAML resources were found in build output: $BuildOutputDir"
    }

    foreach ($resource in $resourcesToCopy) {
        $relativePath = $resource.FullName.Substring($BuildOutputDir.Length).TrimStart("\")
        $destination = Join-Path $PublishOutputDir $relativePath
        $destinationDir = Split-Path -Parent $destination
        New-Item -ItemType Directory -Force -Path $destinationDir | Out-Null
        Copy-Item -LiteralPath $resource.FullName -Destination $destination -Force
    }

    $requiredResources = @(
        "App.xbf",
        $AppPriFileName
    )

    foreach ($requiredResource in $requiredResources) {
        $requiredPath = Join-Path $PublishOutputDir $requiredResource
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Required WinUI publish resource is missing: $requiredPath"
        }
    }
}

function Convert-ToAssemblyVersion {
    param([Parameter(Mandatory = $true)][string]$Version)

    $numericVersion = ($Version -split "[-+]")[0]
    $parts = @($numericVersion -split "\.")
    if ($parts.Count -lt 3 -or $parts.Count -gt 4) {
        throw "Version must have three or four numeric parts, for example 0.1.1: $Version"
    }

    foreach ($part in $parts) {
        if ($part -notmatch "^\d+$") {
            throw "Version contains a non-numeric part: $Version"
        }
    }

    while ($parts.Count -lt 4) {
        $parts += "0"
    }

    return ($parts -join ".")
}

$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
$appProject = Join-Path $projectRoot "src\iPhotoDropper.App\iPhotoDropper.App.csproj"
$innoScript = Join-Path $projectRoot "packaging\inno\iPhotoDropper.iss"
$targetFramework = Get-ProjectProperty -ProjectPath $appProject -Name "TargetFramework"
$projectVersion = Get-ProjectProperty -ProjectPath $appProject -Name "Version"
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $projectVersion
}

$assemblyVersion = Convert-ToAssemblyVersion -Version $Version
$assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($appProject)
$buildOutputDir = Join-Path $projectRoot "src\iPhotoDropper.App\bin\$Configuration\$targetFramework\$Runtime"
$publishDir = Join-Path $projectRoot "artifacts\publish\iPhotoDropper\$Runtime"
$installerDir = Join-Path $projectRoot "artifacts\installer"
$compiler = Find-InnoCompiler -ExplicitPath $InnoCompiler

Assert-UnderRoot -Root $projectRoot -Path $buildOutputDir -Label "Build output directory"
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
    /p:Version=$Version `
    /p:AssemblyVersion=$assemblyVersion `
    /p:FileVersion=$assemblyVersion `
    /p:PublishSingleFile=false `
    /p:PublishReadyToRun=false

Copy-WinUIResourceOutput `
    -BuildOutputDir $buildOutputDir `
    -PublishOutputDir $publishDir `
    -AppPriFileName "$assemblyName.pri"

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

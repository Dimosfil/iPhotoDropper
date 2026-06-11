#ifndef AppVersion
#define AppVersion "0.1.0"
#endif

#ifndef SourceDir
#define SourceDir "..\..\artifacts\publish\iPhotoDropper\win-x64"
#endif

#ifndef OutputDir
#define OutputDir "..\..\artifacts\installer"
#endif

[Setup]
AppId={{8FAD411E-5CB9-4F37-9C4C-929F3C269AA8}
AppName=iPhotoDropper
AppVersion={#AppVersion}
AppPublisher=iPhotoDropper
DefaultDirName={localappdata}\Programs\iPhotoDropper
DefaultGroupName=iPhotoDropper
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=iPhotoDropper-Setup-{#AppVersion}
UninstallDisplayIcon={app}\iPhotoDropper.App.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\iPhotoDropper"; Filename: "{app}\iPhotoDropper.App.exe"
Name: "{group}\Uninstall iPhotoDropper"; Filename: "{uninstallexe}"
Name: "{autodesktop}\iPhotoDropper"; Filename: "{app}\iPhotoDropper.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\iPhotoDropper.App.exe"; Description: "{cm:LaunchProgram,iPhotoDropper}"; Flags: nowait postinstall skipifsilent

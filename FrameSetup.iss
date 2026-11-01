; Inno Setup Script for Frame
; Written for Inno Setup 5 / 6

#define MyAppName "Frame"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Frame"
#define MyAppExeName "Frame.exe"

[Setup]
AppId={{6B5D7A9E-3E4F-4C91-9C81-817D62E1A1B2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=installer_output
OutputBaseFilename=FrameSetup_v{#MyAppVersion}
SetupIconFile=app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#MyAppExeName}
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "associate_media"; Description: "Associate with common media files (.mp4, .mp3, .wav, .mkv, .avi)"; GroupDescription: "File Associations:"; Flags: unchecked

[Files]
; Packages the self-contained build files (includes .NET runtime and native media components)
Source: "publish-selfcontained\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: If distributing the lightweight framework-dependent build instead, change the Source path above to:
; Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; File associations for common media formats when the task is selected
Root: HKA; Subkey: "Software\Classes\FrameMediaFile"; ValueType: string; ValueName: ""; ValueData: "Media File"; Flags: uninsdeletekey; Tasks: associate_media
Root: HKA; Subkey: "Software\Classes\FrameMediaFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: associate_media
Root: HKA; Subkey: "Software\Classes\FrameMediaFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: associate_media

Root: HKA; Subkey: "Software\Classes\.mp4\OpenWithProgids"; ValueType: string; ValueName: "FrameMediaFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate_media
Root: HKA; Subkey: "Software\Classes\.mkv\OpenWithProgids"; ValueType: string; ValueName: "FrameMediaFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate_media
Root: HKA; Subkey: "Software\Classes\.mp3\OpenWithProgids"; ValueType: string; ValueName: "FrameMediaFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate_media
Root: HKA; Subkey: "Software\Classes\.wav\OpenWithProgids"; ValueType: string; ValueName: "FrameMediaFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate_media
Root: HKA; Subkey: "Software\Classes\.avi\OpenWithProgids"; ValueType: string; ValueName: "FrameMediaFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate_media
Root: HKA; Subkey: "Software\Classes\.wmv\OpenWithProgids"; ValueType: string; ValueName: "FrameMediaFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate_media

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent


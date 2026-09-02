; =====================================================================
; RAMAVERSE STUDIO • Professional Windows Inno Setup Script
; Generates RamaverseStudio-Setup-v1.2.0.exe
; =====================================================================

#define MyAppName "Ramaverse Studio"
#define MyAppVersion "1.2.0"
#define MyAppPublisher "Ramaverse"
#define MyAppURL "https://github.com/jaimin229/ramaverse-studio"
#define MyAppExeName "RamaverseStudio.exe"

[Setup]
AppId={{D814FA7F-8344-4E32-9F1B-94DF58BA6A1C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\RamaverseStudio
DisableProgramGroupPage=yes
OutputBaseFilename=RamaverseStudio-Setup-v{#MyAppVersion}
OutputDir=..\dist
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
CloseApplications=yes
RestartApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\worldwide\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

#define MyAppName "QuizScore Live"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "AMIT KUMAR UPADHYAY"
#define MyAppURL "https://github.com/amitkumarupadhyay1/Score"
#define MyAppExeName "Score.exe"

[Setup]
AppId={{8E9C9E4A-7E91-4A61-9F42-1C3D4AA4E0AB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\QuizScore Live
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\installer-output
OutputBaseFilename=ScoreSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x86 x64
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\Score\bin\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsDotNetDetected('4.7.2', 0) then
  begin
    MsgBox('QuizScore Live requires .NET Framework 4.7.2 or later. Please install it from Microsoft, then run this installer again.', mbInformation, MB_OK);
    Result := False;
  end;
end;

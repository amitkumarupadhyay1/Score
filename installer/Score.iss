#define MyAppName "QuizScore Live"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
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
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupLogging=yes
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} installer
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\Score\bin\Release\*"; DestDir: "{app}"; Excludes: "*.pdb,*.xml,EntityFramework.SqlServer.*"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
function IsDotNet472OrLaterInstalled(): Boolean;
var
  ReleaseValue: Cardinal;
begin
  if IsWin64 then
    Result := RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', ReleaseValue)
      and (ReleaseValue >= 461808)
  else
    Result := RegQueryDWordValue(HKLM32, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', ReleaseValue)
      and (ReleaseValue >= 461808);
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsDotNet472OrLaterInstalled() then
  begin
    MsgBox('QuizScore Live requires .NET Framework 4.7.2 or later. Please install it from Microsoft, then run this installer again.', mbInformation, MB_OK);
    Result := False;
  end;
end;

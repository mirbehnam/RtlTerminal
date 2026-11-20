#define AppName "Rtl Terminal"
#ifndef AppVersion
  #error AppVersion must be supplied by build-release.ps1 or the release workflow
#endif
#define AppPublisher "behnamapps"
#define AppExeName "RtlTerminal.exe"
#ifndef PublishDirectory
  #define PublishDirectory "..\artifacts\publish\win-x64"
#endif

[Setup]
AppId={{A5BC0F01-F4CD-49C4-B85F-8B88ACDC4416}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\behnamapps\Rtl Terminal
DefaultGroupName=Rtl Terminal
UninstallDisplayName=Rtl Terminal
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=..\artifacts\release
OutputBaseFilename=RtlTerminal-Setup-{#AppVersion}-x64
SetupIconFile=..\src\RtlTerminal\Assets\RtlTerminal.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
MinVersion=10.0.19041
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#PublishDirectory}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDirectory}\ConPty\*"; DestDir: "{app}\ConPty"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PublishDirectory}\Assets\Fonts\*"; DestDir: "{app}\Assets\Fonts"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Rtl Terminal"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{%USERPROFILE}"
Name: "{group}\Uninstall Rtl Terminal"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Rtl Terminal"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{%USERPROFILE}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; WorkingDir: "{%USERPROFILE}"; Description: "Launch Rtl Terminal"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[UninstallRun]
Filename: "{sys}\reg.exe"; Parameters: "delete ""HKCU\Software\Classes\Directory\shell\RtlTerminal"" /f"; Flags: runhidden
Filename: "{sys}\reg.exe"; Parameters: "delete ""HKCU\Software\Classes\Directory\Background\shell\RtlTerminal"" /f"; Flags: runhidden

[Code]
const
  PreferencesKey = 'Software\RtlTerminal';
  AutoHideMenuOption = 0;
  RememberDirectoriesOption = 1;

var
  PreferencesPage: TInputOptionWizardPage;
  InitialRememberDirectories: Boolean;
  PreferencesPageVisited: Boolean;

procedure InitializeWizard;
var
  Value: Cardinal;
begin
  InitialRememberDirectories := True;
  if RegQueryDWordValue(HKCU, PreferencesKey, 'DirectoryHistoryEnabled', Value) then
    InitialRememberDirectories := Value <> 0;

  PreferencesPage := CreateInputOptionPage(wpSelectTasks,
    'Terminal preferences', 'Choose how Rtl Terminal should start',
    'You can change these preferences later in Rtl Terminal settings.', False, False);
  PreferencesPage.Add('Automatically hide the menu bar');
  PreferencesPage.Add('Remember visited directories');
  PreferencesPage.Values[AutoHideMenuOption] := False;
  PreferencesPage.Values[RememberDirectoriesOption] := InitialRememberDirectories;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = PreferencesPage.ID then
    PreferencesPageVisited := True;
end;

function SavePreference(const Name: String; Enabled: Boolean): Boolean;
var
  Value: String;
  ResultCode: Integer;
begin
  if Enabled then Value := '1' else Value := '0';
  Result := False;
  try
    // Write to the account that launched Setup, even when elevation uses a different account.
    if ExecAsOriginalUser(ExpandConstant('{sys}\reg.exe'),
      'add "HKCU\' + PreferencesKey + '" /v ' + Name + ' /t REG_DWORD /d ' + Value + ' /f',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      Result := ResultCode = 0;
  except
    Log(GetExceptionMessage);
  end;
  if not Result then
    Log('Could not save terminal preference: ' + Name);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Saved: Boolean;
begin
  if (CurStep <> ssPostInstall) or WizardSilent or not PreferencesPageVisited then
    Exit;

  // Applying either checkbox state makes the displayed Auto Hide choice authoritative.
  Saved := SavePreference('AutoHideMenu', PreferencesPage.Values[AutoHideMenuOption]);
  if PreferencesPage.Values[RememberDirectoriesOption] <> InitialRememberDirectories then
    if not SavePreference('DirectoryHistoryEnabled', PreferencesPage.Values[RememberDirectoriesOption]) then
      Saved := False;
  if not Saved then
    MsgBox('Setup could not save one or more preferences. You can set them in Rtl Terminal after it starts.',
      mbInformation, MB_OK);
end;

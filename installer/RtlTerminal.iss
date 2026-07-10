#define AppName "Rtl Terminal"
#define AppVersion "1.0.0"
#define AppPublisher "behnamapps"
#define AppExeName "RtlTerminal.exe"
#define PublishDirectory "..\publish\win-x64"

[Setup]
AppId={{A5BC0F01-F4CD-49C4-B85F-8B88ACDC4416}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\behnamapps\Rtl Terminal
DefaultGroupName=Rtl Terminal
UninstallDisplayName=Rtl Terminal
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=..\release
OutputBaseFilename=RtlTerminal-Setup-{#AppVersion}-x64
SetupIconFile=..\RtlTerminal.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#PublishDirectory}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Rtl Terminal"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall Rtl Terminal"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Rtl Terminal"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch Rtl Terminal"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[UninstallRun]
Filename: "{sys}\reg.exe"; Parameters: "delete ""HKCU\Software\Classes\Directory\shell\RtlTerminal"" /f"; Flags: runhidden
Filename: "{sys}\reg.exe"; Parameters: "delete ""HKCU\Software\Classes\Directory\Background\shell\RtlTerminal"" /f"; Flags: runhidden

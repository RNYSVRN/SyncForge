; Installationsprogramm für SyncForge (Inno Setup 6).
; Wird von tools\Build-Release.ps1 aufgerufen, das vorher die App veröffentlicht und die Version übergibt:
;   ISCC.exe /DAppVersion=0.4.0 /DPublishDir=..\artifacts\publish\x64 installer\SyncForge.iss
;
; Installiert pro Benutzer (ohne Administratorrechte) nach %LOCALAPPDATA%\Programs\SyncForge.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\release"
#endif

[Setup]
; Die AppId nie ändern: Daran erkennt Windows Updates und die Deinstallation.
AppId={{7E14F12D-37BA-41B5-8E1B-9575CD29B50F}
AppName=SyncForge
AppVersion={#AppVersion}
AppVerName=SyncForge {#AppVersion}
AppPublisher=Ronny Severin
VersionInfoVersion={#AppVersion}
VersionInfoProductName=SyncForge
DefaultDirName={autopf}\SyncForge
DefaultGroupName=SyncForge
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir={#OutputDir}
OutputBaseFilename=SyncForge-{#AppVersion}-x64-Setup
SetupIconFile=..\src\SyncForge\Assets\SyncForge.ico
UninstallDisplayIcon={app}\SyncForge.exe
UninstallDisplayName=SyncForge
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; SyncForge wird vor der Installation selbst beendet (siehe [Code]), läuft es doch noch, fragt Windows nach.
CloseApplications=yes
RestartApplications=no
LanguageDetectionMethod=uilanguage
ShowLanguageDialog=auto

[Languages]
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
de.DeleteData=Sollen auch deine Einstellungen, Backup-Profile und die Historie von SyncForge gelöscht werden?%n%nDeine gesicherten Dateien in den Zielordnern bleiben in jedem Fall erhalten.
en.DeleteData=Do you also want to delete your SyncForge settings, backup profiles and history?%n%nYour backed-up files in the destination folders are kept in any case.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\SyncForge"; Filename: "{app}\SyncForge.exe"
Name: "{autodesktop}\SyncForge"; Filename: "{app}\SyncForge.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SyncForge.exe"; Description: "{cm:LaunchProgram,SyncForge}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Benachrichtigungs-Registrierung und Autostart-Eintrag entfernen.
Filename: "{app}\SyncForge.exe"; Parameters: "--uninstall-cleanup"; Flags: runhidden waituntilterminated; RunOnceId: "Cleanup"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
procedure StopSyncForge;
var
  ResultCode: Integer;
begin
  // Läuft SyncForge (auch nur im Infobereich), würden Dateien in Benutzung sein.
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM SyncForge.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopSyncForge;
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopSyncForge;
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // Daten nur auf ausdrücklichen Wunsch löschen (bei stiller Deinstallation nie).
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent then
    if MsgBox(CustomMessage('DeleteData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      DelTree(ExpandConstant('{localappdata}\SyncForge'), True, True, True);
end;

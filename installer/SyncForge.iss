; Installer for SyncForge (Inno Setup 6)
; Build: powershell -ExecutionPolicy Bypass -File installer\build.ps1
; Expects the published app in ..\dist\publish (see build.ps1).

#define AppName      "SyncForge"
#define AppExe       "SyncForge.exe"
#define AppPublisher "Ronny Severin"
#define AppUrl       "https://github.com/RNYSVRN/SyncForge"
#ifndef PublishDir
  #define PublishDir "..\dist\publish"
#endif
#ifndef OutputDir
  #define OutputDir  "..\dist"
#endif
#define VerMajor
#define VerMinor
#define VerRev
#define VerBuild
#expr GetVersionComponents(PublishDir + "\" + AppExe, VerMajor, VerMinor, VerRev, VerBuild)
#define AppVersion   Str(VerMajor) + "." + Str(VerMinor) + "." + Str(VerRev)

[Setup]
; Fixed AppId: stays the same across versions so updates replace the previous installation
AppId={{7E14F12D-37BA-41B5-8E1B-9575CD29B50F}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=SyncForge-Setup-{#AppVersion}
SetupIconFile=..\src\SyncForge\Assets\SyncForge.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
WizardStyle=modern
; SyncForge runs as the signed-in user and needs no admin rights: install for the current user by default
; (%LocalAppData%\Programs), optionally for all users ("Program Files").
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; WinUI 3 app: Windows 11 (or Windows 10 21H2 build 22000) and later
MinVersion=10.0.22000
; SyncForge is stopped before update/uninstall (see [Code]); if it is still running, Windows asks
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"

[CustomMessages]
en.LaunchApp=Start {#AppName}
de.LaunchApp={#AppName} starten
en.DeleteData=Do you also want to delete your SyncForge settings, backup profiles and history?%n%nYour backed-up files in the destination folders are kept in any case.
de.DeleteData=Sollen auch deine Einstellungen, Backup-Profile und die Historie von SyncForge gelöscht werden?%n%nDeine gesicherten Dateien in den Zielordnern bleiben in jedem Fall erhalten.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remove what the app registered in Windows: notification registration and autostart entry (both per user)
Filename: "{app}\{#AppExe}"; Parameters: "--uninstall-cleanup"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveRegistrations"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
procedure StopSyncForge;
var
  ResultCode: Integer;
begin
  // SyncForge keeps running in the notification area; its files would be in use otherwise.
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
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
  // Delete the data folder %LocalAppData%\SyncForge only on explicit request (never on silent uninstall).
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent then
    if MsgBox(CustomMessage('DeleteData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      DelTree(ExpandConstant('{localappdata}\SyncForge'), True, True, True);
end;

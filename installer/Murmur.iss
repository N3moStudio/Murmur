#ifndef Target
  #define Target "auto"
#endif
#define AppVersion "0.1.3"

[Setup]
AppId={{F18D755A-E128-4FC7-B356-652A84B31279}
AppName=Murmur
AppVersion={#AppVersion}
AppPublisher=N3mo Studio
AppCopyright=Copyright (c) 2026 N3mo Studio
DefaultDirName={localappdata}\Programs\Murmur
DefaultGroupName=Murmur
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0.22000
#if Target == "arm64"
ArchitecturesAllowed=arm64
#elif Target == "x64"
ArchitecturesAllowed=x64os
#else
ArchitecturesAllowed=arm64 or x64os
#endif
ArchitecturesInstallIn64BitMode=arm64 or x64os
SetupArchitecture=x64
OutputDir=..\artifacts\installers
OutputBaseFilename=Murmur-{#AppVersion}-Setup-{#Target}
SetupIconFile=..\src\Murmur\assets\murmur.ico
UninstallDisplayIcon={app}\Murmur.exe
WizardStyle=modern
Compression=lzma2/fast
SolidCompression=yes
CloseApplications=yes
CloseApplicationsFilter=Murmur.exe
RestartApplications=no
DisableWelcomePage=no
WizardSizePercent=110

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked
Name: startup; Description: "Start Murmur quietly when I sign in"; Flags: unchecked

[Files]
#if Target != "x64"
Source: "..\artifacts\publish\win-arm64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsArm64
Source: "..\tools\prerequisites\vc_redist.arm64.exe"; Flags: dontcopy
#endif
#if Target != "arm64"
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: not IsArm64
Source: "..\tools\prerequisites\vc_redist.x64.exe"; Flags: dontcopy
#endif
Source: "..\tools\prerequisites\MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy

[Icons]
Name: "{group}\Murmur"; Filename: "{app}\Murmur.exe"
Name: "{group}\Murmur guide"; Filename: "{app}\USER-GUIDE.html"
Name: "{autodesktop}\Murmur"; Filename: "{app}\Murmur.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "N3moStudio.Murmur"; ValueData: """{app}\Murmur.exe"" --background"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\Murmur.exe"; Description: "Open Murmur"; Flags: nowait postinstall skipifsilent

[Code]
var DeletePersonal, DeleteModels: Boolean;
function GetFileAttributesW(Path: String): Cardinal;
  external 'GetFileAttributesW@kernel32.dll stdcall';

procedure DeleteOwnedTree(Path, DataRoot: String);
var Entry: TFindRec;
begin
  if CompareText(Copy(Path, 1, Length(DataRoot) + 1), DataRoot + '\') <> 0 then exit;
  if not DirExists(Path) then exit;
  { Remove a directory link itself; never follow it to another directory. }
  if (GetFileAttributesW(Path) and $400) <> 0 then begin RemoveDir(Path); exit; end;
  if FindFirst(Path + '\*', Entry) then begin
    try
      repeat
        if (Entry.Name <> '.') and (Entry.Name <> '..') then begin
          if (Entry.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then DeleteOwnedTree(Path + '\' + Entry.Name, DataRoot)
          else DeleteFile(Path + '\' + Entry.Name);
        end;
      until not FindNext(Entry);
    finally FindClose(Entry); end;
  end;
  RemoveDir(Path);
end;

function RuntimeInstalled: Boolean;
var Version: String;
begin
  Result := ((RegQueryStringValue(HKLM32, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) or
              RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version)) and
              (Version <> '') and (Version <> '0.0.0.0'));
end;

function VisualCppInstalled: Boolean;
var MS, LS: Cardinal;
begin
  Result := GetVersionNumbers(ExpandConstant('{sys}\vcruntime140.dll'), MS, LS) and
    (MS >= ((14 shl 16) + 40)) and FileExists(ExpandConstant('{sys}\msvcp140.dll'));
end;

function ArchitectureName: String;
begin
  if IsArm64 then Result := 'ARM64' else Result := 'x64 (Intel / AMD)';
end;

procedure InitializeWizard;
begin
  WizardForm.WelcomeLabel2.Caption := 'Murmur is local voice writing by N3mo Studio.' + #13#10#13#10 +
    'This PC uses native Windows ' + ArchitectureName + '. Setup installs that build automatically.' + #13#10#13#10 +
    'Double-tap Ctrl to start recording. Tap Ctrl once to stop. The recording bar is hidden while idle.' + #13#10#13#10 +
    'Required: Windows 11, 8 GB RAM (16 GB recommended); Snapdragon X / ARMv8.7 or newer for ARM64. Models download separately. WebView2 downloads from Microsoft only if missing. Windows may request permission for a missing Microsoft C++ runtime.' + #13#10#13#10 +
    'This local release is unsigned. No subscription is required.';
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var ExitCode: Integer; RuntimeFile: String;
begin
  Result := '';
  if FileExists(ExpandConstant('{app}\Murmur.exe')) then
    Exec(ExpandConstant('{app}\Murmur.exe'), '--shutdown', '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
  if not VisualCppInstalled then begin
    if IsArm64 then RuntimeFile := 'vc_redist.arm64.exe' else RuntimeFile := 'vc_redist.x64.exe';
    ExtractTemporaryFile(RuntimeFile);
    if not Exec(ExpandConstant('{tmp}\') + RuntimeFile, '/install /passive /norestart', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then begin
      Result := 'The Microsoft C++ runtime could not start. Install it from Microsoft, then run setup again.'; exit;
    end;
    if (ExitCode <> 0) and (ExitCode <> 3010) and (ExitCode <> 1638) then begin
      Result := 'The Microsoft C++ runtime was not installed (code ' + IntToStr(ExitCode) + '). Your existing Murmur files are unchanged.'; exit;
    end;
    if ExitCode = 3010 then NeedsRestart := True;
  end;
  if not RuntimeInstalled then begin
    ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
    if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or not RuntimeInstalled then begin
      Result := 'Microsoft Edge WebView2 could not be installed. Connect to the internet or install Microsoft''s offline WebView2 Runtime, then retry.'; exit;
    end;
  end;
end;

function InitializeUninstall: Boolean;
var ExitCode: Integer;
begin
  Result := True; DeletePersonal := False; DeleteModels := False;
  if not UninstallSilent then begin
    DeletePersonal := MsgBox('Also permanently delete your Murmur history, notes, recordings, settings and saved connections?' + #13#10 + 'Choose No to keep them for a later reinstall.', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
    DeleteModels := MsgBox('Also delete the downloaded speech and writing models?' + #13#10 + 'Choose No to avoid downloading these large files again.', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
  end;
  if FileExists(ExpandConstant('{app}\Murmur.exe')) then
    Exec(ExpandConstant('{app}\Murmur.exe'), '--shutdown', '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var DataRoot: String; Found: TFindRec;
begin
  if CurUninstallStep <> usPostUninstall then exit;
  DataRoot := ExpandConstant('{localappdata}\N3moStudio\Murmur');
  if not DirExists(DataRoot) then exit;
  if ((GetFileAttributesW(DataRoot) and $400) <> 0) or ((GetFileAttributesW(ExpandConstant('{localappdata}\N3moStudio')) and $400) <> 0) then begin
    if DeletePersonal or DeleteModels then MsgBox('Murmur data is in a redirected folder. Setup kept that folder to avoid deleting files elsewhere. You can remove the saved data manually.', mbInformation, MB_OK);
    exit;
  end;
  if DeletePersonal then begin
    DeleteFile(DataRoot + '\murmur.db'); DeleteFile(DataRoot + '\murmur.db-wal'); DeleteFile(DataRoot + '\murmur.db-shm');
    if FindFirst(DataRoot + '\*.secret', Found) then begin
      try repeat if (Found.Attributes and FILE_ATTRIBUTE_DIRECTORY) = 0 then DeleteFile(DataRoot + '\' + Found.Name); until not FindNext(Found); finally FindClose(Found); end;
    end;
    DeleteOwnedTree(DataRoot + '\recordings', DataRoot);
    DeleteOwnedTree(DataRoot + '\webview', DataRoot);
    DeleteOwnedTree(DataRoot + '\updates', DataRoot);
  end;
  if DeleteModels then DeleteOwnedTree(DataRoot + '\models', DataRoot);
  if DeletePersonal and DeleteModels then RemoveDir(DataRoot);
end;

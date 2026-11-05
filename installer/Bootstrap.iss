#ifndef AppVersion
  #error Build using scripts/package-bootstrap.ps1 so release hashes are supplied.
#endif

[Setup]
AppId={{71B2929F-0562-4FE3-A909-09C063DECD47}
AppName=Murmur Setup
AppVersion={#AppVersion}
AppPublisher=N3mo Studio
AppPublisherURL=https://github.com/{#Repository}
CreateAppDir=no
Uninstallable=no
CreateUninstallRegKey=no
PrivilegesRequired=lowest
MinVersion=10.0.22000
ArchitecturesAllowed=arm64 or x64os
SetupArchitecture=x64
OutputDir=..\artifacts\installers
OutputBaseFilename=Murmur-Setup
SetupIconFile=..\src\Murmur\assets\murmur.ico
WizardStyle=modern
DisableWelcomePage=no
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
DisableFinishedPage=yes
CloseApplications=no
Compression=lzma2/fast

[Files]
Source: "https://github.com/{#Repository}/releases/download/v{#AppVersion}/Murmur-{#AppVersion}-Setup-arm64.exe"; DestDir: "{tmp}"; DestName: "Murmur-package.exe"; Flags: external download ignoreversion deleteafterinstall; ExternalSize: {#ArmBytes}; Hash: "{#ArmHash}"; Check: IsArm64
Source: "https://github.com/{#Repository}/releases/download/v{#AppVersion}/Murmur-{#AppVersion}-Setup-x64.exe"; DestDir: "{tmp}"; DestName: "Murmur-package.exe"; Flags: external download ignoreversion deleteafterinstall; ExternalSize: {#X64Bytes}; Hash: "{#X64Hash}"; Check: not IsArm64

[Code]
var ChildExitCode: Integer;

function ArchitectureName: String;
begin
  if IsArm64 then Result := 'arm64' else Result := 'x64';
end;

procedure InitializeWizard;
var Description: String;
begin
  if IsArm64 then Description := 'Windows on ARM (ARM64)' else Description := 'Intel / AMD Windows (x64)';
  WizardForm.WelcomeLabel2.Caption :=
    'Detected: ' + Description + '.' + #13#10#13#10 +
    'Setup will download and verify the matching Murmur {#AppVersion} installer, then open it for you. An internet connection is required.' + #13#10#13#10 +
    'Murmur is free and open source. On first launch, choose Download all to get the English speech and writing components. After setup, local dictation works offline.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Destination, Parameters: String;
begin
  if CurStep <> ssPostInstall then exit;
  Destination := ExpandConstant('{param:DOWNLOADONLY|}');
  if Destination <> '' then begin
    { Diagnostic/offline handoff: download and verify, without launching setup. }
    if not ForceDirectories(Destination) then RaiseException('Could not create the download folder.');
    Destination := AddBackslash(Destination) + 'Murmur-{#AppVersion}-Setup-' + ArchitectureName + '.exe';
    if not FileCopy(ExpandConstant('{tmp}\Murmur-package.exe'), Destination, True) then
      RaiseException('Could not save the installer. Choose an empty download folder.');
    Log('Verified installer saved: ' + Destination);
    exit;
  end;
  Parameters := '/SP- /NORESTART';
  if WizardSilent then Parameters := Parameters + ' /VERYSILENT /SUPPRESSMSGBOXES';
  if not Exec(ExpandConstant('{tmp}\Murmur-package.exe'), Parameters, '', SW_SHOWNORMAL, ewWaitUntilTerminated, ChildExitCode) then
    RaiseException('Could not open the downloaded installer: ' + SysErrorMessage(ChildExitCode));
  if (ChildExitCode <> 0) and (ChildExitCode <> 3010) then begin
    if not WizardSilent then
      SuppressibleMsgBox('Murmur installation did not finish. You can run this setup again. Exit code: ' + IntToStr(ChildExitCode), mbInformation, MB_OK, IDOK);
  end;
end;

function GetCustomSetupExitCode: Integer;
begin
  Result := ChildExitCode;
end;

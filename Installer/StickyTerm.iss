; StickyTerm Installer Script for Inno Setup
; https://jrsoftware.org/isinfo.php

#define MyAppName "StickyTerm"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "StickyTerm"
#define MyAppURL "https://github.com/ril3y/StickyTerm"
#define MyAppExeName "StickyTerm.exe"
#define MyAppDescription "Serial terminal with sticky COM port assignments"

[Setup]
; Unique identifier for this application
AppId={{A8E9F2D1-5B3C-4E7A-9F1D-2C8B4A6E3F5D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; Output settings
OutputDir=..\Output
OutputBaseFilename=StickyTerm_Setup_{#MyAppVersion}
; Compression
Compression=lzma2/ultra64
SolidCompression=yes
; Require admin for installation (needed for Program Files)
PrivilegesRequired=admin
; Modern look
WizardStyle=modern
; Icon
SetupIconFile=..\StickyTerm\Resources\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
; Version info
VersionInfoVersion={#MyAppVersion}
VersionInfoDescription={#MyAppDescription}
; Architecture
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Uninstall
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start {#MyAppName} when Windows starts"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
; Main application files from publish folder
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
; Start menu shortcut
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "{#MyAppDescription}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
; Desktop shortcut (optional)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "{#MyAppDescription}"

[Run]
; Option to run after install
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  TASK_NAME = 'StickyTerm Auto-Start';

// Function to create the scheduled task for startup
procedure CreateStartupTask();
var
  ResultCode: Integer;
  TaskXml: string;
  TempFile: string;
begin
  // Create XML for the scheduled task
  TaskXml := '<?xml version="1.0" encoding="UTF-16"?>' + #13#10 +
    '<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">' + #13#10 +
    '  <RegistrationInfo>' + #13#10 +
    '    <Description>Starts StickyTerm at Windows startup with elevated privileges</Description>' + #13#10 +
    '    <Author>StickyTerm</Author>' + #13#10 +
    '  </RegistrationInfo>' + #13#10 +
    '  <Triggers>' + #13#10 +
    '    <LogonTrigger>' + #13#10 +
    '      <Enabled>true</Enabled>' + #13#10 +
    '      <Delay>PT10S</Delay>' + #13#10 +
    '    </LogonTrigger>' + #13#10 +
    '  </Triggers>' + #13#10 +
    '  <Principals>' + #13#10 +
    '    <Principal id="Author">' + #13#10 +
    '      <LogonType>InteractiveToken</LogonType>' + #13#10 +
    '      <RunLevel>HighestAvailable</RunLevel>' + #13#10 +
    '    </Principal>' + #13#10 +
    '  </Principals>' + #13#10 +
    '  <Settings>' + #13#10 +
    '    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>' + #13#10 +
    '    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>' + #13#10 +
    '    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>' + #13#10 +
    '    <AllowHardTerminate>true</AllowHardTerminate>' + #13#10 +
    '    <StartWhenAvailable>true</StartWhenAvailable>' + #13#10 +
    '    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>' + #13#10 +
    '    <AllowStartOnDemand>true</AllowStartOnDemand>' + #13#10 +
    '    <Enabled>true</Enabled>' + #13#10 +
    '    <Hidden>false</Hidden>' + #13#10 +
    '    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>' + #13#10 +
    '    <Priority>7</Priority>' + #13#10 +
    '  </Settings>' + #13#10 +
    '  <Actions Context="Author">' + #13#10 +
    '    <Exec>' + #13#10 +
    '      <Command>"' + ExpandConstant('{app}') + '\' + '{#MyAppExeName}' + '"</Command>' + #13#10 +
    '      <Arguments>--minimized</Arguments>' + #13#10 +
    '      <WorkingDirectory>' + ExpandConstant('{app}') + '</WorkingDirectory>' + #13#10 +
    '    </Exec>' + #13#10 +
    '  </Actions>' + #13#10 +
    '</Task>';

  // Save XML to temp file
  TempFile := ExpandConstant('{tmp}\stickyterm_task.xml');
  SaveStringToFile(TempFile, TaskXml, False);

  // Create the task using schtasks
  Exec('schtasks.exe', '/Create /TN "' + TASK_NAME + '" /XML "' + TempFile + '" /F',
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  // Clean up temp file
  DeleteFile(TempFile);
end;

// Function to delete the scheduled task
procedure DeleteStartupTask();
var
  ResultCode: Integer;
begin
  Exec('schtasks.exe', '/Delete /TN "' + TASK_NAME + '" /F',
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Called after installation
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Create startup task if user selected the option
    if IsTaskSelected('startupicon') then
    begin
      CreateStartupTask();
    end;
  end;
end;

// Called during uninstallation
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    // Always try to delete the startup task during uninstall
    DeleteStartupTask();
  end;
end;

// Initialize setup
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

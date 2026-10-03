; ============================================================================
; Datchik installer script, for Inno Setup (jrsoftware.org).
;
; Builds DatchikSetup-<version>.exe from the files in ..\publish.
; Before compiling, publish the app:
;   dotnet publish SysMonitor.App -c Release -r win-x64 -o publish
;
; Syntax notes:
;   ; at the start of a line   = a comment
;   [Section]                  = a group of settings
;   #define Name "Value"       = a constant, used below as {#Name}
;   {app}, {autopf}, ...       = Inno's built-in placeholders for folders
; ============================================================================

#define AppName "Datchik"
; Passed in by the build (from <Version> in SysMonitor.App.csproj). Fallback for manual compiles.
#ifndef AppVersion
  #define AppVersion "0.0.0-dev"
#endif
#define AppPublisher "rayjackson13"    ; shown in "Apps" in Windows Settings
#define AppExeName "Datchik.exe"
#define PublishDir "..\publish"     ; relative to this .iss file

[Setup]
; A unique ID for this app. Windows uses it to recognize Datchik when you install
; an update or uninstall. Generate your own: in Inno Setup's editor, Tools → Generate GUID.
; It must start with TWO opening braces ({{), because a single { would mean a placeholder.
; Never change it after you've shared the first version.
AppId={{6f7d017b-33c9-4cf7-9c6f-e8617c0e4a81}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}

; Install into C:\Program Files\Datchik. {autopf} = the right Program Files folder.
DefaultDirName={autopf}\{#AppName}
; Skip the "choose Start Menu folder" page; we just add one shortcut.
DisableProgramGroupPage=yes

; Where the finished installer goes, and its file name.
OutputDir=..\installer-output
OutputBaseFilename=DatchikSetup-{#AppVersion}

; Icons: the installer's own .exe, and the entry in "Apps" in Windows Settings.
SetupIconFile=..\SysMonitor.App\Assets\datchik.ico
UninstallDisplayIcon={app}\{#AppExeName}

; Compression: strong, but much faster than ultra64, for a slightly bigger installer.
Compression=lzma2/max
SolidCompression=yes

; Compress in parallel on several CPU cores. LZMA2 splits the data into blocks and
; compresses them at the same time, at the cost of a slightly lower compression ratio.
LZMAUseSeparateProcess=yes
LZMANumBlockThreads=4

; 64-bit Windows 10 or newer only, matching how the app is built.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

; Installing into Program Files needs admin rights (Datchik needs them anyway).
PrivilegesRequired=admin

; The modern-looking wizard.
WizardStyle=modern

[Languages]
; Lets the installer's own texts (buttons, pages) appear in English or Russian.
; Inno picks the one matching the PC's language, or asks.
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
; Optional checkboxes shown during installation. "unchecked" = off by default.
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

; Download and install PawnIO. On by default; "Check:" hides the checkbox entirely
; when PawnIO is already installed (IsPawnIOInstalled is defined in [Code]).
Name: "pawnio"; Description: "Download and install PawnIO (needed for CPU temperature, clock, power and fans)"; GroupDescription: "Hardware access:"; Check: not IsPawnIOInstalled

[Files]
; Copy everything from the publish folder into the install folder.
;   recursesubdirs + createallsubdirs = include subfolders too
;   ignoreversion = always overwrite, so updates replace old files
;   Excludes: "*.pdb" = skip debug information files, users don't need them
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
; A Start Menu shortcut, and a desktop shortcut if that checkbox was ticked.
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; A "Launch Datchik" checkbox on the last page.
;   postinstall = show it as a checkbox on the Finish page
;   shellexec   = start it the way Explorer would, so its admin manifest is respected
;   nowait      = don't keep the installer open while Datchik runs
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent shellexec

[Code]
// The [Code] section is a small program in Pascal Script.
// Here it downloads PawnIO's official installer and runs it silently.

const
  // PawnIO's official installer, from the author's GitHub releases. "latest/download"
  // always points to the newest release, so new PawnIO versions need no script change.
  PawnIODownloadUrl = 'https://github.com/namazso/PawnIO.Setup/releases/latest/download/PawnIO_setup.exe';
  PawnIOSetupFileName = 'PawnIO_setup.exe';
  PawnIOWebsite = 'https://pawnio.eu/';

  // Exit codes PawnIO's installer returns (standard Windows error codes).
  ExitCodeSuccess = 0;
  ExitCodeSuccessRebootRequired = 3010;

var
  // The wizard page that shows download progress. Created once, in InitializeWizard.
  DownloadPage: TDownloadWizardPage;

  // True if PawnIO's download finished, so its setup can be run after Datchik's files are copied.
  PawnIODownloaded: Boolean;

  // True if PawnIO's setup said Windows must restart. Read by NeedRestart below.
  PawnIONeedsRestart: Boolean;

// Returns True if the PawnIO driver is installed. Windows registers every driver and service
// under this registry key, so its presence means PawnIO is there.
function IsPawnIOInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\PawnIO');
end;

// Offers to open PawnIO's website, for when the automatic installation didn't work.
// Reason = a sentence explaining what went wrong, shown at the start of the message.
procedure OfferPawnIOWebsite(Reason: String);
var
  ErrorCode: Integer;
begin
  if WizardSilent then Exit;  // no windows in silent mode, so no questions

  // #13#10 = a line break.
  if MsgBox(Reason + #13#10 + #13#10 +
            'Datchik is installed and works without PawnIO, but CPU temperature, clock, power ' +
            'and fan speed will be unavailable until it is installed.' + #13#10 + #13#10 +
            'Open the PawnIO website to install it manually?',
            mbConfirmation, MB_YESNO) = IDYES then
  begin
    ShellExec('open', PawnIOWebsite, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
  end;
end;

// Inno calls this once, when the wizard is being set up. We create the download page here;
// it stays hidden until we show it.
procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage('Downloading PawnIO',
    'Please wait while the PawnIO driver installer is downloaded.', nil);
end;

// Inno calls this whenever Next is clicked. CurPageID says which page we're leaving.
// Returning False keeps the wizard on that page; we always return True, so a failed
// download never blocks installing Datchik itself.
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  // wpReady = the "Ready to Install" page, so this runs right when Install is clicked.
  if (CurPageID = wpReady) and WizardIsTaskSelected('pawnio') then
  begin
    DownloadPage.Clear;
    // Arguments: URL, file name to save as (in a temporary folder), and an optional SHA-256
    // hash to verify the file. Empty = no check, because "latest" changes with each release.
    DownloadPage.Add(PawnIODownloadUrl, PawnIOSetupFileName, '');
    DownloadPage.Show;
    try
      try
        DownloadPage.Download;
        PawnIODownloaded := True;
      except
        // Download failed (no internet, blocked, cancelled): carry on without PawnIO.
        if not DownloadPage.AbortedByUser then
          Log('PawnIO download failed: ' + GetExceptionMessage);
        PawnIODownloaded := False;
      end;
    finally
      DownloadPage.Hide;
    end;
  end;
end;

// Inno calls this at each step of the installation. ssPostInstall = Datchik's files are copied.
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if (CurStep <> ssPostInstall) or (not WizardIsTaskSelected('pawnio')) then Exit;

  if not PawnIODownloaded then
  begin
    OfferPawnIOWebsite('The PawnIO installer could not be downloaded.');
    Exit;
  end;

  // Show what's happening in the progress page's status line.
  WizardForm.StatusLabel.Caption := 'Installing PawnIO...';

  // Run PawnIO's setup silently and wait for it to finish.
  // {tmp} = the temporary folder the download page saved the file into.
  if not Exec(ExpandConstant('{tmp}\' + PawnIOSetupFileName), '-install -silent', '',
              SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    OfferPawnIOWebsite('The PawnIO installer could not be started.');
    Exit;
  end;

  if ResultCode = ExitCodeSuccessRebootRequired then
    PawnIONeedsRestart := True
  else if ResultCode <> ExitCodeSuccess then
    OfferPawnIOWebsite('The PawnIO installer reported an error (code ' + IntToStr(ResultCode) + ').');
end;

// Inno calls this at the end. Returning True makes the Finish page offer to restart Windows.
function NeedRestart: Boolean;
begin
  Result := PawnIONeedsRestart;
end;
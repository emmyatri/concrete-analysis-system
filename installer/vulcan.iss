; Vulcan CBAS installer (SCRUM-116; NFR-1 single installer, NFR-3 offline).
; Packages the self-contained build from SCRUM-108. Build steps are in the README ("Building the installer").
;
; Uninstalling removes the program but keeps the saved data in %LOCALAPPDATA%\VulcanCBAS (database, logs,
; WebView2 data), so lab records survive an uninstall or upgrade.

#define AppName "Vulcan CBAS"
#ifndef AppVersion
#define AppVersion "1.0.0"
#endif
#define AppExe "Vulcan.Desktop.exe"
#define PublishDir "..\src\Vulcan.Desktop\bin\publish\win-x64"
#define WebView2Setup "MicrosoftEdgeWebView2RuntimeInstallerX64.exe"

[Setup]
; Never change AppId: Windows uses it to recognise upgrades and the uninstaller.
AppId={{E068D033-C743-4032-B5BC-232085ECAEB5}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Vulcan CBAS project team
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
; Lets someone without admin rights install just for themselves (corporate laptops).
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 1809 or later; the target is Windows 11.
MinVersion=10.0.17763
SetupIconFile=..\src\Vulcan.Web\public\favicon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
; Closes the app if it's open, so its files can be replaced.
CloseApplications=yes
OutputDir=output
OutputBaseFilename=VulcanCBAS-Setup-{#AppVersion}

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Only unpacked when the WebView2 runtime is missing (Windows 11 already has it).
Source: "redist\{#WebView2Setup}"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NeedsWebView2

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{tmp}\{#WebView2Setup}"; Parameters: "/silent /install"; StatusMsg: "Installing the Microsoft Edge WebView2 runtime..."; Flags: waituntilterminated; Check: NeedsWebView2
Filename: "{app}\{#AppExe}"; Description: "Open {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
const
  WebView2Key = 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

{ True when this registry key holds an installed WebView2 version. }
function HasWebView2(RootKey: Integer): Boolean;
var
  Version: String;
begin
  Result := RegQueryStringValue(RootKey, WebView2Key, 'pv', Version)
    and (Version <> '') and (Version <> '0.0.0.0');
end;

{ Microsoft's documented check: per-machine (32-bit view) or per-user install. }
function NeedsWebView2: Boolean;
begin
  Result := not (HasWebView2(HKLM32) or HasWebView2(HKLM64) or HasWebView2(HKCU));
end;

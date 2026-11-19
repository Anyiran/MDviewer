; ============================================================
;  MDviewer installer (Inno Setup 6)
;  Build with:  powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
; ============================================================

#define AppName        "MDviewer"
#define AppVersion     "1.01"
#define AppPublisher   "MDviewer"
#define AppExeName     "MDviewer.exe"

[Setup]
AppId={{BA1F0C52-6E8B-4C57-9A21-0D3F7E6C4A11}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppComments=轻量、离线的 GitHub 风格 Markdown 阅读器
DefaultDirName={localappdata}\Programs\MDviewer
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\build
OutputBaseFilename=MDviewer-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
MinVersion=6.1sp1
SetupIconFile=..\src\MDviewer\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
ChangesAssociations=yes
CloseApplications=yes
RestartApplications=no
AllowNoIcons=yes
LicenseFile=..\LICENSE
InfoBeforeFile=
ArchitecturesAllowed=x86compatible
ArchitecturesInstallIn64BitMode=

[Languages]
Name: "zh"; MessagesFile: "ChineseSimplified.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "associate"; Description: "将 MDviewer 注册为 Markdown 文件（.md）的打开方式"; GroupDescription: "集成："

[Files]
Source: "..\build\MDviewer\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Parameters: "--register-silent"; Flags: runhidden; StatusMsg: "正在注册文件关联…"; Tasks: associate
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#AppExeName}"; Parameters: "--unregister-silent"; Flags: runhidden; RunOnceId: "RemoveAssociations"

[Code]
const
  WebView2DownloadUrl = 'https://developer.microsoft.com/microsoft-edge/webview2/';

function HasDotNet45OrNewer(): Boolean;
var
  Release: Cardinal;
begin
  Result := False;
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) then
    Result := Release >= 378389
  else if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Client', 'Install', Release) then
    Result := True;
end;

function HasWebView2(): Boolean;
begin
  Result :=
    DirExists(ExpandConstant('{localappdata}\Microsoft\EdgeWebView\Application')) or
    DirExists(ExpandConstant('{commonpf}\Microsoft\EdgeWebView\Application')) or
    DirExists(ExpandConstant('{commonpf32}\Microsoft\EdgeWebView\Application'));
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;

  if not HasDotNet45OrNewer() then
  begin
    if MsgBox('MDviewer 需要 .NET Framework 4.5 或更高版本（推荐 4.8）。' + #13#10#13#10 +
              '是否现在打开 .NET Framework 下载页面？', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet-framework', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
    Result := False;
    Exit;
  end;

  if not HasWebView2() then
  begin
    if MsgBox('未检测到 Microsoft Edge WebView2 运行时。' + #13#10#13#10 +
              'Windows 10/11 通常已自带；Windows 7/8.1 需要手动安装后才能显示文档。' + #13#10#13#10 +
              '是否现在打开下载页面？（安装仍会继续）', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', WebView2DownloadUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
  end;
end;

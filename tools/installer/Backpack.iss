; Установщик Backpack для Windows x64 (Inno Setup 6). Собирается из уже проверенного portable-пакета (tools/package-release.ps1,
; tools/verify-package.ps1) скриптом tools/build-installer.ps1 — состав файлов тот же, что в zip:
;   ISCC /DAppVersion=0.4.0-preview.8 /DAppVersionNumeric=0.4.0.8 /DSourceDir=<распакованный zip> /DOutputDir=<папка> /DRepoRoot=<репозиторий> Backpack.iss
;
; - Без прав администратора по умолчанию: установка для текущего пользователя в %LOCALAPPDATA%\Programs\Backpack (как и сама
;   программа, которой права администратора не нужны); в диалоге можно выбрать установку для всех пользователей.
; - Self-contained: .NET runtime входит в пакет, ничего доустанавливать не нужно.
; - Настройки (%APPDATA%\Backpack) установщик не трогает ни при обновлении, ни при удалении. Обновление — запуск установщика новой
;   версии поверх: тот же AppId, папки Plugins и Samples заменяются целиком (без пакетов прежних версий).
; - Удаление снимает запись автозапуска Windows, только если она указывает на эту установку (портативные копии не трогаются).
; - Не подписан цифровой подписью (как и zip): Windows SmartScreen может предупредить.

#ifndef AppVersion
  #error AppVersion is required (/DAppVersion=...)
#endif
#ifndef AppVersionNumeric
  #define AppVersionNumeric "0.0.0.0"
#endif
#ifndef SourceDir
  #error SourceDir is required (/DSourceDir=...)
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif
#ifndef RepoRoot
  #define RepoRoot "..\.."
#endif

[Setup]
AppId={{E35DC09A-A4B5-40CC-9F2C-4EEB4E73E264}
AppName=Backpack
AppVersion={#AppVersion}
AppVerName=Backpack {#AppVersion}
AppPublisher=soobshoob
AppPublisherURL=https://github.com/soobshoob/Backpack
AppSupportURL=https://github.com/soobshoob/Backpack/issues
AppUpdatesURL=https://github.com/soobshoob/Backpack/releases
AppCopyright=Copyright soobshoob
VersionInfoVersion={#AppVersionNumeric}
VersionInfoProductName=Backpack
VersionInfoProductTextVersion={#AppVersion}
VersionInfoDescription=Backpack {#AppVersion} Setup
DefaultDirName={autopf}\Backpack
DefaultGroupName=Backpack
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=Backpack-{#AppVersion}-win-x64-setup
SetupIconFile={#RepoRoot}\lab\src\Backpack.App\Assets\Backpack.ico
UninstallDisplayIcon={app}\Backpack.exe
UninstallDisplayName=Backpack
LicenseFile={#SourceDir}\LICENSE.txt
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ChangesAssociations=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
; Поставляемые плагины и их исходники — ровно этой версии (индекс bundled.json); пакеты прежних версий не остаются.
Type: filesandordirs; Name: "{app}\Plugins"
Type: filesandordirs; Name: "{app}\Samples"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Backpack"; Filename: "{app}\Backpack.exe"
Name: "{autodesktop}\Backpack"; Filename: "{app}\Backpack.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Backpack.exe"; Description: "{cm:LaunchProgram,Backpack}"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  // Автозапуск (значение «Backpack», см. Autostart.cs) снимается, только если он указывает на эту установку.
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, RunKey, 'Backpack', Command) then
      if Pos(Lowercase(AddBackslash(ExpandConstant('{app}'))), Lowercase(Command)) > 0 then
        RegDeleteValue(HKCU, RunKey, 'Backpack');
end;

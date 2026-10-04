; Script de Inno Setup para el instalador de Windows (Pos-<versión>-win-x64-setup.exe).
; Lo compilan scripts/build-installers.ps1 (Windows) o scripts/build-installers.sh (Linux + Wine),
; que pasan AppVersion, SourceDir y OutputDir con /D. Los datos del usuario viven en
; %LOCALAPPDATA%\Pos; el instalador y el desinstalador nunca los tocan.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\artifacts\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\artifacts\installers"
#endif

#define AppName "POS"
#define AppExe "Pos.exe"

[Setup]
; Fijo: identifica la app entre versiones para que una actualización reemplace la instalación
; anterior. NO CAMBIAR.
AppId={{7A3E79CE-BCB3-4FC9-805A-EAA1B44B98E2}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=POS
DefaultDirName={autopf}\Pos
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Instalación por usuario por omisión (sin permisos de administrador); se puede elegir para todos.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=Pos-{#AppVersion}-win-x64-setup
SetupIconFile=..\..\src\Pos.Desktop\Assets\pos.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

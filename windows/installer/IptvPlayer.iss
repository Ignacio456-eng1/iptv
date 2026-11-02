; Instalador de IPTV Player para Windows 10/11 (Inno Setup 6).
; Uso: ISCC.exe /DAppVersion=0.2.0 /DSourceDir=<carpeta publicada> /DOutDir=<salida> IptvPlayer.iss

#ifndef AppVersion
  #define AppVersion "0.2.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\out\app"
#endif
#ifndef OutDir
  #define OutDir "..\out"
#endif

[Setup]
AppId={{6B1E3C5A-2F44-4F7E-9C1B-3D6A2B9E7F10}
AppName=IPTV Player
AppVersion={#AppVersion}
AppVerName=IPTV Player {#AppVersion}
AppPublisher=IPTV Player
DefaultDirName={autopf}\IPTV Player
DefaultGroupName=IPTV Player
DisableProgramGroupPage=yes
; Se instala para el usuario actual sin pedir permisos de administrador (se puede elegir "para todos").
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutDir}
OutputBaseFilename=IptvPlayer-Setup
SetupIconFile=..\src\IptvPlayer\Assets\app.ico
UninstallDisplayIcon={app}\IptvPlayer.exe
UninstallDisplayName=IPTV Player
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\IPTV Player"; Filename: "{app}\IptvPlayer.exe"
Name: "{autodesktop}\IPTV Player"; Filename: "{app}\IptvPlayer.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\IptvPlayer.exe"; Description: "{cm:LaunchProgram,IPTV Player}"; Flags: nowait postinstall skipifsilent

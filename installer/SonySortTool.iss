; 소니정리툴 설치 프로그램 (Inno Setup 6)
;  - 관리자 권한 없이 현재 사용자에게 설치:  %LOCALAPPDATA%\Programs\SonySortTool
;  - 시작 메뉴 바로가기(폴더 없이 바로), 바탕화면 아이콘(선택), 폴더 우클릭 메뉴(선택)
;  - 제거는 [설정 › 앱] 또는 배포 파일의 SonySortTool-Uninstall.exe
;  - 이전 버전 위에 설치하면 그대로 업데이트 (AppId가 같음. 설정·기록은 유지)
;  - 제거하면 설정·기록(%LOCALAPPDATA%\SonySortTool)까지 지움
;  build.ps1 -Release 가 버전을 넘겨 줌 (직접 컴파일하면 아래 기본값)

#ifndef AppVersion
  #define AppVersion "1.4"
#endif

[Setup]
AppId={{55684156-3B06-4A90-9837-DC59F8AA3EFE}
AppName=소니정리툴
AppVersion={#AppVersion}
AppVerName=소니정리툴 {#AppVersion}
AppPublisher=이중필름
AppCopyright=Copyright © 2026 이중필름
VersionInfoVersion={#AppVersion}.0.0
VersionInfoDescription=소니정리툴 설치 프로그램
DefaultDirName={autopf}\SonySortTool
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\release
OutputBaseFilename=SonySortTool-{#AppVersion}-Setup
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\SonySortTool.exe
UninstallDisplayName=소니정리툴
WizardStyle=modern
Compression=lzma2/ultra
SolidCompression=yes
ShowLanguageDialog=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕화면에 아이콘 만들기"; GroupDescription: "추가 아이콘:"
Name: "contextmenu"; Description: "폴더 우클릭 메뉴에 '소니정리툴로 정리' 추가"; GroupDescription: "탐색기:"

; 폴더 우클릭 · 폴더 안 빈 곳 우클릭 → 그 폴더로 바로 미리보기 (현재 사용자만, 제거하면 함께 삭제)
; Windows 11에서는 '더 많은 옵션 표시' 안에 나타남 (새 메뉴에 바로 넣으려면 코드 서명된 패키지가 필요)
[Registry]
Root: HKCU; Subkey: "Software\Classes\Directory\shell\SonySortTool"; ValueType: string; ValueName: "MUIVerb"; ValueData: "소니정리툴로 정리"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\SonySortTool"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\SonySortTool.exe"",0"; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\SonySortTool"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Single"; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\SonySortTool\command"; ValueType: string; ValueName: ""; ValueData: """{app}\SonySortTool.exe"" ""%1"""; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\SonySortTool"; ValueType: string; ValueName: "MUIVerb"; ValueData: "소니정리툴로 정리"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\SonySortTool"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\SonySortTool.exe"",0"; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\SonySortTool\command"; ValueType: string; ValueName: ""; ValueData: """{app}\SonySortTool.exe"" ""%V"""; Tasks: contextmenu

[Files]
Source: "..\dist\SonySortTool.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\소니정리툴"; Filename: "{app}\SonySortTool.exe"
Name: "{autodesktop}\소니정리툴"; Filename: "{app}\SonySortTool.exe"; Tasks: desktopicon

; 1.1이 만든 시작 메뉴 '소니정리툴 제거'는 업데이트할 때 지움
[InstallDelete]
Type: files; Name: "{autoprograms}\소니정리툴 제거.lnk"

[Run]
Filename: "{app}\SonySortTool.exe"; Description: "소니정리툴 실행"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\SonySortTool"

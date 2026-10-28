# 빌드·테스트

SONY 타입 정리기를 소스 코드로 빌드하고 시험하는 방법입니다.
프로그램을 쓰기만 한다면 이 문서는 필요 없습니다. [README](../README.md)를 보세요.

## 준비

빌드에는 Windows 10/11에 들어 있는 C# 컴파일러(.NET Framework 4.x)만 씁니다. 따로 설치할 것은 없습니다.

## 빌드와 테스트

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1 -Test
```

| 결과 파일 | 내용 |
|---|---|
| `dist\SonySortTool.exe` | 프로그램. 설치하지 않아도 실행됩니다. |
| `dist\Setup.exe` | 설치 파일. 배포 이름은 `SONY 타입 정리기 설치.exe` |
| `dist\Uninstall.exe` | 제거 파일. 배포 이름은 `SONY 타입 정리기 제거.exe` |
| `build\OrganizerTest.exe` | 정리 로직 테스트 |
| `tests\SetupTest.ps1` | 설치·제거 테스트 |

**정리 로직 테스트**

- `OrganizerTest.exe`는 실제 카드(ILCE-7M5) 구조를 본뜬 가짜 카드로 여러 상황을 시험합니다.
- `OrganizerTest.exe --replica <폴더>`는 실제 카드 폴더의 복제본으로 시험합니다. 복제본에서 XML은 내용까지 복사하고, 다른 파일은 1바이트로 만듭니다. 원본 폴더는 바뀌지 않습니다.

**설치·제거 테스트**

- 테스트는 임시 폴더와 별도 레지스트리 키(`/sandbox`)만 씁니다. 그래서 PC에 설치된 프로그램은 바뀌지 않습니다.
- 테스트 항목: 새로 설치, 다시 설치, 이전 버전 업데이트, 실행 중 설치 막기, 제거

## 배포 파일 만들기

1. 버전을 두 곳에 쓰세요: `src\Product.cs`의 `Version`, `src\app.manifest`
2. 아래 명령을 실행하세요. 테스트를 통과해야 배포 파일이 생깁니다.

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1 -Release
```

```
release\
├ SonySortTool-<버전>.zip    ← GitHub 릴리스에 올리는 파일
│   ├ SONY 타입 정리기 설치.exe
│   ├ SONY 타입 정리기 제거.exe
│   └ 사용 안내.txt
└ SHA256.txt                ← 파일 지문. 릴리스 노트에 붙임
```

## 소스 구조

| 경로 | 내용 |
|---|---|
| `src\Organizer.cs` | 소니 카드 정리 로직 (미리보기·정리·되돌리기) |
| `src\MainForm.cs` | 화면 |
| `src\Ui.cs` | 화면 공통 부품 (버튼·토글·진행 막대·카드·머리 그림). 설치·제거 파일도 씁니다. |
| `src\Product.cs` | 이름·버전·색 등 제품 정보 |
| `src\FolderPicker.cs` | 폴더 선택 창 |
| `src\Settings.cs` | 설정 저장 |
| `setup\Setup.cs` | 설치·제거 파일. 같은 소스로 두 파일을 만듭니다. |
| `setup\guide.txt` | 배포 zip의 `사용 안내.txt` |
| `assets\make_icon.ps1` | 아이콘 (프로그램 · 설치 · 제거) |
| `assets\art\` | 설치 화면과 프로그램 화면의 그림 |
| `assets\readme\` | README 그림 |

# 설치·제거 프로그램 테스트 (build.ps1 -Test)
#  실제 설치본은 건드리지 않음: /sandbox=<임시 폴더> → 설치 위치·바로가기·설정 폴더는 그 안, 레지스트리는 HKCU\Software\<Id>Sandbox 아래
#   1) 새로 설치  2) 같은 버전 다시 설치 (설정 유지)  3) 옛 Inno Setup 설치본(1.0, 옛 이름) 위에 업데이트  4) 실행 중이면 막음
#   5) [설정 › 앱]과 같은 경로(설치 폴더의 Uninstall.exe)로 제거  6) 없는 것을 제거  7) 설치 위치를 바꿔 둔 설치본
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$product = [IO.File]::ReadAllText((Join-Path $repo 'src\Product.cs'))
function Const($n) { [regex]::Match($product, "\b$n\s*=\s*""([^""]*)""").Groups[1].Value }
$name = Const 'Name'; $old = Const 'OldName'; $version = Const 'Version'; $id = Const 'Id'; $key = Const 'UninstallKey'; $menuText = Const 'MenuText'
$setup = Join-Path $repo 'dist\Setup.exe'; $uninstall = Join-Path $repo 'dist\Uninstall.exe'

$root = Join-Path $env:TEMP ("$($id)SetupTest_" + [guid]::NewGuid().ToString('N').Substring(0, 8))
$reg  = "HKCU:\Software\$($id)Sandbox"
$ukey = "$reg\Microsoft\Windows\CurrentVersion\Uninstall\$key"
$app  = "$root\LocalAppData\Programs\$id"
$data = "$root\LocalAppData\$id"
$startLink = "$root\StartMenu\$name.lnk"; $deskLink = "$root\Desktop\$name.lnk"
$script:failures = 0

function Check($ok, $what) {
    if ($ok) { Write-Host "  ✔ $what" } else { Write-Host "  ✘ $what" -ForegroundColor Red; $script:failures++ }
}
function Run($exe, [string[]]$arguments) {
    $p = Start-Process $exe -ArgumentList $arguments -PassThru -Wait -WindowStyle Hidden
    $p.ExitCode
}
function LinkTarget($lnk) { (New-Object -ComObject WScript.Shell).CreateShortcut($lnk).TargetPath }
function NewLink($lnk, $target) {
    $null = New-Item -ItemType Directory -Force (Split-Path $lnk)
    $s = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk); $s.TargetPath = $target; $s.Save()
}
function Reset { if (Test-Path $root) { Remove-Item $root -Recurse -Force }; if (Test-Path $reg) { Remove-Item $reg -Recurse -Force } }
$sb = "/sandbox=$root"

try {
    Reset
    Write-Host '[새로 설치]'
    Check ((Run $setup '/silent', $sb) -eq 0) '끝 코드 0'
    Check ((Test-Path "$app\$id.exe") -and (Test-Path "$app\Uninstall.exe") -and (Test-Path "$app\LICENSE.txt")) '프로그램 · 제거 프로그램 · 라이선스'
    Check ((Get-Item "$app\$id.exe").Length -eq (Get-Item (Join-Path $repo "dist\$id.exe")).Length) '프로그램은 이번에 빌드한 것과 같은 파일'
    Check ((Test-Path $startLink) -and (LinkTarget $startLink) -eq "$app\$id.exe") '시작 메뉴 바로가기 → 프로그램'
    Check ((Test-Path $deskLink) -and (LinkTarget $deskLink) -eq "$app\$id.exe") '바탕화면 바로가기 → 프로그램'
    $u = Get-ItemProperty $ukey
    Check ($u.DisplayName -eq $name -and $u.DisplayVersion -eq $version) "[설정 › 앱] 항목: $($u.DisplayName) $($u.DisplayVersion)"
    Check ($u.UninstallString -eq "`"$app\Uninstall.exe`" `"/sandbox=$root`"") '제거 명령 = 설치 폴더의 Uninstall.exe'
    Check ($u.EstimatedSize -gt 0 -and $u.NoModify -eq 1) '용량 · 변경 없음 표시'
    Check ((Get-ItemProperty "$reg\Classes\Directory\shell\$id").MUIVerb -eq $menuText) "폴더 우클릭 메뉴 '$menuText'"
    Check ((Get-ItemProperty "$reg\Classes\Directory\shell\$id\command").'(default)' -eq "`"$app\$id.exe`" `"%1`"") '우클릭 메뉴 명령'
    Check ((Get-ItemProperty "$reg\Classes\Directory\Background\shell\$id\command").'(default)' -eq "`"$app\$id.exe`" `"%V`"") '폴더 빈 곳 우클릭 메뉴 명령'
    Check (-not (Test-Path "$app\*.new")) '임시 파일 없음'

    Write-Host '[같은 버전 다시 설치 · 설정 유지 · 옵션 끄기]'
    $null = New-Item -ItemType Directory -Force "$data\logs"
    Set-Content "$data\settings.ini" 'AddSize=0'
    NewLink "$root\StartMenu\$old.lnk" "$env:WINDIR\notepad.exe"            # 사용자가 따로 만든 옛 이름의 바로가기
    Check ((Run $setup '/silent', '/nodesktop', '/nomenu', $sb) -eq 0) '끝 코드 0'
    Check ((Get-Content "$data\settings.ini") -eq 'AddSize=0') '설정 파일 그대로'
    Check (-not (Test-Path $deskLink)) '바탕화면 아이콘을 끄면 지움'
    Check (-not (Test-Path "$reg\Classes\Directory\shell\$id")) '우클릭 메뉴를 끄면 지움'
    Check (Test-Path "$root\StartMenu\$old.lnk") "다른 프로그램을 가리키는 '$old' 바로가기는 그대로"

    Write-Host "[옛 Inno Setup 설치본(1.0, 옛 이름 '$old') 위에 업데이트]"
    Reset
    $null = New-Item -ItemType Directory -Force $app, "$data\logs", "$root\StartMenu"
    Set-Content "$app\$id.exe" 'old'; Set-Content "$app\unins000.exe" 'x'; Set-Content "$app\unins000.dat" 'x'; Set-Content "$app\LICENSE.txt" 'x'
    Set-Content "$data\settings.ini" 'DeleteXml=0'; Set-Content "$data\logs\20261012_203814.txt" 'a	b'
    NewLink "$root\StartMenu\$old.lnk" "$app\$id.exe"
    NewLink "$root\Desktop\$old.lnk" "$app\$id.exe"
    NewLink "$root\StartMenu\$old 제거.lnk" "$app\unins000.exe"            # 1.1이 만든 시작 메뉴 항목
    $null = New-Item -Force $ukey
    Set-ItemProperty $ukey 'DisplayName' $old; Set-ItemProperty $ukey 'DisplayVersion' '1.0'; Set-ItemProperty $ukey 'InstallLocation' "$app\"
    Set-ItemProperty $ukey 'UninstallString' "`"$app\unins000.exe`""; Set-ItemProperty $ukey 'Inno Setup: App Path' $app
    $null = New-Item -Force "$reg\Classes\Directory\shell\$id\command"
    Check ((Run $setup '/silent', $sb) -eq 0) '끝 코드 0'
    Check ((Get-Item "$app\$id.exe").Length -gt 1000) '프로그램을 새 버전으로 바꿈'
    Check (-not (Test-Path "$app\unins000.exe") -and -not (Test-Path "$app\unins000.dat")) 'Inno Setup 제거 파일(unins000) 지움'
    $u = Get-ItemProperty $ukey
    Check ($u.DisplayName -eq $name -and $u.DisplayVersion -eq $version -and -not $u.'Inno Setup: App Path') "[설정 › 앱] 항목을 새 이름으로 새로 씀 ($($u.DisplayName) $($u.DisplayVersion))"
    Check ((Get-Content "$data\settings.ini") -eq 'DeleteXml=0' -and (Test-Path "$data\logs\20261012_203814.txt")) '설정·정리 기록 그대로'
    Check (-not (Test-Path "$root\StartMenu\$old.lnk") -and -not (Test-Path "$root\Desktop\$old.lnk")) "옛 이름 '$old'의 시작 메뉴·바탕화면 바로가기 지움"
    Check (-not (Test-Path "$root\StartMenu\$old 제거.lnk")) "1.1의 시작 메뉴 '$old 제거' 지움"
    Check ((LinkTarget $startLink) -eq "$app\$id.exe" -and (LinkTarget $deskLink) -eq "$app\$id.exe") "새 이름 '$name'의 시작 메뉴·바탕화면 바로가기 (바탕화면 아이콘이 있던 설치본)"
    Check ((Get-ItemProperty "$reg\Classes\Directory\shell\$id").MUIVerb -eq $menuText) "우클릭 메뉴가 있던 설치본은 새 이름으로 새로 씀 ('$menuText')"

    Write-Host '[프로그램이 실행 중이면 막음]'
    $running = Start-Process "$app\$id.exe" -PassThru -WindowStyle Minimized
    Start-Sleep -Milliseconds 1500
    Check ((Run $setup '/silent', $sb) -eq 2) '설치: 끝 코드 2'
    Check ((Run $uninstall '/silent', $sb) -eq 2) '제거: 끝 코드 2'
    Stop-Process -Id $running.Id -Force; $running.WaitForExit()
    Start-Sleep -Milliseconds 500

    Write-Host '[제거 (설정 › 앱과 같은 경로: 설치 폴더의 Uninstall.exe)]'
    Get-ChildItem $env:TEMP -Filter "$id-Uninstall-*.exe" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    $q = (Get-ItemProperty $ukey).QuietUninstallString
    Check ($q -eq "`"$app\Uninstall.exe`" /silent `"/sandbox=$root`"") '조용한 제거 명령'
    Check ((Run "$app\Uninstall.exe" '/silent', $sb) -eq 0) '끝 코드 0'
    Check (-not (Test-Path $app)) '설치 폴더 지움 (실행 중이던 Uninstall.exe 포함)'
    Check (-not (Test-Path $startLink) -and -not (Test-Path $deskLink)) '바로가기 지움'
    Check (-not (Test-Path "$reg\Classes\Directory\shell\$id") -and -not (Test-Path "$reg\Classes\Directory\Background\shell\$id")) '우클릭 메뉴 지움'
    Check (-not (Test-Path $data)) '설정·기록 지움'
    Check (-not (Test-Path $ukey)) '[설정 › 앱] 항목 지움'
    Start-Sleep -Seconds 4
    Check (-not (Get-ChildItem $env:TEMP -Filter "$id-Uninstall-*.exe" -ErrorAction SilentlyContinue)) '임시로 비켜난 제거 프로그램도 잠시 뒤 지워짐'

    Write-Host '[설치되지 않은 것을 제거]'
    Check ((Run $uninstall '/silent', $sb) -eq 3) '끝 코드 3'

    Write-Host '[설치 위치를 바꿔 둔 설치본]'
    Reset
    $custom = "$root\내 프로그램\$id"
    $null = New-Item -Force $ukey
    Set-ItemProperty $ukey 'DisplayVersion' '1.2'; Set-ItemProperty $ukey 'InstallLocation' "$custom\"
    Check ((Run $setup '/silent', $sb) -eq 0) '끝 코드 0'
    Check ((Test-Path "$custom\$id.exe") -and -not (Test-Path "$app\$id.exe")) '원래 설치 위치에 업데이트'
    Check (-not (Test-Path $deskLink)) '바탕화면 아이콘이 없던 설치본은 그대로 없음'
    Check ((Run $uninstall '/silent', $sb) -eq 0 -and -not (Test-Path $custom)) '그 위치에서 제거'
} finally {
    Reset
}
Write-Host ($(if ($script:failures -eq 0) { "`n설치·제거 테스트 모두 통과" } else { "`n설치·제거 테스트 실패 $($script:failures)개" }))
exit $script:failures

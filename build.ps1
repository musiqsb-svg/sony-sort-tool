# SONY 타입 정리기 빌드: Windows에 기본으로 들어 있는 C# 컴파일러(.NET Framework 4.x)만 사용. 따로 설치할 것 없음
#   dist\SonySortTool.exe     프로그램 (설치 없이도 바로 실행됨)
#   dist\Uninstall.exe        제거 프로그램 (배포의 'SONY 타입 정리기 제거.exe' = 설치 폴더의 Uninstall.exe)
#   dist\Setup.exe            설치 프로그램 (위 두 파일과 라이선스를 안에 담음)
#   build\OrganizerTest.exe   정리 로직 테스트
#   -Test    : 빌드 후 테스트 (정리 로직 + 설치·제거. 설치·제거 테스트는 임시 폴더에서만 하고 실제 설치본은 건드리지 않음)
#   -Release : 테스트 후 배포 파일까지 (버전은 src\Product.cs 의 Version)
#     release\SonySortTool-<버전>.zip   ← 이것 하나만 배포. 압축을 풀면:
#         SONY 타입 정리기 설치.exe · SONY 타입 정리기 제거.exe · 사용 안내.txt
#     release\SHA256.txt                 배포 글에 함께 올릴 파일 지문
param([switch]$Test, [switch]$Release)
$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
Set-Location $PSScriptRoot
[Environment]::CurrentDirectory = $PSScriptRoot   # .NET 메서드의 상대 경로도 이 폴더 기준으로
Add-Type -AssemblyName System.Drawing

$product = [IO.File]::ReadAllText('src\Product.cs')
$version = [regex]::Match($product, '\bVersion\s*=\s*"([^"]+)"').Groups[1].Value
$name    = [regex]::Match($product, '\bName\s*=\s*"([^"]+)"').Groups[1].Value   # \b: OldName과 헷갈리지 않게
if (-not $version -or -not $name) { throw 'src\Product.cs 에서 이름·버전을 찾지 못함' }
if ([IO.File]::ReadAllText('src\app.manifest') -notmatch ('version="' + [regex]::Escape($version) + '\.0\.0"')) { throw "src\app.manifest 의 버전을 $version.0.0 으로 고쳐 주세요" }

if (-not ((Test-Path 'assets\app.ico') -and (Test-Path 'assets\setup.ico') -and (Test-Path 'assets\uninstall.ico'))) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'assets\make_icon.ps1'
}
$null = New-Item -ItemType Directory -Force 'dist', 'build', 'build\art'

# ── 그림: assets\art 원본(Codex로 만든 큰 PNG)을 화면에 쓰는 크기로 줄여 넣음 (프로그램이 무거워지지 않게) ──
function Save-Art([string]$src, [string]$dst, [int]$w, [int]$h) {
    $img = [Drawing.Image]::FromFile((Resolve-Path $src).Path)
    try {
        $bmp = New-Object Drawing.Bitmap $w, $h, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'; $g.CompositingQuality = 'HighQuality'
        $wrap = New-Object Drawing.Imaging.ImageAttributes; $wrap.SetWrapMode('TileFlipXY')
        $g.DrawImage($img, (New-Object Drawing.Rectangle 0, 0, $w, $h), 0, 0, $img.Width, $img.Height, 'Pixel', $wrap)
        $g.Dispose()
        if ($dst.EndsWith('.jpg')) {
            $jpeg = [Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
            $q = New-Object Drawing.Imaging.EncoderParameters 1
            $q.Param[0] = New-Object Drawing.Imaging.EncoderParameter ([Drawing.Imaging.Encoder]::Quality), 90L
            $bmp.Save((Join-Path $PSScriptRoot $dst), $jpeg, $q)
        } else { $bmp.Save((Join-Path $PSScriptRoot $dst), [Drawing.Imaging.ImageFormat]::Png) }
        $bmp.Dispose()
    } finally { $img.Dispose() }
}
$appArt = @(); $setupArt = @()
if (Test-Path 'assets\art\drop.png') { Save-Art 'assets\art\drop.png' 'build\art\drop.png' 256 256; $appArt += '/resource:build\art\drop.png,drop.png' }
else { Write-Warning 'assets\art\drop.png 이 없어 그림 없이 빌드' }
if (Test-Path 'assets\art\hero.png') { Save-Art 'assets\art\hero.png' 'build\art\hero.jpg' 960 640; $setupArt += '/resource:build\art\hero.jpg,hero.jpg' }
else { Write-Warning 'assets\art\hero.png 이 없어 설치 화면은 단색 바탕' }

# ── 컴파일 ──
$common = '/nologo', '/codepage:65001', '/optimize+', '/platform:anycpu', '/warn:4', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll',
          '/win32manifest:src\app.manifest', '/resource:assets\app.ico,app.ico'
$ui = 'src\Product.cs', 'src\Ui.cs'

& $csc @common /target:winexe /out:dist\SonySortTool.exe /win32icon:assets\app.ico @appArt src\*.cs
if ($LASTEXITCODE) { throw 'SonySortTool.exe 빌드 실패' }

& $csc @common /target:winexe /out:dist\Uninstall.exe /win32icon:assets\uninstall.ico @setupArt setup\Setup.cs @ui
if ($LASTEXITCODE) { throw 'Uninstall.exe 빌드 실패' }

& $csc @common /define:INSTALLER /target:winexe /out:dist\Setup.exe /win32icon:assets\setup.ico @setupArt `
       '/resource:dist\SonySortTool.exe,payload.app' '/resource:dist\Uninstall.exe,payload.uninstall' '/resource:LICENSE,payload.license' setup\Setup.cs @ui
if ($LASTEXITCODE) { throw 'Setup.exe 빌드 실패' }

& $csc /nologo /codepage:65001 /optimize+ /warn:4 /target:exe /out:build\OrganizerTest.exe tests\OrganizerTest.cs src\Organizer.cs
if ($LASTEXITCODE) { throw 'OrganizerTest.exe 빌드 실패' }

foreach ($f in 'dist\SonySortTool.exe', 'dist\Uninstall.exe', 'dist\Setup.exe') { "빌드: $f  ($([math]::Round((Get-Item $f).Length / 1KB)) KB)" }

if ($Test -or $Release) {
    & 'build\OrganizerTest.exe'; if ($LASTEXITCODE) { throw '정리 로직 테스트 실패' }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'tests\SetupTest.ps1'; if ($LASTEXITCODE) { throw '설치·제거 테스트 실패' }
}
if (-not $Release) { return }

# ── 배포 파일: zip 하나 (설치 · 제거 · 사용 안내) ──
$rel = Join-Path $PSScriptRoot 'release'
$null = New-Item -ItemType Directory -Force $rel
$zip = Join-Path $rel "SonySortTool-$version.zip"
$guide = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'setup\guide.txt')).Replace('{VERSION}', $version) -replace "`r?`n", "`r`n"
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
if (Test-Path $zip) { [IO.File]::Delete($zip) }
# 한글 파일 이름은 UTF-8 표시와 함께 저장됨 (Windows 탐색기·반디집·macOS에서 깨지지 않음)
$za = [IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($za, (Resolve-Path 'dist\Setup.exe').Path, "$name 설치.exe", 'Optimal')
    $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($za, (Resolve-Path 'dist\Uninstall.exe').Path, "$name 제거.exe", 'Optimal')
    $entry = $za.CreateEntry('사용 안내.txt', 'Optimal')
    $sw = New-Object IO.StreamWriter ($entry.Open(), (New-Object Text.UTF8Encoding($true)))   # 메모장에서 한글이 깨지지 않게 BOM
    $sw.Write($guide); $sw.Dispose()
} finally { $za.Dispose() }
"배포: $zip  ($([math]::Round((Get-Item $zip).Length / 1KB)) KB)"

# 배포 글에 올릴 파일 지문 (받은 사람이 같은 파일인지 확인용). 메모장에서 한글이 깨지지 않게 UTF-8(BOM)
$lines = @('{0}  {1}' -f (Get-FileHash $zip -Algorithm SHA256).Hash, (Split-Path $zip -Leaf))
$lines += '{0}  {1} 설치.exe' -f (Get-FileHash 'dist\Setup.exe' -Algorithm SHA256).Hash, $name
$lines += '{0}  {1} 제거.exe' -f (Get-FileHash 'dist\Uninstall.exe' -Algorithm SHA256).Hash, $name
[IO.File]::WriteAllLines((Join-Path $rel 'SHA256.txt'), [string[]]$lines, (New-Object Text.UTF8Encoding($true)))
$lines

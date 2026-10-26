# 그림 에셋 (Codex 이미지 생성)

`assets/art`의 그림은 Codex CLI의 이미지 생성으로 만들었습니다. 다시 만들 때는 아래 명령을 이 폴더에서 실행합니다.
빌드(`build.ps1`)가 이 PNG들을 프로그램·설치 프로그램 안에 넣으므로, 파일 이름을 바꾸지 마세요.

| 파일 | 쓰는 곳 | 크기 |
|---|---|---|
| `hero.png` | 설치·제거 프로그램 머리 그림, README | 1536×1024 (가로로 잘라 씀) |
| `drop.png` | 프로그램의 '폴더를 끌어다 놓는 곳' 그림 | 1024×1024, 투명 배경 |

공통 원칙: 글자·숫자·로고 없음 (Sony 로고·제품 디자인을 쓰지 않음), 브랜드 색 = 주황 `#EA580C`

```
codex exec -s workspace-write --skip-git-repo-check "<아래 프롬프트>"
```

## hero.png

> Premium minimalist 3D illustration for a desktop app installer header. Wide landscape 3:2 (1536x1024).
> Scene: a sleek generic mirrorless camera body (no brand, no logo, no text anywhere), a small SD memory card,
> and three neat rounded folder tiles arranged like calendar pages, suggesting video files being sorted by shooting date.
> Soft matte clay-like materials, warm orange (#EA580C), peach and cream palette, soft studio lighting, gentle shadows,
> smooth warm gradient background from deep orange to soft apricot.
> Composition: objects grouped in the right half, slightly floating; the left 45% is calm empty gradient space for an overlaid title.
> Absolutely no text, letters, numbers, logos, watermarks or UI elements.

## drop.png

> A single clean 3D icon illustration on a fully transparent background (PNG with alpha).
> Subject: an SD memory card dropping into a neat stack of three rounded folders with tiny calendar tabs, conveying "sorted by date".
> Soft matte clay material, orange (#EA580C) and cream palette, soft contact shadow.
> Square 1024x1024, subject centered with generous padding (fills about 70%).
> No text, letters, numbers or logos.

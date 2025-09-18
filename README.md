# 📘 ytDownloader (yt-dlp)

유튜브 영상을 GUI로 손쉽게 저장하는 **WPF 기반 yt-dlp 프론트엔드**입니다.  
`yt-dlp.exe` + `ffmpeg.exe`를 사용하며, **영상/음악/자막/썸네일**, **채널 최신 영상 자동 다운로드**, **설정 저장**, **자동 업데이트**를 지원합니다.

---

## ✨ 주요 기능

- 🎬 **영상 다운로드 (MP4)**
- 🎵 **음악만 추출 (MP3)** — `--extract-audio --audio-format mp3`
- 📝 **자막 다운로드/삽입**
  - 원본 + 자동 생성 자막 지원 (`--write-subs --write-auto-subs`)
  - 언어/포맷 선택 (`--sub-langs`, `--sub-format srt|vtt`)
  - 영상에 자막 삽입 (`--embed-subs`)
- 🖼 **썸네일 저장** (`--write-thumbnail`)
- 🗂 **구조화 폴더 저장** (채널/플레이리스트 기준 경로 구성)
- 📡 **채널 구독 다운로드**
  - 최신 N개만 다운로드 (`--max-downloads N`)
  - MP4/MP3, 자막, 썸네일, 폴더 구조 옵션 동일 적용
- 🧭 **단일 영상만** (플레이리스트 URL에서도 단일 추출) — `--no-playlist`
- 💾 **설정 저장/복원**
- 🔄 **yt-dlp 자동 업데이트**
- 📊 **진행률/속도/ETA**, 📜 **실시간 로그 출력**

---

## 🖥 설치 및 폴더 구조

1. 릴리스 빌드를 준비합니다(예: `net8.0-windows`).  
2. 실행 폴더에 `tools` 폴더를 만들고 다음 파일을 넣습니다.
   ```text
   YourApp/
   ├─ YouTubeDownloaderGUI.exe
   ├─ config.txt              # (자동 생성/사용, 저장 경로 보관)
   └─ tools/
      ├─ yt-dlp.exe
      └─ ffmpeg.exe
   ```
3. 처음 실행 시 **yt-dlp 최신 버전 확인/자동 업데이트**가 수행됩니다.

> ⚠️ `yt-dlp.exe`, `ffmpeg.exe`가 없으면 다운로드가 실행되지 않습니다.

---

## 🚀 사용 방법

### 1) 기본 다운로드 (여러 URL 가능)
1. **저장 경로**를 확인/선택합니다. (기본: 사용자 `다운로드` 폴더 / 마지막 경로 자동 복원)
2. URL 입력창에 **영상 또는 플레이리스트 URL**을 붙여넣습니다.
3. 상단 **형식**에서 선택:
   - `Video (MP4)` → 영상 저장
   - `Music (MP3)` → 오디오만 추출
4. (선택) **자막 다운로드** 체크 → `언어 불러오기` 클릭 후 언어/포맷 선택  
5. (선택) **썸네일 저장**, **채널/플레이리스트 폴더** 체크  
6. (선택) **단일 영상만** 체크 (플레이리스트 URL에서도 한 개만)
7. **↓ 다운로드** 버튼 클릭 → 로그/진행 상태 확인

### 2) 채널 구독 다운로드 (최신 N개)
1. **채널(또는 플레이리스트) URL**을 입력
2. **최대 다운로드 개수(N)** 입력 (기본 5)
3. 위의 **형식/자막/썸네일/폴더 구조 옵션**이 동일하게 적용됩니다.
4. **구독 다운로드** 클릭 → 최신 N개만 자동 저장

---

## 🧩 내부적으로 사용하는 yt-dlp 옵션 예시

- MP4(영상):
  ```bash
  yt-dlp -f "bv*+ba/best" --merge-output-format mp4 --windows-filenames     -o "<저장경로>/%(title)s.%(ext)s" --newline <URL>
  ```
- MP3(음악):
  ```bash
  yt-dlp --extract-audio --audio-format mp3 --windows-filenames     -o "<저장경로>/%(title)s.%(ext)s" --newline <URL>
  ```
- 자막:
  ```bash
  --write-subs --write-auto-subs --sub-langs <ko|en|...> --sub-format <srt|vtt> --embed-subs
  ```
- 썸네일:
  ```bash
  --write-thumbnail
  ```
- 채널 구독(중복 방지/최신 N개):
  ```bash
  --download-archive "<저장경로>/archive.txt" --max-downloads N
  ```
- 구조화 폴더(예):
  ```bash
  -o "<저장경로>/%(uploader)s/%(playlist)s/%(title)s.%(ext)s"
  ```

---

## 🛠 문제 해결 (FAQ)

- **MP3 저장에서 오류**: `--merge-output-format mp3`는 지원되지 않습니다. 본 앱은 `--extract-audio --audio-format mp3`로 처리합니다.
- **자막이 안 나와요**: 원본 자막이 없고 자동 생성만 있을 수 있습니다. 자동 자막을 위해 `--write-auto-subs`가 포함되어 있습니다.
- **자막이 영상에 안 붙어요**: `--embed-subs`가 포함되어 있으며, FFmpeg가 필요합니다.
- **한글 파일명/로그 깨짐**: UTF-8 로깅을 사용합니다.

---

## 📄 라이선스/주의사항

- 본 프로그램은 **GUI 래퍼**이며, 다운로드 콘텐츠의 저작권 및 이용 약관 준수는 사용자 책임입니다.
- `yt-dlp`, `FFmpeg`의 라이선스를 확인하시기 바랍니다.

---

## 🗓 변경 이력 (요약)

- MP3 추출 방식 수정 (`--extract-audio`)
- 자막 옵션 강화 (`--write-auto-subs`, `--sub-langs`, `--embed-subs`)
- 채널 구독 다운로드에 옵션 통합 (MP4/MP3, 자막, 썸네일, 폴더 구조)
- 실행 시 **yt-dlp 자동 업데이트**


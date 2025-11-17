# ytDownloader - 기술 문서

이 문서는 ytDownloader의 기술적인 세부 사항, 아키텍처, 개발 정보를 다룹니다.

---

## 목차

- [기술 스택](#기술-스택)
- [프로젝트 구조](#프로젝트-구조)
- [아키텍처](#아키텍처)
- [주요 컴포넌트](#주요-컴포넌트)
- [외부 의존성](#외부-의존성)
- [빌드 및 배포](#빌드-및-배포)
- [자동 업데이트 시스템](#자동-업데이트-시스템)
- [설정 관리](#설정-관리)
- [다국어 지원](#다국어-지원)
- [테마 시스템](#테마-시스템)
- [개발 가이드](#개발-가이드)

---

## 기술 스택

### 프레임워크 및 플랫폼
- **.NET 8.0**: 최신 .NET 플랫폼
- **WPF (Windows Presentation Foundation)**: UI 프레임워크
- **C# 12**: 프로그래밍 언어

### 외부 도구
- **yt-dlp**: YouTube 및 다양한 플랫폼의 동영상 다운로드
- **ffmpeg**: 동영상/오디오 처리 및 변환

### NuGet 패키지
- **Ookii.Dialogs.Wpf** (5.0.1): 폴더 선택 다이얼로그
- **Newtonsoft.Json** (13.0.3): JSON 직렬화/역직렬화

---

## 프로젝트 구조

```
ytDownloader/
├── ytDownloader/                # 메인 애플리케이션
│   ├── App.xaml                 # 애플리케이션 진입점
│   ├── App.xaml.cs
│   ├── MainWindow.xaml          # 메인 UI
│   ├── MainWindow.xaml.cs
│   ├── ScheduleSettingsWindow.xaml  # 예약 설정 UI
│   ├── ScheduleSettingsWindow.xaml.cs
│   ├── AboutWindow.xaml         # 정보 창
│   ├── AboutWindow.xaml.cs
│   │
│   ├── Models/                  # 데이터 모델
│   │   ├── AppSettings.cs       # 앱 설정 모델
│   │   ├── DownloadOptions.cs   # 다운로드 옵션
│   │   ├── VideoFormat.cs       # 비디오 포맷 정의
│   │   ├── SchedulerSettings.cs # 스케줄러 설정
│   │   └── ScheduledChannel.cs  # 예약된 채널 정보
│   │
│   ├── Services/                # 비즈니스 로직
│   │   ├── DownloadService.cs   # 다운로드 처리
│   │   ├── SettingsService.cs   # 설정 관리
│   │   ├── AppUpdateService.cs  # 앱 업데이트
│   │   ├── ToolUpdateService.cs # yt-dlp/ffmpeg 업데이트
│   │   └── TaskSchedulerService.cs # Windows 작업 스케줄러 관리
│   │
│   ├── Resources/               # 다국어 리소스
│   │   ├── Korean.xaml
│   │   └── English.xaml
│   │
│   ├── Themes/                  # 테마 스타일
│   │   ├── LightTheme.xaml
│   │   └── DarkTheme.xaml
│   │
│   └── Properties/
│       └── Settings.settings    # 사용자 설정
│
├── Updater/                     # 업데이터 프로그램
│   └── Updater.csproj
│
└── tools/                       # 외부 도구 (런타임)
    ├── yt-dlp.exe
    └── ffmpeg.exe
```

---

## 아키텍처

### MVVM 패턴
프로젝트는 부분적으로 MVVM (Model-View-ViewModel) 패턴을 따릅니다:
- **View**: XAML 파일 (MainWindow.xaml, ScheduleSettingsWindow.xaml 등)
- **Code-Behind**: UI 이벤트 처리 및 간단한 로직
- **Model**: 데이터 구조 (Models 폴더)
- **Service**: 비즈니스 로직 및 외부 API 통신 (Services 폴더)

### 레이어 구조
```
┌─────────────────────────────────┐
│    Presentation Layer (UI)      │
│  MainWindow, ScheduleSettings   │
└────────────┬────────────────────┘
             │
┌────────────▼────────────────────┐
│     Service Layer               │
│  Download, Settings, Update     │
└────────────┬────────────────────┘
             │
┌────────────▼────────────────────┐
│    External Tools Layer         │
│   yt-dlp.exe, ffmpeg.exe        │
└─────────────────────────────────┘
```

---

## 주요 컴포넌트

### 1. DownloadService
`Services/DownloadService.cs`

yt-dlp를 사용하여 다운로드를 처리하는 핵심 서비스입니다.

**주요 기능**:
- 단일 동영상 다운로드
- 채널/플레이리스트 다운로드
- 진행 상황 파싱 및 UI 업데이트
- 자막 다운로드
- 메타데이터 및 썸네일 처리

**프로세스 실행**:
```csharp
Process ytDlpProcess = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "tools/yt-dlp.exe",
        Arguments = BuildArguments(options),
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    }
};
```

### 2. TaskSchedulerService
`Services/TaskSchedulerService.cs`

Windows 작업 스케줄러와 통합하여 예약 다운로드를 관리합니다.

**주요 기능**:
- 작업 스케줄러 작업 생성
- 작업 수정 및 삭제
- 실행 주기 및 시간 설정
- 관리자 권한 처리

**작업 스케줄러 등록**:
- COM Interop을 통해 Windows Task Scheduler API 사용
- 트리거 설정 (일일, 주간 등)
- 실행 계정 및 권한 설정

### 3. AppUpdateService
`Services/AppUpdateService.cs`

GitHub Releases API를 사용하여 애플리케이션 업데이트를 확인합니다.

**업데이트 확인 프로세스**:
1. GitHub API에서 최신 릴리스 정보 가져오기
2. 현재 버전과 비교
3. 업데이트 가능 시 사용자에게 알림
4. 다운로드 및 Updater.exe 실행

### 4. ToolUpdateService
`Services/ToolUpdateService.cs`

yt-dlp와 ffmpeg의 최신 버전을 자동으로 다운로드하고 설치합니다.

**업데이트 프로세스**:
- GitHub Releases에서 최신 버전 확인
- 바이너리 다운로드
- tools 폴더에 설치
- 실행 권한 설정

### 5. SettingsService
`Services/SettingsService.cs`

애플리케이션 설정과 예약 설정을 JSON 파일로 저장/로드합니다.

**저장 위치**:
```
%APPDATA%\ytDownloader\
├── settings.json          # 앱 설정
└── scheduler_settings.json # 예약 설정
```

**설정 항목**:
- 저장 경로
- 기본 포맷
- 자막 설정
- 테마 및 언어
- 예약된 채널 목록

---

## 외부 의존성

### yt-dlp
- **버전**: 최신 안정 버전 (자동 업데이트)
- **역할**: 동영상 다운로드 및 정보 추출
- **저장소**: https://github.com/yt-dlp/yt-dlp

**주요 사용 인자**:
```bash
yt-dlp.exe
  --format "bestvideo[height<=1080]+bestaudio/best"
  --output "%(title)s.%(ext)s"
  --merge-output-format mp4
  --embed-thumbnail
  --embed-metadata
  --write-sub
  --sub-lang ko
  --convert-subs srt
  --ffmpeg-location "tools/ffmpeg.exe"
  [URL]
```

### ffmpeg
- **버전**: 최신 안정 버전 (자동 업데이트)
- **역할**: 동영상/오디오 병합 및 변환
- **저장소**: https://github.com/BtbN/FFmpeg-Builds

**사용 목적**:
- 비디오와 오디오 스트림 병합
- 포맷 변환
- 메타데이터 임베딩
- 썸네일 임베딩

---

## 빌드 및 배포

### 로컬 빌드

**필요 사항**:
- .NET 8.0 SDK
- Visual Studio 2022 또는 VS Code

**빌드 명령**:
```bash
# Debug 빌드
dotnet build

# Release 빌드
dotnet build -c Release

# 단일 파일 게시
dotnet publish -c Release -r win-x64 --self-contained
```

### GitHub Actions CI/CD

프로젝트는 GitHub Actions를 사용하여 자동 빌드 및 배포를 수행합니다.

**워크플로우**:
1. 태그 푸시 시 트리거
2. .NET 8.0 환경 설정
3. 버전 정보 주입 (`-p:Version`)
4. Release 빌드
5. yt-dlp 및 ffmpeg 다운로드
6. 아티팩트 패키징
7. GitHub Release 생성
8. 업데이트 알림 시스템 동작

**빌드 아티팩트**:
- `ytDownloader.exe`
- `Updater.exe`
- `tools/yt-dlp.exe`
- `tools/ffmpeg.exe`
- 필요한 DLL 및 리소스 파일

---

## 자동 업데이트 시스템

### 업데이트 확인 메커니즘

**앱 업데이트**:
1. `AppUpdateService`가 GitHub Releases API 호출
2. 최신 릴리스의 태그 버전과 현재 버전 비교
3. 새 버전 발견 시 다운로드 URL 제공
4. 사용자 확인 후 업데이트 진행

**yt-dlp/ffmpeg 업데이트**:
1. `ToolUpdateService`가 각 도구의 GitHub Releases 확인
2. 버전 비교 또는 파일 존재 여부 확인
3. 자동 다운로드 및 설치
4. 첫 실행 시 자동으로 최신 버전 설치

### 업데이터 프로세스

`Updater.exe`는 별도의 프로세스로 실행되어 메인 애플리케이션을 업데이트합니다:

1. 메인 앱 종료 대기
2. 새 버전 다운로드
3. 기존 파일 백업
4. 새 파일로 교체
5. 메인 앱 재시작

---

## 설정 관리

### 설정 저장 방식

**AppData 경로**:
```
%APPDATA%\ytDownloader\
```

**settings.json 예시**:
```json
{
  "SavePath": "C:\\Users\\Username\\Downloads",
  "DefaultFormat": "best",
  "SubtitleEnabled": true,
  "SubtitleLanguage": "ko",
  "SubtitleFormat": "srt",
  "Theme": "Dark",
  "Language": "Korean"
}
```

**scheduler_settings.json 예시**:
```json
{
  "FrequencyDays": 1,
  "ExecutionHour": 2,
  "ExecutionMinute": 0,
  "ScheduledChannels": [
    {
      "Url": "https://www.youtube.com/@channel",
      "Name": "My Channel",
      "SavePath": "C:\\Videos",
      "Format": "1080p",
      "SubtitleEnabled": true,
      "SubtitleLanguage": "ko",
      "SubtitleFormat": "srt",
      "NotificationEnabled": true,
      "MaxDownloads": 10
    }
  ]
}
```

---

## 다국어 지원

### 리소스 사전 방식

WPF의 Resource Dictionary를 사용하여 다국어를 지원합니다.

**지원 언어**:
- 한국어 (`Resources/Korean.xaml`)
- 영어 (`Resources/English.xaml`)

**사용 방법**:
```xaml
<TextBlock Text="{DynamicResource Label_SavePath}" />
```

**런타임 언어 변경**:
```csharp
ResourceDictionary dict = new ResourceDictionary
{
    Source = new Uri($"Resources/{language}.xaml", UriKind.Relative)
};
Application.Current.Resources.MergedDictionaries.Add(dict);
```

---

## 테마 시스템

### 다크/라이트 테마

**테마 파일**:
- `Themes/DarkTheme.xaml`
- `Themes/LightTheme.xaml`

**테마 구성 요소**:
- 색상 브러시 정의
- 버튼, 텍스트박스 등 컨트롤 스타일
- 윈도우 배경 및 전경색

**테마 전환**:
```csharp
ResourceDictionary theme = new ResourceDictionary
{
    Source = new Uri($"Themes/{themeName}Theme.xaml", UriKind.Relative)
};
Application.Current.Resources.MergedDictionaries.Add(theme);
```

---

## 개발 가이드

### 코딩 규칙

- **네이밍**:
  - PascalCase: 클래스, 메서드, 속성
  - camelCase: 로컬 변수, 매개변수
  - _camelCase: private 필드
- **비동기**: I/O 작업은 `async/await` 사용
- **예외 처리**: 사용자에게 명확한 오류 메시지 제공
- **주석**: XML 문서 주석 사용

### 새 기능 추가

1. **모델 정의**: `Models/` 폴더에 데이터 모델 생성
2. **서비스 구현**: `Services/` 폴더에 비즈니스 로직 작성
3. **UI 연결**: XAML과 Code-Behind에서 서비스 호출
4. **설정 연동**: 필요시 `SettingsService`에 설정 추가
5. **다국어 리소스**: 모든 문자열을 리소스 사전에 추가

### 디버깅

**로그 출력**:
- 현재 콘솔 출력 방식
- 추후 로그 파일 시스템 구현 권장

**yt-dlp 출력 확인**:
```csharp
process.OutputDataReceived += (sender, e) =>
{
    if (!string.IsNullOrEmpty(e.Data))
    {
        Console.WriteLine(e.Data);
    }
};
```

### 테스트

**수동 테스트 체크리스트**:
- [ ] 단일 동영상 다운로드
- [ ] 플레이리스트 다운로드
- [ ] 자막 다운로드
- [ ] 예약 다운로드 설정
- [ ] 테마 변경
- [ ] 언어 변경
- [ ] 업데이트 확인

---

## 보안 고려사항

### 관리자 권한

- 예약 작업 등록 시 UAC 권한 상승 필요
- `app.manifest`에 `requireAdministrator` 설정

### 네트워크 통신

- GitHub API 호출 시 HTTPS 사용
- API 제한 고려 (rate limiting)

### 파일 다운로드

- 사용자가 지정한 경로에만 파일 저장
- 악의적인 URL 필터링 고려 (yt-dlp 자체에서 처리)

---

## 알려진 제한사항

1. **Windows 전용**: WPF 및 Task Scheduler 의존성으로 Windows만 지원
2. **yt-dlp 의존**: yt-dlp의 기능과 제약사항을 그대로 상속
3. **YouTube 정책**: YouTube의 다운로드 정책 변경에 영향받을 수 있음
4. **네트워크 필요**: 다운로드 및 업데이트에 인터넷 연결 필요

---

## 향후 개선 사항

- [ ] 로그 파일 시스템 구현
- [ ] 다운로드 이력 관리
- [ ] 다운로드 큐 시스템
- [ ] 프록시 설정 지원
- [ ] 사용자 정의 yt-dlp 인자 입력
- [ ] 다운로드 속도 제한 설정
- [ ] 더 많은 언어 지원

---

## 참고 자료

- [yt-dlp 공식 문서](https://github.com/yt-dlp/yt-dlp)
- [ffmpeg 공식 문서](https://ffmpeg.org/documentation.html)
- [WPF 가이드](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
- [.NET 8.0 문서](https://docs.microsoft.com/en-us/dotnet/core/)
- [Windows Task Scheduler API](https://docs.microsoft.com/en-us/windows/win32/taskschd/task-scheduler-start-page)

---

## 라이선스

이 프로젝트는 오픈소스이며, yt-dlp와 ffmpeg의 라이선스를 준수합니다.

**의존 도구 라이선스**:
- yt-dlp: Unlicense
- ffmpeg: LGPL/GPL (빌드 옵션에 따름)

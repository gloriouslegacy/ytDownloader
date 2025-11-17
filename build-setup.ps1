# ==============================================
# ytDownloader 전체 빌드 스크립트 (설치파일 포함)
# ==============================================

param(
    [string]$Version = "0.0.0-manual"
)

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "ytDownloader 전체 빌드 시작" -ForegroundColor Cyan
Write-Host "버전: $Version" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Inno Setup 설치 확인
$innoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $innoSetupPath)) {
    Write-Host "경고: Inno Setup이 설치되지 않았습니다." -ForegroundColor Yellow
    Write-Host "설치파일 생성은 건너뜁니다. 포터블 ZIP만 생성됩니다." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Inno Setup 설치: choco install innosetup -y" -ForegroundColor Cyan
    Write-Host ""
    $createSetup = $false
} else {
    $createSetup = $true
}

# 빌드 폴더 정리
if (Test-Path publish) {
    Remove-Item publish -Recurse -Force
}
if (Test-Path dist) {
    Remove-Item dist -Recurse -Force
}

# 1. ytDownloader 빌드
Write-Host "[1/5] ytDownloader 빌드 중..." -ForegroundColor Green
dotnet publish ytDownloader/ytDownloader.csproj -c Release -r win-x64 --self-contained false -o publish/ytDownloader -p:Version=$Version
if ($LASTEXITCODE -ne 0) {
    Write-Host "오류: ytDownloader 빌드 실패!" -ForegroundColor Red
    exit 1
}

# 2. Updater 빌드
Write-Host "[2/5] Updater 빌드 중..." -ForegroundColor Green
dotnet build Updater/Updater.csproj -c Release -p:Version=$Version
if ($LASTEXITCODE -ne 0) {
    Write-Host "오류: Updater 빌드 실패!" -ForegroundColor Red
    exit 1
}

New-Item -ItemType Directory -Path publish/Updater -Force | Out-Null
Copy-Item "Updater/bin/Release/net8.0-windows/*" -Destination "publish/Updater/" -Recurse

# 3. 배포 패키지 생성
Write-Host "[3/5] 배포 패키지 생성 중..." -ForegroundColor Green
New-Item -ItemType Directory -Path dist -Force | Out-Null
New-Item -ItemType Directory -Path dist/ytDownloader -Force | Out-Null
Copy-Item publish/ytDownloader/* dist/ytDownloader/ -Recurse

# Updater 서브폴더 복사
New-Item -ItemType Directory -Path dist/ytDownloader/updater -Force | Out-Null
Copy-Item publish/Updater/* dist/ytDownloader/updater/ -Recurse

# 불필요한 파일 제거
if (Test-Path dist/ytDownloader/tools) {
    Remove-Item dist/ytDownloader/tools -Recurse -Force
}
Get-ChildItem dist/ytDownloader -Include *.pdb -Recurse | Remove-Item -Force

# 4. ZIP 생성
Write-Host "[4/5] ZIP 파일 생성 중..." -ForegroundColor Green
if (Test-Path ytdownloader.zip) {
    Remove-Item ytdownloader.zip -Force
}
Compress-Archive -Path dist/ytDownloader/* -DestinationPath ytdownloader.zip -Force

# Updater ZIP 생성
New-Item -ItemType Directory -Path dist/Updater -Force | Out-Null
Copy-Item publish/Updater/* dist/Updater/ -Recurse
if (Test-Path ytupdater.zip) {
    Remove-Item ytupdater.zip -Force
}
Compress-Archive -Path dist/Updater/* -DestinationPath ytupdater.zip -Force

# 5. Inno Setup 설치파일 생성
if ($createSetup) {
    Write-Host "[5/5] 설치파일 생성 중..." -ForegroundColor Green

    # setup.iss 파일이 이미 존재하는지 확인
    if (-not (Test-Path setup.iss)) {
        Write-Host "경고: setup.iss 파일이 없습니다. 기본 템플릿을 생성합니다..." -ForegroundColor Yellow

        # 기본 setup.iss 생성
        @"
#define MyAppName "ytDownloader"
#define MyAppVersion "$Version"
#define MyAppPublisher "gloriouslegacy"
#define MyAppURL "https://github.com/gloriouslegacy/ytDownloader"
#define MyAppExeName "ytDownloader.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={userappdata}\{#MyAppName}\app
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=ytDownloader-setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
Source: "publish\ytDownloader\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Flags: nowait postinstall

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\{#MyAppName}\app"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  SettingsPath: String;
  ResultCode: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    SettingsPath := ExpandConstant('{userappdata}\{#MyAppName}\settings.json');

    if MsgBox('설정 파일을 삭제하시겠습니까?' + #13#10 + SettingsPath, mbConfirmation, MB_YESNO) = IDYES then
    begin
      if FileExists(SettingsPath) then
        DeleteFile(SettingsPath);
    end;
  end;
end;
"@ | Out-File -FilePath setup.iss -Encoding UTF8
    }

    # Inno Setup 실행
    & $innoSetupPath setup.iss
    if ($LASTEXITCODE -ne 0) {
        Write-Host "오류: 설치파일 생성 실패!" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "[5/5] 설치파일 생성 건너뜀 (Inno Setup 미설치)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "빌드 완료!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "출력 파일:" -ForegroundColor Yellow
Write-Host "  - ytdownloader.zip (포터블)" -ForegroundColor White
Write-Host "  - ytupdater.zip (Updater)" -ForegroundColor White
if ($createSetup) {
    Write-Host "  - ytDownloader-setup.exe (설치파일)" -ForegroundColor White
}
Write-Host ""
Write-Host "빌드 폴더: dist\" -ForegroundColor Yellow
Write-Host ""

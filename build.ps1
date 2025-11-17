# ==============================================
# ytDownloader 포터블 빌드 스크립트 (PowerShell)
# ==============================================

param(
    [string]$Version = "0.0.0-manual"
)

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "ytDownloader 포터블 빌드 시작" -ForegroundColor Cyan
Write-Host "버전: $Version" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 빌드 폴더 정리
if (Test-Path publish) {
    Remove-Item publish -Recurse -Force
}
if (Test-Path dist) {
    Remove-Item dist -Recurse -Force
}

# 1. ytDownloader 빌드
Write-Host "[1/4] ytDownloader 빌드 중..." -ForegroundColor Green
dotnet publish ytDownloader/ytDownloader.csproj -c Release -r win-x64 --self-contained false -o publish/ytDownloader -p:Version=$Version
if ($LASTEXITCODE -ne 0) {
    Write-Host "오류: ytDownloader 빌드 실패!" -ForegroundColor Red
    exit 1
}

# 2. Updater 빌드
Write-Host "[2/4] Updater 빌드 중..." -ForegroundColor Green
dotnet build Updater/Updater.csproj -c Release -p:Version=$Version
if ($LASTEXITCODE -ne 0) {
    Write-Host "오류: Updater 빌드 실패!" -ForegroundColor Red
    exit 1
}

New-Item -ItemType Directory -Path publish/Updater -Force | Out-Null
Copy-Item "Updater/bin/Release/net8.0-windows/*" -Destination "publish/Updater/" -Recurse

# 3. 배포 패키지 생성
Write-Host "[3/4] 배포 패키지 생성 중..." -ForegroundColor Green
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
Write-Host "[4/4] ZIP 파일 생성 중..." -ForegroundColor Green
if (Test-Path ytdownloader.zip) {
    Remove-Item ytdownloader.zip -Force
}
Compress-Archive -Path dist/ytDownloader/* -DestinationPath ytdownloader.zip -Force

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "빌드 완료!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "출력 파일: ytdownloader.zip" -ForegroundColor Yellow
Write-Host "빌드 폴더: dist\ytDownloader" -ForegroundColor Yellow
Write-Host ""

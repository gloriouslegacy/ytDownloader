@echo off
REM ==============================================
REM ytDownloader 간단 빌드 스크립트
REM ==============================================

echo.
echo ========================================
echo ytDownloader 빌드 시작
echo ========================================
echo.

REM 빌드 폴더 정리
if exist build rmdir /s /q build
mkdir build

REM 1. ytDownloader 빌드
echo [1/2] ytDownloader 빌드 중...
dotnet publish ytDownloader/ytDownloader.csproj -c Release -r win-x64 --self-contained false -o build/ytDownloader
if %ERRORLEVEL% neq 0 (
    echo 오류: ytDownloader 빌드 실패!
    pause
    exit /b 1
)

REM 2. Updater 빌드
echo [2/2] Updater 빌드 중...
dotnet build Updater/Updater.csproj -c Release
if %ERRORLEVEL% neq 0 (
    echo 오류: Updater 빌드 실패!
    pause
    exit /b 1
)

REM Updater 파일을 updater 서브폴더로 복사
mkdir build\ytDownloader\updater
xcopy /E /I /Y "Updater\bin\Release\net8.0-windows\*" "build\ytDownloader\updater\"

echo.
echo ========================================
echo 빌드 완료!
echo ========================================
echo.
echo 출력 폴더: build\ytDownloader
echo.

pause

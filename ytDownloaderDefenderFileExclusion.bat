@echo off
REM 관리자 권한으로 실행하는지 확인하고, 아니면 다시 요청합니다.
:checkAdmin
NET SESSION >nul 2>&1
if NOT %ERRORLEVEL% EQU 0 (
    echo 관리자 권한이 필요합니다. UAC 승인을 요청합니다...
    powershell -Command "Start-Process '%~dpnx0' -Verb RunAs"
    exit /b
)

REM --- UAC 승인 후 실제 작업 영역 시작 ---

echo.
echo =======================================================
echo     PowerShell을 이용한 Windows Defender 제외 파일 추가
echo =======================================================

REM 제외할 파일의 경로를 설정합니다. (PowerShell에서 사용할 경로 포맷)
set "ExclusionFile=%%APPDATA%%\ytDownloader\app\ytDownloader.exe"

echo.
echo 대상 파일: %ExclusionFile%

REM PowerShell을 실행하여 Add-MpPreference cmdlet으로 제외 항목을 추가합니다.
powershell -ExecutionPolicy Bypass -Command "Add-MpPreference -ExclusionPath '%ExclusionFile%'"

REM 에러 코드 확인 (PowerShell 실행 자체의 에러 코드만 확인 가능)
if errorlevel 1 (
    echo.
    echo 실패: 제외 항목 추가 명령 실행 중 오류가 발생했습니다.
    echo    (PowerShell 스크립트 내부 오류는 표시되지 않을 수 있습니다.)
    goto :end
)

echo.
echo 성공: %ExclusionFile% 파일이 Windows Defender 제외 목록에 추가되었을 가능성이 높습니다.
echo (PowerShell을 사용하여 안정적으로 처리되었습니다.)

:end
echo.
pause
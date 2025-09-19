using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

class Program
{
    private static readonly string LogFile = Path.Combine(Path.GetTempPath(), "ytDownloader_updater.log");

    static void Log(string msg)
    {
        File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");
    }

    [STAThread]
    static int Main(string[] args)
    {
        Log("=== Updater 시작 ===");

        if (args.Length < 3)
        {
            Log("인자가 부족합니다.");
            return 1;
        }

        string zipPath = args[0];
        string installDir = args[1];
        string targetExe = args[2];

        Log($"zipPath   = {zipPath}");
        Log($"installDir= {installDir}");
        Log($"targetExe = {targetExe}");

        try
        {
            if (!File.Exists(zipPath))
                throw new FileNotFoundException("ZIP 파일을 찾을 수 없습니다.", zipPath);

            // 🔹 ZIP 유효성 검사
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                if (archive.Entries.Count == 0)
                    throw new InvalidDataException("다운로드된 ZIP이 비어있습니다.");

                Log($"ZIP 유효성 통과 (파일 {archive.Entries.Count}개)");
            }

            // 🔹 현재 실행 중인 ytDownloader 종료 대기
            foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(targetExe)))
            {
                try
                {
                    Log($"기존 프로세스 종료 대기: {p.ProcessName} (PID {p.Id})");
                    p.Kill();
                    p.WaitForExit();
                }
                catch (Exception ex)
                {
                    Log($"프로세스 종료 실패: {ex}");
                }
            }

            // 🔹 ZIP 압축 해제 (덮어쓰기)
            Log("ZIP 압축 해제 시작...");
            ZipFile.ExtractToDirectory(zipPath, installDir, overwriteFiles: true);
            Log("ZIP 압축 해제 완료");

            // 🔹 버전 확인 (exe의 FileVersionInfo 이용)
            string exePath = Path.Combine(installDir, Path.GetFileName(targetExe));
            if (File.Exists(exePath))
            {
                var fvi = FileVersionInfo.GetVersionInfo(exePath);
                string fileVersion = fvi.FileVersion ?? "";
                string productVersion = fvi.ProductVersion ?? "";

                // 🔹 날짜 제거: 0.3.15-20250919 → 0.3.15
                string normalizedVersion = productVersion.Split('-')[0];

                Log($"업데이트된 버전: FileVersion={fileVersion}, ProductVersion={productVersion}, Normalized={normalizedVersion}");
            }
            else
            {
                Log("경고: 업데이트 후 ytDownloader.exe를 찾을 수 없습니다.");
            }

            // 🔹 업데이트 완료 후 프로그램 재실행
            Log("업데이트 완료 → ytDownloader.exe 재실행");
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });

            return 0;
        }
        catch (Exception ex)
        {
            Log($"업데이트 실패: {ex}");
            System.Windows.MessageBox.Show(
                "업데이트 실패: " + ex.Message,
                "업데이트 오류",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            return 1;
        }
    }
}

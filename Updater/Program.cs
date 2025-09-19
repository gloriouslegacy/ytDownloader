using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

class Program
{
    private static readonly string LogFile =
        Path.Combine(Path.GetTempPath(), "ytDownloader_updater.log");

    static void Main(string[] args)
    {
        try
        {
            // 전역 예외 처리
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                File.AppendAllText(LogFile,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ❌ UnhandledException: {e.ExceptionObject}\n");
            };

            Log("=== Updater.exe 시작 ===");

            if (args.Length < 3)
            {
                Log("❌ 실행 인자가 부족합니다. 사용법: Updater.exe <zipPath> <installDir> <targetExe>");
                return;
            }

            string zipPath = args[0];
            string installDir = args[1];
            string targetExe = args[2];

            Log($"zipPath    = {zipPath}");
            Log($"installDir = {installDir}");
            Log($"targetExe  = {targetExe}");

            if (!File.Exists(zipPath))
            {
                Log("❌ ZIP 파일이 존재하지 않습니다.");
                return;
            }

            if (!Directory.Exists(installDir))
            {
                Log("📂 설치 폴더가 없어서 생성합니다.");
                Directory.CreateDirectory(installDir);
            }

            // 1. 대상 exe가 잠겨 있으면 잠금 해제 대기
            WaitForFileUnlock(targetExe);

            // 2. 압축 해제 (tools 폴더 제외)
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName.StartsWith("tools\\", StringComparison.OrdinalIgnoreCase))
                    {
                        Log($"↷ Skip tools: {entry.FullName}");
                        continue;
                    }

                    string destinationPath = Path.Combine(installDir, entry.FullName);

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destinationPath);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                    Log($"→ Extract {entry.FullName}");
                    entry.ExtractToFile(destinationPath, true);
                }
            }

            // 3. 완료 후 새 실행
            Log("✅ 업데이트 완료, ytDownloader 재실행 시도");
            Process.Start(new ProcessStartInfo
            {
                FileName = targetExe,
                UseShellExecute = true
            });

            Log("=== Updater.exe 종료 ===");
        }
        catch (Exception ex)
        {
            Log($"❌ 예외 발생: {ex}");
        }
    }

    private static void WaitForFileUnlock(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;

        for (int i = 0; i < 20; i++) // 최대 10초 대기
        {
            try
            {
                using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    Log($"🔓 파일 잠금 해제됨: {filePath}");
                    return;
                }
            }
            catch (IOException)
            {
                Log("⏳ 파일 잠김 상태, 500ms 대기");
                Thread.Sleep(500);
            }
        }
        Log("⚠️ 파일 잠금을 해제하지 못했습니다. 강행 진행.");
    }

    private static void Log(string message)
    {
        File.AppendAllText(LogFile,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
    }
}

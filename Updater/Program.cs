using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

class Program
{
    static int Main(string[] args)
    {
        string logPath = Path.Combine(Path.GetTempPath(), "ytDownloader_updater.log");

        try
        {
            if (args.Length < 3)
            {
                File.AppendAllText(logPath, "[Updater] ❌ 인자가 부족합니다.\r\n");
                return 1;
            }

            string zipPath = args[0];
            string installDir = args[1];
            string targetExe = args[2];

            File.AppendAllText(logPath,
                $"[Updater] 시작\r\nzipPath    = {zipPath}\r\ninstallDir = {installDir}\r\ntargetExe  = {targetExe}\r\n");

            // ✅ ZIP 파일 존재 여부 확인
            if (!File.Exists(zipPath))
                throw new FileNotFoundException("ZIP 파일을 찾을 수 없습니다.", zipPath);

            // ✅ ZIP 파일 유효성 검사
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                if (archive.Entries.Count == 0)
                    throw new Exception("ZIP 파일이 비어 있습니다.");
            }

            // ✅ 타겟 exe 잠금 해제 대기
            WaitForFileRelease(targetExe, logPath);

            // ✅ 압축 해제 (tools 폴더 제외)
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName.StartsWith("tools\\", StringComparison.OrdinalIgnoreCase))
                    {
                        File.AppendAllText(logPath, $"[Updater] Skip tools → {entry.FullName}\r\n");
                        continue;
                    }

                    string destinationPath = Path.Combine(installDir, entry.FullName);

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destinationPath);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    entry.ExtractToFile(destinationPath, true);
                    File.AppendAllText(logPath, $"[Updater] Extracted: {destinationPath}\r\n");
                }
            }

            // ✅ 원래 실행 파일 다시 시작
            Process.Start(new ProcessStartInfo
            {
                FileName = targetExe,
                UseShellExecute = true
            });

            File.AppendAllText(logPath, "[Updater] 완료 ✅\r\n");
            return 0;
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"[Updater] 실패 ❌ {ex}\r\n");
            return 1;
        }
    }

    // 🔹 실행 파일이 잠금 해제될 때까지 대기
    private static void WaitForFileRelease(string filePath, string logPath)
    {
        for (int i = 0; i < 20; i++) // 최대 10초 (500ms × 20)
        {
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    File.AppendAllText(logPath, "[Updater] Target exe 접근 가능\r\n");
                    return;
                }
            }
            catch
            {
                Thread.Sleep(500);
            }
        }
        File.AppendAllText(logPath, "[Updater] ⚠️ 파일 잠금 해제 대기 시간 초과\r\n");
    }
}

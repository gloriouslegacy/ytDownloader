using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace Updater
{
    internal class Program
    {
        private static string logFile = Path.Combine(Path.GetTempPath(), "ytDownloader_updater.log");

        static void Main(string[] args)
        {
            try
            {
                if (args.Length < 3)
                {
                    Log("❌ 잘못된 인자. 사용법: Updater.exe <zipPath> <installDir> <targetExe>");
                    return;
                }

                string zipPath = args[0];
                string installDir = args[1];
                string targetExe = args[2];

                Log("=== Updater 시작 ===");
                Log($"zipPath   = {zipPath}");
                Log($"installDir= {installDir}");
                Log($"targetExe = {targetExe}");

                // 1. ZIP 유효성 검사
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    if (archive.Entries == null || archive.Entries.Count == 0)
                    {
                        Log("❌ 업데이트 실패: ZIP 파일이 비어있거나 손상됨");
                        return;
                    }
                }

                // 2. 파일 잠금 해제 대기
                WaitForFileRelease(targetExe);

                // 3. 압축 해제 (tools 폴더 제외)
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.StartsWith("tools\\", StringComparison.OrdinalIgnoreCase))
                        {
                            Log($"⏭️ 제외됨: {entry.FullName}");
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
                        Log($"✅ 덮어씀: {destinationPath}");
                    }
                }

                // 4. 원래 프로그램 다시 실행
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExe,
                    UseShellExecute = true
                });

                Log("=== 업데이트 완료 후 프로그램 재실행 성공 ===");
            }
            catch (Exception ex)
            {
                Log($"❌ 예외 발생: {ex}");
            }
        }

        private static void WaitForFileRelease(string filePath)
        {
            for (int i = 0; i < 10; i++) // 최대 10초 대기
            {
                try
                {
                    using (FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        Log($"🔓 파일 잠금 해제 확인: {filePath}");
                        return;
                    }
                }
                catch (IOException)
                {
                    Log($"⏳ 파일 잠금 중... {filePath}");
                    Thread.Sleep(1000);
                }
            }

            Log($"⚠️ 파일 잠금 해제 실패: {filePath}");
        }

        private static void Log(string message)
        {
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(logFile, logEntry);
        }
    }
}

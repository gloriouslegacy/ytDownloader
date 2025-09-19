using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace Updater
{
    class Program
    {
        static void Main(string[] args)
        {
            string logFile = Path.Combine(Path.GetTempPath(), "ytDownloader_updater.log");

            try
            {
                if (args.Length < 3)
                {
                    Log(logFile, "❌ 잘못된 인자. 사용법: Updater.exe <zipPath> <installDir> <targetExe>");
                    return;
                }

                string zipPath = args[0];
                string installDir = args[1];
                string targetExe = args[2];

                Log(logFile, "=== Updater 시작 ===");
                Log(logFile, $"zipPath   = {zipPath}");
                Log(logFile, $"installDir= {installDir}");
                Log(logFile, $"targetExe = {targetExe}");

                // 📌 1. ZIP 유효성 검사
                if (!File.Exists(zipPath))
                    throw new FileNotFoundException("업데이트 ZIP 파일이 존재하지 않습니다.", zipPath);

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    if (archive.Entries == null || archive.Entries.Count == 0)
                        throw new InvalidDataException("ZIP 파일이 비어 있거나 손상되었습니다.");
                }

                // 📌 2. 파일 잠금 해제 대기
                WaitForFileRelease(targetExe, logFile);

                // 📌 3. 압축 해제 (tools 폴더 제외)
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        // tools 폴더는 무시
                        if (entry.FullName.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.StartsWith("tools\\", StringComparison.OrdinalIgnoreCase))
                        {
                            Log(logFile, $"⏭️ 제외됨: {entry.FullName}");
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
                        Log(logFile, $"✅ 덮어씀: {destinationPath}");
                    }
                }

                // 📌 4. 원래 프로그램 다시 실행
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExe,
                    UseShellExecute = true,
                    WorkingDirectory = installDir
                });

                Log(logFile, "=== 업데이트 완료 후 프로그램 재실행 성공 ===");
            }
            catch (Exception ex)
            {
                Log(logFile, $"❌ 업데이트 실패: {ex}");
            }
        }

        private static void WaitForFileRelease(string filePath, string logFile)
        {
            for (int i = 0; i < 10; i++) // 최대 10초 대기
            {
                try
                {
                    using (FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        Log(logFile, $"🔓 파일 잠금 해제 확인: {filePath}");
                        return;
                    }
                }
                catch (IOException)
                {
                    Log(logFile, $"⏳ 파일 잠금 중... {filePath}");
                    Thread.Sleep(1000);
                }
            }

            Log(logFile, $"⚠️ 파일 잠금 해제 실패: {filePath}");
        }

        private static void Log(string logFile, string message)
        {
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(logFile, logEntry);
        }
    }
}

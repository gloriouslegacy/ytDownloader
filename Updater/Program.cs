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
        static int Main(string[] args)
        {
            string logFile = Path.Combine(Path.GetTempPath(), "ytDownloader_updater.log");

            void Log(string message)
            {
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                Console.WriteLine(line);
                try
                {
                    File.AppendAllText(logFile, line + Environment.NewLine);
                }
                catch { /* 로그 파일 쓰기 실패는 무시 */ }
            }

            try
            {
                if (args.Length < 3)
                {
                    Log("인자가 부족합니다. [zipPath] [installDir] [targetExe]");
                    return 1;
                }

                string zipPath = args[0];
                string installDir = args[1];
                string targetExe = args[2];

                Log($"📌 인자 확인");
                Log($"zipPath   = {zipPath}");
                Log($"installDir= {installDir}");
                Log($"targetExe = {targetExe}");

                // 대상 exe 종료 대기 (최대 10초)
                Log("대상 프로세스 종료 대기...");
                for (int i = 0; i < 20; i++)
                {
                    var procs = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(targetExe));
                    if (procs.Length == 0) break;
                    Thread.Sleep(500);
                }

                // 압축 해제 (tools 폴더 제외)
                Log("압축 해제 시작...");
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        // tools 폴더 제외
                        if (entry.FullName.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.StartsWith("tools\\", StringComparison.OrdinalIgnoreCase))
                        {
                            Log($"➡️ 제외됨: {entry.FullName}");
                            continue;
                        }

                        string destinationPath = Path.Combine(installDir, entry.FullName);

                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(destinationPath);
                            continue;
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                        Log($"➡️ 덮어쓰기: {destinationPath}");
                        entry.ExtractToFile(destinationPath, true);
                    }
                }
                Log("압축 해제 완료");

                // 대상 exe 재실행
                Log("프로그램 재실행...");
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExe,
                    WorkingDirectory = installDir,
                    UseShellExecute = true
                });

                Log("업데이트 성공");
                return 0;
            }
            catch (Exception ex)
            {
                Log($"예외 발생: {ex}");
                return 1;
            }
        }
    }
}

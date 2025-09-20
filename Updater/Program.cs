using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace Updater
{
    class Program
    {
        static int Main(string[] args)
        {
            string logFile = Path.Combine(Path.GetTempPath(), "ytDownloader_updater.log");

            try
            {
                File.AppendAllText(logFile, "\n==============================\n");
                File.AppendAllText(logFile, $"[Updater] 실행 시작: {DateTime.Now}\n");
                File.AppendAllText(logFile, $"[Updater] 전달된 인자: {string.Join(" | ", args)}\n");

                if (args.Length < 3)
                {
                    File.AppendAllText(logFile, "[Updater] ❌ 인자가 부족합니다.\n");
                    Console.WriteLine("업데이트 실행 인자가 부족합니다.");
                    return 1;
                }

                string zipPath = args[0];
                string installDir = args[1];
                string targetExe = args[2];

                File.AppendAllText(logFile, $"[Updater] zipPath   = {zipPath}\n");
                File.AppendAllText(logFile, $"[Updater] installDir= {installDir}\n");
                File.AppendAllText(logFile, $"[Updater] targetExe = {targetExe}\n");

                if (!File.Exists(zipPath))
                {
                    File.AppendAllText(logFile, "[Updater] ❌ ZIP 파일이 존재하지 않습니다.\n");
                    Console.WriteLine("ZIP 파일이 존재하지 않습니다.");
                    return 1;
                }

                // 잠깐 대기 (ytDownloader.exe 종료될 시간 확보)
                Thread.Sleep(10000);

                // ZIP 열기
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    if (archive.Entries.Count == 0)
                    {
                        File.AppendAllText(logFile, "[Updater] ❌ ZIP 파일이 비어 있습니다.\n");
                        Console.WriteLine("ZIP 파일이 비어 있습니다.");
                        return 1;
                    }

                    foreach (var entry in archive.Entries)
                    {
                        // tools 폴더 제외
                        if (entry.FullName.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.StartsWith("tools\\", StringComparison.OrdinalIgnoreCase))
                        {
                            File.AppendAllText(logFile, $"[Updater] 제외됨: {entry.FullName}\n");
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
                        File.AppendAllText(logFile, $"[Updater] 파일 교체: {entry.FullName}\n");
                    }
                }

                File.AppendAllText(logFile, "[Updater] ✅ 업데이트 완료\n");
                Console.WriteLine("✅ 업데이트 완료!");

                // 실행 파일 재실행
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExe,
                    UseShellExecute = true
                });

                return 0;
            }
            catch (Exception ex)
            {
                File.AppendAllText(logFile, $"[Updater] ❌ 오류: {ex}\n");
                Console.WriteLine("업데이트 실패: " + ex.Message);
                return 1;
            }
        }
    }
}

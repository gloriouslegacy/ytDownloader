using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

class Program
{
    static void Main(string[] args)
    {
        string logFile = Path.Combine(Path.GetTempPath(), "ytDownloader_updater.log");

        try
        { 
            Console.WriteLine("업데이트를 시작합니다...");
            File.AppendAllText(logFile, "[Updater] Start update\n");

            if (args.Length < 3)
            {
                Console.WriteLine("인자가 부족합니다.");
                File.AppendAllText(logFile, "[Updater] Missing arguments\n");
                return;
            }

            string zipPath = args[0];
            string installDir = args[1];
            string targetExe = args[2];

            Console.WriteLine($"ZIP: {zipPath}");
            Console.WriteLine($"InstallDir: {installDir}");
            Console.WriteLine($"TargetExe: {targetExe}");

            // ZIP 유효성 검사
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                if (archive.Entries.Count == 0)
                {
                    Console.WriteLine("ZIP 파일이 비어있습니다.");
                    File.AppendAllText(logFile, "[Updater] Invalid ZIP (empty)\n");
                    return;
                }
            }

            // 압축 해제
            Console.WriteLine("압축 해제 중...");
            ZipFile.ExtractToDirectory(zipPath, installDir, overwriteFiles: true);

            Console.WriteLine("업데이트 완료!");
            File.AppendAllText(logFile, "[Updater] Update complete\n");

            // ytDownloader.exe 재실행
            Console.WriteLine("프로그램을 다시 실행합니다...");
            Process.Start(new ProcessStartInfo
            {
                FileName = targetExe,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"오류 발생: {ex.Message}");
            File.AppendAllText(logFile, $"[Updater] Error: {ex}\n");
        }
    }
}

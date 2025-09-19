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
                File.AppendAllText(logPath, "❌ Invalid arguments\r\n");
                return 1;
            }

            string zipPath = args[0];
            string installDir = args[1];
            string targetExe = args[2];

            File.AppendAllText(logPath, $"[Updater] zipPath={zipPath}\r\ninstallDir={installDir}\r\n");

            if (!File.Exists(zipPath))
                throw new FileNotFoundException("ZIP not found", zipPath);

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                if (archive.Entries.Count == 0)
                    throw new InvalidDataException("ZIP archive has no entries");
            }

            // ✅ 대상 프로세스 종료 대기
            for (int i = 0; i < 20; i++)
            {
                if (!IsFileLocked(targetExe))
                    break;
                Thread.Sleep(500);
            }

            // ✅ 압축 해제
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    string destinationPath = Path.Combine(installDir, entry.FullName);

                    if (entry.FullName.EndsWith("/"))
                        continue;

                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    entry.ExtractToFile(destinationPath, true);
                }
            }

            File.AppendAllText(logPath, "[Updater] Update applied successfully\r\n");

            // ✅ 새 실행
            Process.Start(new ProcessStartInfo
            {
                FileName = targetExe,
                WorkingDirectory = installDir,
                UseShellExecute = true
            });

            return 0;
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"❌ Update failed: {ex}\r\n");
            return 1;
        }
    }

    private static bool IsFileLocked(string filePath)
    {
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }
}

using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Windows;

namespace Updater
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            string logFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "ytDownloader_updater.log"
            );

            try
            {
                File.AppendAllText(logFile, "[Updater] 실행 시작\n");

                if (args.Length < 3)
                {
                    File.AppendAllText(logFile, "❌ 인자가 부족합니다.\n");
                    return;
                }

                string zipPath = args[0];
                string installDir = args[1];
                string targetExe = args[2];

                File.AppendAllText(logFile, $"zipPath={zipPath}\ninstallDir={installDir}\ntargetExe={targetExe}\n");

                Application app = new Application();
                var win = new UpdateWindow();
                win.Show();

                // 📌 압축 해제
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.StartsWith("tools/")) continue;

                        string destPath = Path.Combine(installDir, entry.FullName);
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                        if (!string.IsNullOrEmpty(entry.Name))
                            entry.ExtractToFile(destPath, true);
                    }
                }

                File.AppendAllText(logFile, "✅ 업데이트 완료\n");

                // 📌 ytDownloader.exe 다시 실행
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExe,
                    UseShellExecute = true
                });

                app.Shutdown();
            }
            catch (Exception ex)
            {
                File.AppendAllText(logFile, "❌ 오류: " + ex + "\n");
            }
        }
    }
}
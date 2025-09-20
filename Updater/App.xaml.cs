using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;

namespace Updater
{
    public partial class App : Application
    {
        private readonly string logFile =
            Path.Combine(Path.GetTempPath(), "ytDownloader_updater.log");

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string debugLog = Path.Combine(Path.GetTempPath(), "updater_debug.log");
            try
            {
                File.AppendAllText(debugLog, $"[INFO] [{DateTime.Now}] 시작됨\n");
                File.AppendAllText(debugLog, $"[INFO] Args: {string.Join(" ", e.Args)}\n");
            }
            catch { }

            var window = new UpdateWindow();
            MainWindow = window;
            window.Show();

            if (e.Args.Length < 3)
            {
                File.AppendAllText(debugLog, $"[ERROR] 잘못된 인자: {string.Join(", ", e.Args)}\n");
                window.UpdateStatus("잘못된 실행 인자입니다.");
                Shutdown();
                return;
            }

            string zipPath = e.Args[0];
            string installDir = e.Args[1];
            string targetExe = e.Args[2];

            Task.Run(() => RunUpdaterAsync(zipPath, installDir, targetExe, window));
        }


        private async Task RunUpdaterAsync(string zipPath, string installDir, string targetExe, UpdateWindow window)
        {
            try
            {
                File.AppendAllText(logFile, $"[INFO] Updater 시작: {DateTime.Now}\n");
                File.AppendAllText(logFile, $"[INFO] zipPath    = {zipPath}\n");
                File.AppendAllText(logFile, $"[INFO] installDir = {installDir}\n");
                File.AppendAllText(logFile, $"[INFO] targetExe  = {targetExe}\n");

                if (!File.Exists(zipPath))
                {
                    window.Dispatcher.Invoke(() =>
                        window.UpdateStatus("업데이트 ZIP 파일이 존재하지 않습니다."));
                    File.AppendAllText(logFile, "[ERROR] ZIP 파일 없음\n");
                    return;
                }

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    if (archive.Entries.Count == 0)
                    {
                        window.Dispatcher.Invoke(() =>
                            window.UpdateStatus("ZIP 파일이 비어 있습니다."));
                        File.AppendAllText(logFile, "[ERROR] ZIP 파일 비어 있음\n");
                        return;
                    }

                    foreach (var entry in archive.Entries)
                    {
                        // tools 폴더 제외
                        if (entry.FullName.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.StartsWith("tools\\", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string destinationPath = Path.Combine(installDir, entry.FullName);

                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(destinationPath);
                            continue;
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                        entry.ExtractToFile(destinationPath, true);
                        File.AppendAllText(logFile, $"[INFO] Extracted: {entry.FullName}\n");
                    }
                }

                window.Dispatcher.Invoke(() =>
                    window.UpdateStatus("업데이트 완료. 프로그램을 다시 시작합니다."));

                File.AppendAllText(logFile, "[INFO] 업데이트 성공\n");

                // 원래 실행파일 다시 실행
                await Task.Delay(2000);
                System.Diagnostics.Process.Start(targetExe);
            }
            catch (Exception ex)
            {
                window.Dispatcher.Invoke(() =>
                    window.UpdateStatus($"오류 발생: {ex.Message}"));
                File.AppendAllText(logFile, $"[ERROR] 예외: {ex}\n");
            }
            finally
            {
                await Task.Delay(2000);
                Shutdown();
            }
        }
    }
}

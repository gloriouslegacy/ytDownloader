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
                File.AppendAllText(debugLog, $"[INFO] [{DateTime.Now}] ���۵�\n");
                File.AppendAllText(debugLog, $"[INFO] Args: {string.Join(" ", e.Args)}\n");
            }
            catch { }

            var window = new UpdateWindow();
            MainWindow = window;
            window.Show();

            if (e.Args.Length < 3)
            {
                File.AppendAllText(debugLog, $"[ERROR] �߸��� ����: {string.Join(", ", e.Args)}\n");
                window.UpdateStatus("�߸��� ���� �����Դϴ�.");
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
                File.AppendAllText(logFile, $"[INFO] Updater ����: {DateTime.Now}\n");
                File.AppendAllText(logFile, $"[INFO] zipPath    = {zipPath}\n");
                File.AppendAllText(logFile, $"[INFO] installDir = {installDir}\n");
                File.AppendAllText(logFile, $"[INFO] targetExe  = {targetExe}\n");

                if (!File.Exists(zipPath))
                {
                    window.Dispatcher.Invoke(() =>
                        window.UpdateStatus("������Ʈ ZIP ������ �������� �ʽ��ϴ�."));
                    File.AppendAllText(logFile, "[ERROR] ZIP ���� ����\n");
                    return;
                }

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    if (archive.Entries.Count == 0)
                    {
                        window.Dispatcher.Invoke(() =>
                            window.UpdateStatus("ZIP ������ ��� �ֽ��ϴ�."));
                        File.AppendAllText(logFile, "[ERROR] ZIP ���� ��� ����\n");
                        return;
                    }

                    foreach (var entry in archive.Entries)
                    {
                        // tools ���� ����
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
                    window.UpdateStatus("������Ʈ �Ϸ�. ���α׷��� �ٽ� �����մϴ�."));

                File.AppendAllText(logFile, "[INFO] ������Ʈ ����\n");

                // ���� �������� �ٽ� ����
                await Task.Delay(2000);
                System.Diagnostics.Process.Start(targetExe);
            }
            catch (Exception ex)
            {
                window.Dispatcher.Invoke(() =>
                    window.UpdateStatus($"���� �߻�: {ex.Message}"));
                File.AppendAllText(logFile, $"[ERROR] ����: {ex}\n");
            }
            finally
            {
                await Task.Delay(2000);
                Shutdown();
            }
        }
    }
}

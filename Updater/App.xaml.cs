using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using System.Text;
using System.Diagnostics;

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
                // UTF-8�� �α� ���� �ۼ�
                File.AppendAllText(debugLog, $"[INFO] [{DateTime.Now}] ���۵�\n", Encoding.UTF8);
                File.AppendAllText(debugLog, $"[INFO] Args: {string.Join(" ", e.Args)}\n", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                File.AppendAllText(debugLog, $"[ERROR] �α� �ۼ� ����: {ex.Message}\n", Encoding.UTF8);
            }

            var window = new UpdateWindow();
            MainWindow = window;
            window.Show();

            if (e.Args.Length < 3)
            {
                File.AppendAllText(debugLog, $"[ERROR] �߸��� ����: {string.Join(", ", e.Args)}\n", Encoding.UTF8);
                window.UpdateStatus("�߸��� ���� �����Դϴ�.");
                Task.Delay(3000).ContinueWith(_ => Shutdown());
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
                // UTF-8�� �α� ���� �ۼ�
                File.AppendAllText(logFile, $"[INFO] Updater ����: {DateTime.Now}\n", Encoding.UTF8);
                File.AppendAllText(logFile, $"[INFO] zipPath    = {zipPath}\n", Encoding.UTF8);
                File.AppendAllText(logFile, $"[INFO] installDir = {installDir}\n", Encoding.UTF8);
                File.AppendAllText(logFile, $"[INFO] targetExe  = {targetExe}\n", Encoding.UTF8);

                // ���� ���� Ȯ��
                if (!File.Exists(zipPath))
                {
                    string errorMsg = "������Ʈ ZIP ������ �������� �ʽ��ϴ�.";
                    window.Dispatcher.Invoke(() => window.UpdateStatus(errorMsg));
                    File.AppendAllText(logFile, $"[ERROR] {errorMsg}: {zipPath}\n", Encoding.UTF8);
                    return;
                }

                window.Dispatcher.Invoke(() => window.UpdateStatus("������Ʈ ���� ���� ���� ��..."));

                // ZIP ���� ���� ����
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    if (archive.Entries.Count == 0)
                    {
                        string errorMsg = "ZIP ������ ��� �ֽ��ϴ�.";
                        window.Dispatcher.Invoke(() => window.UpdateStatus(errorMsg));
                        File.AppendAllText(logFile, $"[ERROR] {errorMsg}\n", Encoding.UTF8);
                        return;
                    }

                    File.AppendAllText(logFile, $"[INFO] ZIP ��Ʈ�� ����: {archive.Entries.Count}\n", Encoding.UTF8);

                    int processedCount = 0;
                    foreach (var entry in archive.Entries)
                    {
                        try
                        {
                            // tools ���� ����
                            if (entry.FullName.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) ||
                                entry.FullName.StartsWith("tools\\", StringComparison.OrdinalIgnoreCase))
                            {
                                File.AppendAllText(logFile, $"[INFO] Skipped: {entry.FullName} (tools ����)\n", Encoding.UTF8);
                                continue;
                            }

                            string destinationPath = Path.Combine(installDir, entry.FullName);
                            File.AppendAllText(logFile, $"[INFO] ó�� ��: {entry.FullName} -> {destinationPath}\n", Encoding.UTF8);

                            // ������ ���
                            if (string.IsNullOrEmpty(entry.Name))
                            {
                                if (!Directory.Exists(destinationPath))
                                {
                                    Directory.CreateDirectory(destinationPath);
                                    File.AppendAllText(logFile, $"[INFO] ���� ����: {destinationPath}\n", Encoding.UTF8);
                                }
                                continue;
                            }

                            // ������ ���
                            string? directoryPath = Path.GetDirectoryName(destinationPath);
                            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                            {
                                Directory.CreateDirectory(directoryPath);
                                File.AppendAllText(logFile, $"[INFO] ���� ���� ����: {directoryPath}\n", Encoding.UTF8);
                            }

                            // ���� ������ ��� ���� �� �����Ƿ� ��� ���
                            if (File.Exists(destinationPath))
                            {
                                await Task.Delay(100);
                                try
                                {
                                    File.Delete(destinationPath);
                                }
                                catch (Exception deleteEx)
                                {
                                    File.AppendAllText(logFile, $"[WARNING] ���� ���� ���� ����: {deleteEx.Message}\n", Encoding.UTF8);
                                }
                            }

                            // ���� ���� ����
                            entry.ExtractToFile(destinationPath, true);
                            File.AppendAllText(logFile, $"[INFO] ���� �Ϸ�: {entry.FullName}\n", Encoding.UTF8);
                            processedCount++;

                            // UI ������Ʈ
                            window.Dispatcher.Invoke(() =>
                                window.UpdateStatus($"���� ó�� ��... ({processedCount}/{archive.Entries.Count})"));
                        }
                        catch (Exception entryEx)
                        {
                            File.AppendAllText(logFile, $"[ERROR] ��Ʈ�� ó�� ���� ({entry.FullName}): {entryEx}\n", Encoding.UTF8);
                        }
                    }
                }

                window.Dispatcher.Invoke(() => window.UpdateStatus("������Ʈ �Ϸ�. ���α׷��� �ٽ� �����մϴ�."));
                File.AppendAllText(logFile, "[INFO] ������Ʈ ����\n", Encoding.UTF8);

                // ��� ��� �� ���� �������� �ٽ� ����
                await Task.Delay(2000);

                if (File.Exists(targetExe))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = targetExe,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(targetExe)
                    };

                    Process.Start(startInfo);
                    File.AppendAllText(logFile, $"[INFO] ���α׷� �����: {targetExe}\n", Encoding.UTF8);
                }
                else
                {
                    File.AppendAllText(logFile, $"[ERROR] ��� ���������� �������� ����: {targetExe}\n", Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"���� �߻�: {ex.Message}";
                window.Dispatcher.Invoke(() => window.UpdateStatus(errorMsg));
                File.AppendAllText(logFile, $"[ERROR] ����: {ex}\n", Encoding.UTF8);

                // ���� ���� ������ �α׿� ���
                File.AppendAllText(logFile, $"[ERROR] StackTrace: {ex.StackTrace}\n", Encoding.UTF8);
            }
            finally
            {
                // ZIP ���� ����
                try
                {
                    if (File.Exists(zipPath))
                    {
                        File.Delete(zipPath);
                        File.AppendAllText(logFile, "[INFO] �ӽ� ZIP ���� ���� �Ϸ�\n", Encoding.UTF8);
                    }
                }
                catch (Exception deleteEx)
                {
                    File.AppendAllText(logFile, $"[WARNING] ZIP ���� ���� ����: {deleteEx.Message}\n", Encoding.UTF8);
                }

                await Task.Delay(3000);
                Dispatcher.Invoke(() => Shutdown());
            }
        }
    }
}
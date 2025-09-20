using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;

namespace Updater
{
    public partial class App : Application
    {
        private string logFile = Path.Combine(
            Path.GetTempPath(),
            "ytDownloader_updater.log"
        );

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Length < 3)
            {
                File.AppendAllText(logFile, "? �߸��� ����: " + string.Join(", ", e.Args));
                Shutdown();
                return;
            }

            string zipPath = e.Args[0];
            string installDir = e.Args[1];
            string targetExe = e.Args[2];

            var window = (UpdateWindow)MainWindow;
            Task.Run(() => RunUpdaterAsync(zipPath, installDir, targetExe, window));
        }

        private async Task RunUpdaterAsync(string zipPath, string installDir, string targetExe, UpdateWindow window)
        {
            try
            {
                Log("�� ������Ʈ ����");
                window.UpdateStatus("������Ʈ �غ� ��...");

                if (!File.Exists(zipPath))
                {
                    Log("? ZIP ���� ����");
                    window.UpdateStatus("ZIP ������ ã�� �� �����ϴ�.");
                    return;
                }

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    if (archive.Entries.Count == 0)
                    {
                        Log("? ZIP ������ �������");
                        window.UpdateStatus("ZIP ������ ����ֽ��ϴ�.");
                        return;
                    }
                }

                Log("? ���� ���� ��...");
                window.UpdateStatus("���� ���� ��...");

                await Task.Run(() =>
                {
                    ZipFile.ExtractToDirectory(zipPath, installDir, true);
                });

                Log("? ���� ���� �Ϸ�");
                window.UpdateStatus("������Ʈ �Ϸ�!");

                Log("�� ytDownloader.exe ����");
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExe,
                    WorkingDirectory = installDir,
                    UseShellExecute = true
                });

                await Task.Delay(2000);
                Shutdown();
            }
            catch (Exception ex)
            {
                Log("? ����: " + ex);
                window.UpdateStatus("������Ʈ ����: " + ex.Message);
            }
        }

        private void Log(string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            File.AppendAllText(logFile, line + Environment.NewLine);
            Debug.WriteLine(line);
        }
    }
}

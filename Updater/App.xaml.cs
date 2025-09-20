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
                File.AppendAllText(logFile, "? 잘못된 인자: " + string.Join(", ", e.Args));
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
                Log("▶ 업데이트 시작");
                window.UpdateStatus("업데이트 준비 중...");

                if (!File.Exists(zipPath))
                {
                    Log("? ZIP 파일 없음");
                    window.UpdateStatus("ZIP 파일을 찾을 수 없습니다.");
                    return;
                }

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    if (archive.Entries.Count == 0)
                    {
                        Log("? ZIP 파일이 비어있음");
                        window.UpdateStatus("ZIP 파일이 비어있습니다.");
                        return;
                    }
                }

                Log("? 압축 해제 중...");
                window.UpdateStatus("압축 해제 중...");

                await Task.Run(() =>
                {
                    ZipFile.ExtractToDirectory(zipPath, installDir, true);
                });

                Log("? 압축 해제 완료");
                window.UpdateStatus("업데이트 완료!");

                Log("▶ ytDownloader.exe 실행");
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
                Log("? 오류: " + ex);
                window.UpdateStatus("업데이트 실패: " + ex.Message);
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

using System;
using System.IO;
using System.Windows;

namespace Updater
{
    public partial class UpdateWindow : Window
    {
        private readonly string logFile;

        public UpdateWindow()
        {
            InitializeComponent();

            // ✅ 바탕화면에 로그 저장
            logFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "ytDownloader_updater.log"
            );

            Log("Updater 실행됨");
        }

        public void UpdateProgress(double percent, string speed, string eta)
        {
            progressBar.Value = percent;
            txtProgress.Text = $"{percent:F1}%";
            txtSpeed.Text = speed;
            txtEta.Text = eta;

            Log($"진행률 {percent:F1}%, 속도={speed}, ETA={eta}");
        }

        private void Log(string message)
        {
            try
            {
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
            catch
            {
                // 로그 실패는 무시
            }
        }
    }
}
using System.Windows;

namespace ytDownloader
{
    public partial class UpdateWindow : Window
    {
        public UpdateWindow()
        {
            InitializeComponent();
        }

        // 다운로드 진행률 업데이트
        public void UpdateProgress(double percent, string speed, string eta)
        {
            progressBar.Value = percent;
            txtSpeed.Text = speed;
            txtEta.Text = eta;
        }

        // 상단 상태 문구 교체
        public void SetStatus(string text)
        {
            txtStatus.Text = text;
        }
    }
}



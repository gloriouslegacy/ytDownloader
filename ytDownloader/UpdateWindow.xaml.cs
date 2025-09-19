using System.Windows;

namespace ytDownloader
{
    public partial class UpdateWindow : Window
    {
        public UpdateWindow()
        {
            InitializeComponent();
        }

        // ? 진행률 갱신 메서드 (txtProgress 제거됨)
        public void UpdateProgress(double percent, string speed, string eta)
        {
            progressBar.Value = percent;
            txtSpeed.Text = speed;
            txtEta.Text = eta;
        }
    }
}


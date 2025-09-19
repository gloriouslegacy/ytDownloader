using System.Windows;

namespace ytDownloader
{
    public partial class UpdateWindow : Window
    {
        public UpdateWindow()
        {
            InitializeComponent();
        }

        // ? ����� ���� �޼��� (txtProgress ���ŵ�)
        public void UpdateProgress(double percent, string speed, string eta)
        {
            progressBar.Value = percent;
            txtSpeed.Text = speed;
            txtEta.Text = eta;
        }
    }
}


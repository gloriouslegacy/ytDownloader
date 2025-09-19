using System.Windows;

namespace ytDownloader
{
    public partial class UpdateWindow : Window
    {
        public UpdateWindow()
        {
            InitializeComponent();
        }

        // �ٿ�ε� ����� ������Ʈ
        public void UpdateProgress(double percent, string speed, string eta)
        {
            progressBar.Value = percent;
            txtSpeed.Text = speed;
            txtEta.Text = eta;
        }

        // ��� ���� ���� ��ü
        public void SetStatus(string text)
        {
            txtStatus.Text = text;
        }
    }
}



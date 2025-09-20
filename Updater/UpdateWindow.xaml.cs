using System.Text;
using System.Windows;

namespace Updater
{
    public partial class UpdateWindow : Window
    {
        public UpdateWindow()
        {
            // UTF-8 ���ڵ� ����
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            InitializeComponent();

            // ������ �Ӽ� ����
            this.Title = "������Ʈ ��";
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.ResizeMode = ResizeMode.NoResize;
            this.Topmost = true;
        }

        public void UpdateStatus(string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (txtStatus != null)
                {
                    txtStatus.Text = message;
                }
            });
        }
    }
}
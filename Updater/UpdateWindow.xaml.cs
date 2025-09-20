using System.Text;
using System.Windows;

namespace Updater
{
    public partial class UpdateWindow : Window
    {
        public UpdateWindow()
        {
            // UTF-8 인코딩 설정
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            InitializeComponent();

            // 윈도우 속성 설정
            this.Title = "업데이트 중";
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
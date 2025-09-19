using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;

namespace ytDownloader
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();

            // 실행 중인 어셈블리에서 버전 가져오기
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
            VersionText.Text = $"버전: {version}";
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show("브라우저를 열 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

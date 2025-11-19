using System.Diagnostics;
using System.Windows;

namespace ytDownloader
{
    public partial class DefenderWarningWindow : Window
    {
        private const string GITHUB_RELEASES_URL = "https://github.com/gloriouslegacy/ytDownloader/releases";

        public DefenderWarningWindow()
        {
            InitializeComponent();
        }

        private void btnDownloadExclusion_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open GitHub releases page in default browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = GITHUB_RELEASES_URL,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open URL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

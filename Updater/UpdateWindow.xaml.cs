using System.Windows;

namespace Updater
{
    public partial class UpdateWindow : Window
    {
        public UpdateWindow()
        {
            InitializeComponent();
        }

        public void UpdateProgress(double percent, string speed, string eta)
        {
            progressBar.Value = percent;
            txtProgress.Text = $"{percent:F1}%";
            txtSpeed.Text = speed;
            txtEta.Text = eta;
        }
    }
}
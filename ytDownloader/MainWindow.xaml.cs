using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;
using ytDownloader.Models;
using ytDownloader.Services;

// 2025-09-21 .NET 8.0, C# 12.0
// ✅ 자동 업데이트
// ❌ 웹뷰 내장
// ❌ 채널 예약 다운로드
// ❌ 라이트 / 다크 모드 전환
// ❌ 드래그 앤 드롭
// ❌ 다운로드 정지/일시정지/재개
// ❌ 다운로드 후 알림
// ❌ 다국어 지원

namespace ytDownloader
{
    public partial class MainWindow : Window
    {
        // 서비스
        private readonly SettingsService _settingsService;
        private readonly ToolUpdateService _toolUpdateService;
        private readonly AppUpdateService _appUpdateService;
        private readonly DownloadService _downloadService;

        // 현재 설정
        private AppSettings _currentSettings;

        public MainWindow()
        {
            InitializeComponent();

            // 서비스 초기화
            _settingsService = new SettingsService();
            _toolUpdateService = new ToolUpdateService();
            _appUpdateService = new AppUpdateService();
            _downloadService = new DownloadService();

            // 서비스 이벤트 구독
            _toolUpdateService.LogMessage += AppendOutput;
            _appUpdateService.LogMessage += AppendOutput;
            _downloadService.LogMessage += AppendOutput;
            _downloadService.ProgressChanged += OnDownloadProgressChanged;

            // 설정 로드 및 UI 초기화
            _currentSettings = _settingsService.LoadSettings();
            LoadSettingsToUI();
            AttachSettingsEventHandlers();

            // 도구 및 앱 업데이트 시작
            _ = UpdateToolsAndAppSequentiallyAsync();
        }

        /// <summary>
        /// 도구 및 앱 업데이트 순차 실행
        /// </summary>
        private async Task UpdateToolsAndAppSequentiallyAsync()
        {
            await Task.Run(async () =>
            {
                await _toolUpdateService.UpdateAllToolsAsync();
                await CheckForUpdateAsync();
            });
        }

        /// <summary>
        /// 앱 업데이트 확인
        /// </summary>
        private async Task CheckForUpdateAsync()
        {
            var updateInfo = await _appUpdateService.CheckForUpdateAsync();

            if (updateInfo != null && updateInfo.UpdateAvailable)
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    string preMsg = updateInfo.IsPrerelease ? "Pre-release" : "정식 릴리스";
                    if (MessageBox.Show(
                        $"새 {preMsg} {updateInfo.LatestVersion} 버전이 있습니다. 업데이트 하시겠습니까?",
                        "업데이트 확인",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        bool success = await _appUpdateService.RunUpdateAsync(updateInfo);
                        if (success)
                        {
                            Application.Current.Shutdown();
                        }
                        else
                        {
                            MessageBox.Show("업데이트 실행 실패", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                });
            }
        }

        /// <summary>
        /// 설정을 UI에 로드
        /// </summary>
        private void LoadSettingsToUI()
        {
            txtSavePath.Text = _currentSettings.SavePath;
            ChkSingleVideo.IsChecked = _currentSettings.SingleVideoOnly;
            SubtitleCheckBox.IsChecked = _currentSettings.DownloadSubtitle;
            SetComboBoxValue(SubtitleLangComboBox, _currentSettings.SubtitleLang);
            SetComboBoxValue(SubtitleFormatComboBox, _currentSettings.SubtitleFormat);
            ChkWriteThumbnail.IsChecked = _currentSettings.SaveThumbnail;
            ChkStructuredFolders.IsChecked = _currentSettings.UseStructuredFolder;
            comboFormat.SelectedIndex = (int)_currentSettings.Format;
            txtMaxDownloads.Text = _currentSettings.MaxDownloads.ToString();
        }

        /// <summary>
        /// UI 설정 변경 이벤트 핸들러 연결
        /// </summary>
        private void AttachSettingsEventHandlers()
        {
            txtSavePath.TextChanged += (s, e) => SaveCurrentSettings();
            ChkSingleVideo.Checked += (s, e) => SaveCurrentSettings();
            ChkSingleVideo.Unchecked += (s, e) => SaveCurrentSettings();
            SubtitleCheckBox.Checked += (s, e) => SaveCurrentSettings();
            SubtitleCheckBox.Unchecked += (s, e) => SaveCurrentSettings();
            SubtitleLangComboBox.SelectionChanged += (s, e) => SaveCurrentSettings();
            SubtitleLangComboBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new System.Windows.Controls.TextChangedEventHandler((s, e) => SaveCurrentSettings()));
            SubtitleFormatComboBox.SelectionChanged += (s, e) => SaveCurrentSettings();
            SubtitleFormatComboBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new System.Windows.Controls.TextChangedEventHandler((s, e) => SaveCurrentSettings()));
            ChkWriteThumbnail.Checked += (s, e) => SaveCurrentSettings();
            ChkWriteThumbnail.Unchecked += (s, e) => SaveCurrentSettings();
            ChkStructuredFolders.Checked += (s, e) => SaveCurrentSettings();
            ChkStructuredFolders.Unchecked += (s, e) => SaveCurrentSettings();
            comboFormat.SelectionChanged += (s, e) => SaveCurrentSettings();
            txtMaxDownloads.TextChanged += (s, e) => SaveCurrentSettings();
        }

        /// <summary>
        /// 현재 UI 설정을 저장
        /// </summary>
        private void SaveCurrentSettings()
        {
            try
            {
                _currentSettings.SavePath = txtSavePath.Text;
                _currentSettings.SingleVideoOnly = ChkSingleVideo.IsChecked ?? false;
                _currentSettings.DownloadSubtitle = SubtitleCheckBox.IsChecked ?? false;
                _currentSettings.SubtitleLang = GetComboBoxValue(SubtitleLangComboBox);
                _currentSettings.SubtitleFormat = GetComboBoxValue(SubtitleFormatComboBox);
                _currentSettings.SaveThumbnail = ChkWriteThumbnail.IsChecked ?? false;
                _currentSettings.UseStructuredFolder = ChkStructuredFolders.IsChecked ?? false;
                _currentSettings.Format = (VideoFormat)(comboFormat.SelectedIndex >= 0 ? comboFormat.SelectedIndex : 0);
                _currentSettings.MaxDownloads = int.TryParse(txtMaxDownloads.Text, out int n) ? n : 5;

                _settingsService.SaveSettings(_currentSettings);
            }
            catch (Exception ex)
            {
                AppendOutput($"❌ 설정 저장 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// ComboBox에 값 설정
        /// </summary>
        private void SetComboBoxValue(System.Windows.Controls.ComboBox comboBox, string value)
        {
            if (comboBox.IsEditable)
            {
                comboBox.Text = value;
            }
            else
            {
                for (int i = 0; i < comboBox.Items.Count; i++)
                {
                    var item = comboBox.Items[i];
                    string itemValue = "";

                    if (item is System.Windows.Controls.ComboBoxItem comboBoxItem)
                    {
                        itemValue = comboBoxItem.Content?.ToString() ?? "";
                    }
                    else
                    {
                        itemValue = item?.ToString() ?? "";
                    }

                    if (itemValue == value)
                    {
                        comboBox.SelectedIndex = i;
                        return;
                    }
                }
                comboBox.Text = value;
            }
        }

        /// <summary>
        /// ComboBox에서 값 가져오기
        /// </summary>
        private string GetComboBoxValue(System.Windows.Controls.ComboBox comboBox)
        {
            if (comboBox.SelectedItem != null)
            {
                if (comboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item)
                {
                    return item.Content?.ToString() ?? "";
                }
                return comboBox.SelectedItem.ToString() ?? "";
            }
            return comboBox.Text ?? "";
        }

        /// <summary>
        /// 로그 출력
        /// </summary>
        private void AppendOutput(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtOutput.AppendText(message + Environment.NewLine);
                txtOutput.ScrollToEnd();
            });
        }

        /// <summary>
        /// 다운로드 진행률 변경 이벤트
        /// </summary>
        private void OnDownloadProgressChanged(object? sender, DownloadProgressEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                progressBar.Value = e.Percent;
                txtProgress.Text = $"{e.Percent:F0}%";
                txtSpeed.Text = e.Speed;
                txtEta.Text = e.Eta;
            });
        }

        /// <summary>
        /// URL 다운로드 버튼 클릭
        /// </summary>
        private void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            string[] urls = txtUrls.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var url in urls)
            {
                var options = DownloadOptions.FromAppSettings(_currentSettings, url.Trim(), isChannelMode: false);
                _ = _downloadService.StartDownloadAsync(options);
            }
        }

        /// <summary>
        /// 채널 다운로드 버튼 클릭
        /// </summary>
        private void btnChannelDownload_Click(object sender, RoutedEventArgs e)
        {
            string[] urls = txtChannelUrl.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var url in urls)
            {
                var options = DownloadOptions.FromAppSettings(_currentSettings, url.Trim(), isChannelMode: true);
                _ = _downloadService.StartDownloadAsync(options);
            }
        }

        /// <summary>
        /// 붙여넣기 버튼 (URL)
        /// </summary>
        private void btnPaste_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                txtUrls.AppendText(Clipboard.GetText() + Environment.NewLine);
            }
        }

        /// <summary>
        /// 지우기 버튼 (URL)
        /// </summary>
        private void btnUrlsClear_Click(object sender, RoutedEventArgs e)
        {
            txtUrls.Clear();
        }

        /// <summary>
        /// 붙여넣기 버튼 (채널)
        /// </summary>
        private void btnChannelPaste_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                txtChannelUrl.AppendText(Clipboard.GetText() + Environment.NewLine);
            }
        }

        /// <summary>
        /// 지우기 버튼 (채널)
        /// </summary>
        private void btnChannelClear_Click(object sender, RoutedEventArgs e)
        {
            txtChannelUrl.Clear();
        }

        /// <summary>
        /// 저장 경로 찾기 버튼
        /// </summary>
        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtSavePath.Text = dialog.SelectedPath;
                }
            }
        }

        /// <summary>
        /// 폴더 열기 버튼
        /// </summary>
        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(txtSavePath.Text))
            {
                Process.Start("explorer.exe", txtSavePath.Text);
            }
        }

        /// <summary>
        /// 하이퍼링크 클릭 이벤트
        /// </summary>
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }

        /// <summary>
        /// 프로그램 재시작 버튼
        /// </summary>
        private void pgRestart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    MessageBox.Show("실행 파일 경로를 찾을 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                });

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show("프로그램 재시작 실패: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 창 닫기 이벤트
        /// </summary>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // 설정은 실시간으로 저장되므로 추가 작업 불필요
        }

        // ===== 메뉴 이벤트 핸들러 =====

        /// <summary>
        /// 메뉴: 종료
        /// </summary>
        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        /// <summary>
        /// 메뉴: GitHub 릴리스
        /// </summary>
        private void MenuGitHub_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/gloriouslegacy/ytDownloader/releases",
                UseShellExecute = true
            });
        }

        /// <summary>
        /// 메뉴: yt-dlp 릴리스
        /// </summary>
        private void MenuYtDlp_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/yt-dlp/yt-dlp/releases",
                UseShellExecute = true
            });
        }

        /// <summary>
        /// 메뉴: 정보
        /// </summary>
        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            string version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "0.0.0";

            // '+' 뒤 빌드 메타데이터 제거
            int plusIdx = version.IndexOf('+');
            if (plusIdx >= 0)
                version = version.Substring(0, plusIdx);

            MessageBox.Show(
                $"ytDownloader v{version}\n\n" +
                $"YouTube 다운로더 (yt-dlp 기반)\n\n" +
                $"© gloriouslegacy\n" +
                $"https://github.com/gloriouslegacy/ytDownloader",
                "정보",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
    }
}

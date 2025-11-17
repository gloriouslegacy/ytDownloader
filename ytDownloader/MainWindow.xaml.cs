using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using ytDownloader.Models;
using ytDownloader.Services;

// 2025-09-21 .NET 8.0, C# 12.0
// ✅ 자동 업데이트
// ✅ 라이트 / 다크 모드 전환
// ✅ 키보드 단축키 (Ctrl+T, Ctrl+L, Ctrl+S, F5)
// ✅ 드래그 앤 드롭 (URL 입력란)
// ✅ 로그 저장 기능
// ✅ 언어 전환 (한국어/English)
// ❌ 웹뷰 내장
// ✅ 채널 예약 다운로드
// ❌ 다운로드 정지/일시정지/재개
// ✅ 다운로드 후 알림

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

        // 스케줄러 모드 플래그
        private bool _isScheduledMode = false;

        public MainWindow(string[] args = null)
        {
            InitializeComponent();

            // 명령줄 인수 처리 (스케줄러에서 실행 시)
            if (args != null && args.Length > 0 && args[0] == "--scheduled")
            {
                _isScheduledMode = true;
            }

            // 작업 디렉토리를 실행 파일 디렉토리로 설정 (스케줄러 실행 시 필요)
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrWhiteSpace(exePath))
                {
                    string? exeDir = Path.GetDirectoryName(exePath);
                    if (!string.IsNullOrWhiteSpace(exeDir))
                    {
                        Directory.SetCurrentDirectory(exeDir);
                    }
                }
            }
            catch
            {
                // 작업 디렉토리 설정 실패 시 무시
            }

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
            _downloadService.DownloadCompleted += OnDownloadCompleted;

            // 설정 로드 및 UI 초기화
            _currentSettings = _settingsService.LoadSettings();
            LoadSettingsToUI();
            AttachSettingsEventHandlers();

            // 테마 적용
            ApplyTheme(_currentSettings.Theme);

            // 키보드 단축키 설정
            SetupKeyboardShortcuts();

            // 스케줄러 모드가 아닐 때만 도구 및 앱 업데이트 시작
            // (예약 실행 시 업데이트로 인한 지연 방지)
            if (!_isScheduledMode)
            {
                _ = UpdateToolsAndAppSequentiallyAsync();
            }
            else
            {
                // 자동 실행 모드 (모든 초기화가 완료된 후 실행)
                AutoExecuteScheduledDownloads();
            }
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
            ChkEnableNotification.IsChecked = _currentSettings.EnableNotification;

            // 예약 목록 로드
            RefreshScheduledChannelsList();
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
            ChkEnableNotification.Checked += (s, e) => SaveCurrentSettings();
            ChkEnableNotification.Unchecked += (s, e) => SaveCurrentSettings();
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
                _currentSettings.EnableNotification = ChkEnableNotification.IsChecked ?? true;

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
        /// 다운로드 완료 이벤트
        /// </summary>
        private void OnDownloadCompleted(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (_currentSettings.EnableNotification)
                {
                    string message = _currentSettings.Language == "ko"
                        ? "다운로드가 완료되었습니다."
                        : "Download completed.";
                    string title = _currentSettings.Language == "ko"
                        ? "다운로드 완료"
                        : "Download Complete";

                    MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
                }
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
        /// 다운로드 정지 버튼
        /// </summary>
        private void btnStopDownload_Click(object sender, RoutedEventArgs e)
        {
            _downloadService.CancelDownload();
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
        /// 메뉴: BtbN 릴리스
        /// </summary>
        private void MenuBtbN_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/BtbN/FFmpeg-Builds/releases",
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

        /// <summary>
        /// 메뉴: 테마 전환
        /// </summary>
        private void MenuTheme_Click(object sender, RoutedEventArgs e)
        {
            // 현재 테마 토글
            string newTheme = _currentSettings.Theme == "Dark" ? "Light" : "Dark";
            _currentSettings.Theme = newTheme;
            _settingsService.SaveSettings(_currentSettings);

            // 테마 적용
            ApplyTheme(newTheme);

            AppendOutput($"✅ 테마 변경: {newTheme}");
        }

        /// <summary>
        /// 메뉴: 언어 전환
        /// </summary>
        private void MenuLanguage_Click(object sender, RoutedEventArgs e)
        {
            // 현재 언어 토글
            string newLanguage = _currentSettings.Language == "ko" ? "en" : "ko";
            _currentSettings.Language = newLanguage;
            _settingsService.SaveSettings(_currentSettings);

            // 언어 적용
            ApplyLanguage(newLanguage);

            string languageName = newLanguage == "ko" ? "한국어" : "English";
            AppendOutput($"✅ 언어 변경: {languageName}");

            string message = newLanguage == "ko"
                ? $"언어가 '{languageName}'(으)로 변경되었습니다."
                : $"Language changed to '{languageName}'.";
            string title = newLanguage == "ko" ? "언어 변경" : "Language Changed";

            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 언어 적용
        /// </summary>
        private void ApplyLanguage(string language)
        {
            try
            {
                var dictionaries = Application.Current.Resources.MergedDictionaries;

                // 기존 언어 리소스 제거
                var existingLanguage = dictionaries.FirstOrDefault(d =>
                    d.Source != null && (d.Source.OriginalString.Contains("Korean.xaml") || d.Source.OriginalString.Contains("English.xaml")));
                if (existingLanguage != null)
                {
                    dictionaries.Remove(existingLanguage);
                }

                string languageFile = language == "en" ? "Resources/English.xaml" : "Resources/Korean.xaml";
                var languageDict = new ResourceDictionary
                {
                    Source = new Uri(languageFile, UriKind.Relative)
                };

                dictionaries.Add(languageDict);
            }
            catch (Exception ex)
            {
                AppendOutput($"❌ 언어 적용 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 메뉴: 로그 저장
        /// </summary>
        private async void MenuSaveLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string logFileName = $"ytDownloader_log_{timestamp}.txt";
                string logPath = Path.Combine(_currentSettings.SavePath, logFileName);

                await File.WriteAllTextAsync(logPath, txtOutput.Text);

                MessageBox.Show(
                    $"로그가 저장되었습니다:\n{logPath}",
                    "로그 저장",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                AppendOutput($"✅ 로그 저장: {logPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"로그 저장 실패:\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// 테마 적용
        /// </summary>
        private void ApplyTheme(string theme)
        {
            try
            {
                var dictionaries = Application.Current.Resources.MergedDictionaries;

                // 기존 테마만 제거 (언어 리소스는 유지)
                var existingTheme = dictionaries.FirstOrDefault(d =>
                    d.Source != null && (d.Source.OriginalString.Contains("LightTheme.xaml") || d.Source.OriginalString.Contains("DarkTheme.xaml")));
                if (existingTheme != null)
                {
                    dictionaries.Remove(existingTheme);
                }

                string themeFile = theme == "Light" ? "Themes/LightTheme.xaml" : "Themes/DarkTheme.xaml";
                var themeDict = new ResourceDictionary
                {
                    Source = new Uri(themeFile, UriKind.Relative)
                };

                // 테마는 맨 앞에 삽입하여 우선순위 보장
                dictionaries.Insert(0, themeDict);

                // Window 배경색 적용
                if (Application.Current.Resources["WindowBackgroundBrush"] is System.Windows.Media.SolidColorBrush windowBrush)
                {
                    this.Background = windowBrush;
                }

                // Foreground 색상 적용
                if (Application.Current.Resources["PrimaryTextBrush"] is System.Windows.Media.SolidColorBrush textBrush)
                {
                    this.Foreground = textBrush;
                }
            }
            catch (Exception ex)
            {
                AppendOutput($"❌ 테마 적용 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 키보드 단축키 설정
        /// </summary>
        private void SetupKeyboardShortcuts()
        {
            // Ctrl+T: 테마 전환
            var themeGesture = new KeyGesture(Key.T, ModifierKeys.Control);
            var themeBinding = new KeyBinding(new RelayCommand(() => MenuTheme_Click(this, new RoutedEventArgs())), themeGesture);
            this.InputBindings.Add(themeBinding);

            // Ctrl+L: 언어 전환
            var languageGesture = new KeyGesture(Key.L, ModifierKeys.Control);
            var languageBinding = new KeyBinding(new RelayCommand(() => MenuLanguage_Click(this, new RoutedEventArgs())), languageGesture);
            this.InputBindings.Add(languageBinding);

            // Ctrl+S: 로그 저장
            var saveLogGesture = new KeyGesture(Key.S, ModifierKeys.Control);
            var saveLogBinding = new KeyBinding(new RelayCommand(() => MenuSaveLog_Click(this, new RoutedEventArgs())), saveLogGesture);
            this.InputBindings.Add(saveLogBinding);

            // F5: URL 다운로드
            var downloadGesture = new KeyGesture(Key.F5);
            var downloadBinding = new KeyBinding(new RelayCommand(() => btnDownload_Click(this, new RoutedEventArgs())), downloadGesture);
            this.InputBindings.Add(downloadBinding);
        }

        // ===== 드래그 앤 드롭 이벤트 핸들러 =====

        /// <summary>
        /// 드래그 오버 이벤트 (텍스트 데이터만 허용)
        /// </summary>
        private void TextBox_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Handled = true;
            if (e.Data.GetDataPresent(System.Windows.DataFormats.Text) || e.Data.GetDataPresent(System.Windows.DataFormats.UnicodeText))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }

        /// <summary>
        /// txtUrls 드롭 이벤트
        /// </summary>
        private void txtUrls_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.Text) || e.Data.GetDataPresent(System.Windows.DataFormats.UnicodeText))
            {
                string text = (string)e.Data.GetData(System.Windows.DataFormats.Text) ??
                              (string)e.Data.GetData(System.Windows.DataFormats.UnicodeText);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    txtUrls.AppendText(text.Trim() + Environment.NewLine);
                    AppendOutput("✅ URL 드래그 앤 드롭 완료");
                }
            }
        }

        /// <summary>
        /// txtChannelUrl 드롭 이벤트
        /// </summary>
        private void txtChannelUrl_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.Text) || e.Data.GetDataPresent(System.Windows.DataFormats.UnicodeText))
            {
                string text = (string)e.Data.GetData(System.Windows.DataFormats.Text) ??
                              (string)e.Data.GetData(System.Windows.DataFormats.UnicodeText);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    txtChannelUrl.AppendText(text.Trim() + Environment.NewLine);
                    AppendOutput("✅ 채널/재생목록 URL 드래그 앤 드롭 완료");
                }
            }
        }

        // ===== 예약 다운로드 =====

        /// <summary>
        /// 예약 목록 새로고침
        /// </summary>
        private void RefreshScheduledChannelsList()
        {
            lstScheduledChannels.Items.Clear();
            foreach (var channel in _currentSettings.ScheduledChannels)
            {
                // 객체를 직접 추가 (ToString()은 자동으로 표시됨)
                lstScheduledChannels.Items.Add(channel);
            }
        }

        /// <summary>
        /// 수동 예약 URL 붙여넣기 버튼 클릭
        /// </summary>
        private void btnScheduleUrlPaste_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                txtScheduleChannelUrl.Text = Clipboard.GetText();
            }
        }

        /// <summary>
        /// 수동 예약 URL 지우기 버튼 클릭
        /// </summary>
        private void btnScheduleUrlClear_Click(object sender, RoutedEventArgs e)
        {
            txtScheduleChannelUrl.Clear();
        }

        /// <summary>
        /// 탭 선택 변경 이벤트
        /// </summary>
        private async void mainTabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // 예약 탭이 선택되었을 때만 새로고침
            if (mainTabControl.SelectedItem == scheduleTabItem)
            {
                RefreshScheduledChannelsList();
                await UpdateSchedulerStatusAsync();
            }
        }

        /// <summary>
        /// 예약 추가 버튼 클릭
        /// </summary>
        private void btnAddSchedule_Click(object sender, RoutedEventArgs e)
        {
            string url = txtScheduleChannelUrl.Text.Trim();
            string name = txtScheduleChannelName.Text.Trim();

            if (string.IsNullOrWhiteSpace(url))
            {
                string message = _currentSettings.Language == "ko"
                    ? "채널 URL을 입력하세요."
                    : "Please enter a channel URL.";
                string title = _currentSettings.Language == "ko"
                    ? "입력 오류"
                    : "Input Error";
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var scheduledChannel = new ScheduledChannel
            {
                Url = url,
                Name = name,
                AddedDate = DateTime.Now
            };

            _currentSettings.ScheduledChannels.Add(scheduledChannel);
            _settingsService.SaveSettings(_currentSettings);

            RefreshScheduledChannelsList();

            // 입력 필드 초기화
            txtScheduleChannelUrl.Clear();
            txtScheduleChannelName.Clear();

            AppendOutput($"✅ 예약 추가: {scheduledChannel}");
        }

        /// <summary>
        /// 예약 삭제 버튼 클릭 (여러 항목 삭제 지원)
        /// </summary>
        private void btnRemoveSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (lstScheduledChannels.SelectedItems == null || lstScheduledChannels.SelectedItems.Count == 0)
            {
                string message = _currentSettings.Language == "ko"
                    ? "삭제할 예약 항목을 선택하세요."
                    : "Please select schedule items to remove.";
                string title = _currentSettings.Language == "ko"
                    ? "선택 오류"
                    : "Selection Error";
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 선택된 항목들을 리스트로 복사 (컬렉션 수정 중 반복 방지)
            var selectedChannels = lstScheduledChannels.SelectedItems.Cast<ScheduledChannel>().ToList();

            string confirmMessage = _currentSettings.Language == "ko"
                ? $"선택한 {selectedChannels.Count}개 항목을 삭제하시겠습니까?"
                : $"Do you want to remove {selectedChannels.Count} selected items?";
            string confirmTitle = _currentSettings.Language == "ko"
                ? "삭제 확인"
                : "Confirm Removal";

            if (MessageBox.Show(confirmMessage, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                foreach (var channel in selectedChannels)
                {
                    _currentSettings.ScheduledChannels.Remove(channel);
                }

                _settingsService.SaveSettings(_currentSettings);
                RefreshScheduledChannelsList();

                AppendOutput($"✅ 수동 예약 삭제: {selectedChannels.Count}개 항목 삭제됨");
            }
        }

        /// <summary>
        /// 예약 전체삭제 버튼 클릭
        /// </summary>
        private void btnRemoveAllSchedules_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSettings.ScheduledChannels.Count == 0)
            {
                string message = _currentSettings.Language == "ko"
                    ? "삭제할 예약 항목이 없습니다."
                    : "No schedule items to remove.";
                string title = _currentSettings.Language == "ko"
                    ? "알림"
                    : "Notice";
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string confirmMessage = _currentSettings.Language == "ko"
                ? $"모든 예약 항목({_currentSettings.ScheduledChannels.Count}개)을 삭제하시겠습니까?"
                : $"Do you want to remove all schedule items ({_currentSettings.ScheduledChannels.Count})?";
            string confirmTitle = _currentSettings.Language == "ko"
                ? "예약 전체삭제 확인"
                : "Confirm Remove All Schedules";

            if (MessageBox.Show(confirmMessage, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                int count = _currentSettings.ScheduledChannels.Count;
                _currentSettings.ScheduledChannels.Clear();
                _settingsService.SaveSettings(_currentSettings);

                RefreshScheduledChannelsList();

                AppendOutput($"✅ 예약 전체삭제: {count}개 항목 삭제됨");
            }
        }

        /// <summary>
        /// 선택 예약 실행 버튼 클릭
        /// </summary>
        private void btnRunSelectedSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (lstScheduledChannels.SelectedItem == null || lstScheduledChannels.SelectedItem is not ScheduledChannel)
            {
                string message = _currentSettings.Language == "ko"
                    ? "실행할 예약 항목을 선택하세요."
                    : "Please select a schedule item to run.";
                string title = _currentSettings.Language == "ko"
                    ? "선택 오류"
                    : "Selection Error";
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedChannel = (ScheduledChannel)lstScheduledChannels.SelectedItem;
            AppendOutput($"🚀 선택한 예약 채널 다운로드 시작: {selectedChannel.Name ?? selectedChannel.Url}");

            var options = DownloadOptions.FromAppSettings(_currentSettings, selectedChannel.Url, isChannelMode: true);
            _ = _downloadService.StartDownloadAsync(options);
        }

        /// <summary>
        /// 모든 예약 실행 버튼 클릭
        /// </summary>
        private void btnRunScheduledDownloads_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSettings.ScheduledChannels.Count == 0)
            {
                string message = _currentSettings.Language == "ko"
                    ? "예약된 채널이 없습니다."
                    : "No scheduled channels.";
                string title = _currentSettings.Language == "ko"
                    ? "알림"
                    : "Notice";
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AppendOutput($"🚀 예약된 {_currentSettings.ScheduledChannels.Count}개 채널 다운로드 시작...");

            foreach (var channel in _currentSettings.ScheduledChannels)
            {
                var options = DownloadOptions.FromAppSettings(_currentSettings, channel.Url, isChannelMode: true);
                _ = _downloadService.StartDownloadAsync(options);
            }
        }

        /// <summary>
        /// 자동 예약 설정 버튼 클릭
        /// </summary>
        private void btnAutoScheduleSettings_Click(object sender, RoutedEventArgs e)
        {
            var scheduleWindow = new ScheduleSettingsWindow();
            scheduleWindow.Owner = this;
            scheduleWindow.ShowDialog();

            // 다이얼로그가 닫히면 자동으로 스케줄러 상태 업데이트
            UpdateSchedulerStatus();
        }

        /// <summary>
        /// 자동 실행 모드 (스케줄러에서 실행 시)
        /// </summary>
        private async void AutoExecuteScheduledDownloads()
        {
            await Task.Delay(2000); // 초기화 대기

            AppendOutput($"🤖 자동 실행 모드 시작 - 작업 디렉토리: {Directory.GetCurrentDirectory()}");
            AppendOutput($"📋 설정 파일 경로: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ytDownloader")}");

            if (_currentSettings.ScheduledChannels.Count == 0)
            {
                AppendOutput("⚠️ 예약된 채널이 없습니다. 프로그램을 종료합니다.");
                await SaveScheduledLog();
                await Task.Delay(3000);
                Application.Current.Shutdown();
                return;
            }

            AppendOutput($"🤖 자동 실행 모드: {_currentSettings.ScheduledChannels.Count}개 채널 다운로드 시작...");

            try
            {
                foreach (var channel in _currentSettings.ScheduledChannels)
                {
                    AppendOutput($"📥 채널 다운로드 시작: {channel.Name ?? channel.Url}");
                    var options = DownloadOptions.FromAppSettings(_currentSettings, channel.Url, isChannelMode: true);
                    await _downloadService.StartDownloadAsync(options);
                    AppendOutput($"✅ 채널 다운로드 완료: {channel.Name ?? channel.Url}");
                }

                AppendOutput("✅ 모든 예약 다운로드 완료. 5초 후 프로그램을 종료합니다.");
            }
            catch (Exception ex)
            {
                AppendOutput($"❌ 예약 다운로드 중 오류 발생: {ex.Message}");
                AppendOutput($"❌ 스택 추적: {ex.StackTrace}");
            }

            await SaveScheduledLog();
            await Task.Delay(5000);
            Application.Current.Shutdown();
        }

        /// <summary>
        /// 스케줄러 모드 로그를 파일로 저장
        /// </summary>
        private async Task SaveScheduledLog()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string logFileName = $"ytDownloader_scheduled_log_{timestamp}.txt";
                string logPath = Path.Combine(_currentSettings.SavePath, logFileName);

                await Dispatcher.InvokeAsync(async () =>
                {
                    await File.WriteAllTextAsync(logPath, txtOutput.Text);
                    AppendOutput($"📝 로그 저장: {logPath}");
                });
            }
            catch (Exception ex)
            {
                AppendOutput($"❌ 로그 저장 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 스케줄러 상태 업데이트
        /// </summary>
        private void UpdateSchedulerStatus()
        {
            _ = UpdateSchedulerStatusAsync();
        }

        /// <summary>
        /// 스케줄러 상태 비동기 업데이트
        /// </summary>
        private async Task UpdateSchedulerStatusAsync()
        {
            var schedulerService = new TaskSchedulerService();

            // UI 스레드를 차단하지 않도록 작업을 백그라운드에서 실행
            var tasks = await Task.Run(() => schedulerService.GetAllScheduledTasks());

            await Dispatcher.InvokeAsync(() =>
            {
                lstAutoScheduledTasks.Items.Clear();

                if (tasks.Count == 0)
                {
                    lstAutoScheduledTasks.Items.Add("등록된 스케줄이 없습니다.");
                }
                else
                {
                    foreach (var task in tasks)
                    {
                        // 객체를 직접 추가 (DisplayText는 자동으로 표시됨)
                        lstAutoScheduledTasks.Items.Add(task);
                    }
                }
            });
        }

        /// <summary>
        /// 상태 새로고침 버튼 클릭
        /// </summary>
        private void btnRefreshScheduleStatus_Click(object sender, RoutedEventArgs e)
        {
            UpdateSchedulerStatus();
        }

        /// <summary>
        /// 자동 예약 선택 삭제 버튼 클릭 (여러 항목 삭제 지원)
        /// </summary>
        private void btnDeleteSelectedAutoSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (lstAutoScheduledTasks.SelectedItems == null || lstAutoScheduledTasks.SelectedItems.Count == 0)
            {
                string message = _currentSettings.Language == "ko"
                    ? "삭제할 스케줄을 선택해주세요."
                    : "Please select schedules to delete.";
                string title = _currentSettings.Language == "ko"
                    ? "알림"
                    : "Notice";
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // "등록된 스케줄이 없습니다." 문자열 체크
            if (lstAutoScheduledTasks.SelectedItems.Count > 0 && lstAutoScheduledTasks.SelectedItems[0] is string)
            {
                string message = _currentSettings.Language == "ko"
                    ? "삭제할 스케줄을 선택해주세요."
                    : "Please select schedules to delete.";
                string title = _currentSettings.Language == "ko"
                    ? "알림"
                    : "Notice";
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 선택된 항목들을 리스트로 복사
            var selectedTasks = lstAutoScheduledTasks.SelectedItems.OfType<ScheduleTaskInfo>().ToList();

            if (selectedTasks.Count == 0)
            {
                return;
            }

            string confirmMessage = _currentSettings.Language == "ko"
                ? $"선택한 {selectedTasks.Count}개 자동 예약을 삭제하시겠습니까?"
                : $"Do you want to delete {selectedTasks.Count} selected auto schedules?";
            string confirmTitle = _currentSettings.Language == "ko"
                ? "확인"
                : "Confirm";

            var result = MessageBox.Show(confirmMessage, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var schedulerService = new TaskSchedulerService();
                int successCount = 0;

                foreach (var task in selectedTasks)
                {
                    if (schedulerService.DeleteScheduledTask(task.TaskName))
                    {
                        successCount++;

                        // 관련 스케줄러 설정 파일도 함께 삭제
                        _settingsService.DeleteSchedulerSettings(task.TaskName);

                        AppendOutput($"✅ 자동 예약 삭제: {task.TaskName}");
                    }
                }

                if (successCount > 0)
                {
                    _settingsService.SaveSettings(_currentSettings);

                    string successMessage = _currentSettings.Language == "ko"
                        ? $"{successCount}개의 자동 예약이 삭제되었습니다."
                        : $"{successCount} auto schedule(s) have been deleted.";
                    string successTitle = _currentSettings.Language == "ko"
                        ? "삭제 완료"
                        : "Delete Complete";
                    MessageBox.Show(successMessage, successTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateSchedulerStatus();
                }
                else
                {
                    string errorMessage = _currentSettings.Language == "ko"
                        ? "자동 예약 삭제에 실패했습니다.\n관리자 권한이 필요할 수 있습니다."
                        : "Failed to delete auto schedules.\nAdministrator privileges may be required.";
                    string errorTitle = _currentSettings.Language == "ko"
                        ? "삭제 실패"
                        : "Delete Failed";
                    MessageBox.Show(errorMessage, errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 수동 예약 전체 선택/해제 체크박스 변경
        /// </summary>
        private void chkSelectAllManual_Changed(object sender, RoutedEventArgs e)
        {
            if (chkSelectAllManual.IsChecked == true)
            {
                lstScheduledChannels.SelectAll();
            }
            else
            {
                lstScheduledChannels.UnselectAll();
            }
        }

        /// <summary>
        /// 자동 예약 전체 선택/해제 체크박스 변경
        /// </summary>
        private void chkSelectAllAuto_Changed(object sender, RoutedEventArgs e)
        {
            if (chkSelectAllAuto.IsChecked == true)
            {
                lstAutoScheduledTasks.SelectAll();
            }
            else
            {
                lstAutoScheduledTasks.UnselectAll();
            }
        }

        /// <summary>
        /// 자동 예약 전체 삭제 버튼 클릭
        /// </summary>
        private void btnDeleteAllAutoSchedules_Click(object sender, RoutedEventArgs e)
        {
            var schedulerService = new TaskSchedulerService();
            var tasks = schedulerService.GetAllScheduledTasks();

            if (tasks.Count == 0)
            {
                string message = _currentSettings.Language == "ko"
                    ? "삭제할 스케줄이 없습니다."
                    : "No schedules to delete.";
                string title = _currentSettings.Language == "ko"
                    ? "알림"
                    : "Notice";
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string confirmMessage = _currentSettings.Language == "ko"
                ? $"모든 자동 예약({tasks.Count}개)을 삭제하시겠습니까?"
                : $"Do you want to delete all auto schedules ({tasks.Count})?";
            string confirmTitle = _currentSettings.Language == "ko"
                ? "전체 삭제 확인"
                : "Confirm Delete All";

            var result = MessageBox.Show(confirmMessage, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // 각 태스크의 설정 파일도 함께 삭제
                foreach (var task in tasks)
                {
                    _settingsService.DeleteSchedulerSettings(task.TaskName);
                }

                int deletedCount = schedulerService.DeleteAllScheduledTasks();

                if (deletedCount > 0)
                {

                    string successMessage = _currentSettings.Language == "ko"
                        ? $"{deletedCount}개의 자동 예약이 삭제되었습니다."
                        : $"{deletedCount} auto schedule(s) have been deleted.";
                    string successTitle = _currentSettings.Language == "ko"
                        ? "삭제 완료"
                        : "Delete Complete";
                    MessageBox.Show(successMessage, successTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateSchedulerStatus();
                    AppendOutput($"✅ 자동 예약 전체 삭제: {deletedCount}개 항목 삭제됨");
                }
                else
                {
                    string errorMessage = _currentSettings.Language == "ko"
                        ? "자동 예약 삭제에 실패했습니다.\n관리자 권한이 필요할 수 있습니다."
                        : "Failed to delete auto schedules.\nAdministrator privileges may be required.";
                    string errorTitle = _currentSettings.Language == "ko"
                        ? "삭제 실패"
                        : "Delete Failed";
                    MessageBox.Show(errorMessage, errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    /// <summary>
    /// RelayCommand - 키보드 단축키용 간단한 커맨드 클래스
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute();

        public void Execute(object? parameter) => _execute();
    }
}
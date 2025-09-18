using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

// URL 붙여넣기시 다중 URL 적용 안됨. 수정 필요. 다음라인 입력시 가능

namespace YouTubeDownloaderGUI
{
    public partial class MainWindow : Window
    {
        private string ytDlpPath;
        private string ffmpegPath;

        // 기본 저장 경로: 사용자 다운로드 폴더
        private readonly string defaultDownloadPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        // 설정 저장 파일
        private readonly string configPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");

        private bool stopRequested = false;

        public MainWindow()
        {
            InitializeComponent();

            // tools 폴더 경로
            string toolPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
            ytDlpPath = Path.Combine(toolPath, "yt-dlp.exe");
            ffmpegPath = Path.Combine(toolPath, "ffmpeg.exe");

            this.Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 저장된 경로 불러오기
            if (File.Exists(configPath))
            {
                string savedPath = File.ReadAllText(configPath).Trim();
                if (!string.IsNullOrEmpty(savedPath) && Directory.Exists(savedPath))
                {
                    txtSavePath.Text = savedPath;
                }
                else
                {
                    txtSavePath.Text = defaultDownloadPath;
                }
            }
            else
            {
                txtSavePath.Text = defaultDownloadPath;
            }

            // yt-dlp 자동 업데이트
            if (File.Exists(ytDlpPath))
            {
                AppendLog("🔄 yt-dlp 최신 버전 확인 중...");
                await RunProcessAsync(ytDlpPath, "-U");
            }
            else
            {
                AppendLog("⚠️ yt-dlp.exe가 tools 폴더에 없습니다. 자동 업데이트를 건너뜁니다.");
            }
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtSavePath.Text = dialog.SelectedPath;
                File.WriteAllText(configPath, txtSavePath.Text.Trim()); // ✅ 변경 시 저장
            }
        }

        private void btnPaste_Click(object sender, RoutedEventArgs e)
        {
            txtUrls.Text = Clipboard.GetText();
        }

        private void btnUrlsClear_Click(object sender, RoutedEventArgs e)
        {
            txtUrls.Clear();
        }

        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(txtSavePath.Text))
            {
                Process.Start("explorer.exe", txtSavePath.Text);
            }
        }

        // 로그 출력
        private void AppendLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtOutput.AppendText(message + Environment.NewLine);
                txtOutput.ScrollToEnd();
            });
        }

        // yt-dlp 실행 공통 함수
        private async Task RunProcessAsync(string exePath, string arguments)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    },
                    EnableRaisingEvents = true
                };

                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        ParseYtDlpOutput(e.Data);
                        AppendLog(e.Data);
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        AppendLog("ERROR: " + e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                AppendLog("FATAL: " + ex.Message);
            }
        }

        // yt-dlp 출력 파싱 (진행률/속도/ETA)
        private void ParseYtDlpOutput(string line)
        {
            if (line.Contains("[download]"))
            {
                try
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        if (part.EndsWith("%"))
                        {
                            if (double.TryParse(part.Replace("%", ""), out double progress))
                            {
                                Dispatcher.Invoke(() => progressBar.Value = progress);
                            }
                        }
                        else if (part.EndsWith("iB/s") || part.EndsWith("KiB/s") || part.EndsWith("MiB/s"))
                        {
                            Dispatcher.Invoke(() => txtSpeed.Text = "속도: " + part);
                        }
                        else if (part.StartsWith("ETA"))
                        {
                            Dispatcher.Invoke(() => txtEta.Text = part);
                        }
                    }
                }
                catch { }
            }
        }

        // 일반 다운로드
        private async void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            stopRequested = false;

            if (!File.Exists(ytDlpPath))
            {
                MessageBox.Show("yt-dlp.exe가 tools 폴더에 없습니다.");
                return;
            }
            if (!File.Exists(ffmpegPath))
            {
                MessageBox.Show("ffmpeg.exe가 tools 폴더에 없습니다.");
                return;
            }

            string[] urls = txtUrls.Text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (urls.Length == 0)
            {
                MessageBox.Show("URL을 입력하세요.");
                return;
            }

            string saveBase = txtSavePath.Text.Trim();
            if (string.IsNullOrEmpty(saveBase)) saveBase = defaultDownloadPath;
            if (!Directory.Exists(saveBase)) Directory.CreateDirectory(saveBase);

            foreach (string url in urls)
            {
                string args;

                if (comboFormat.SelectedIndex == 0) // Video (MP4)
                {
                    args = $"-f \"bv*+ba/best\" --merge-output-format mp4 " +
                           $"--ffmpeg-location \"{Path.GetDirectoryName(ffmpegPath)}\" " +
                           $"--windows-filenames -o \"{Path.Combine(saveBase, "%(title)s.%(ext)s")}\" " +
                           $"--newline \"{url.Trim()}\"";
                }
                else // Music (MP3)
                {
                    args = $"--extract-audio --audio-format mp3 " +
                           $"--ffmpeg-location \"{Path.GetDirectoryName(ffmpegPath)}\" " +
                           $"--windows-filenames -o \"{Path.Combine(saveBase, "%(title)s.%(ext)s")}\" " +
                           $"--newline \"{url.Trim()}\"";
                }

                if (ChkSingleVideo.IsChecked == true)
                    args += " --no-playlist";

                if (SubtitleCheckBox.IsChecked == true)
                {
                    string lang = (SubtitleLangComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ko";
                    string fmt = (SubtitleFormatComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "srt";
                    args += $" --write-subs --write-auto-subs --sub-langs {lang} --sub-format {fmt} --embed-subs";
                }

                if (ChkWriteThumbnail.IsChecked == true)
                    args += " --write-thumbnail";

                if (ChkStructuredFolders.IsChecked == true)
                    args = args.Replace("%(title)s.%(ext)s", "%(uploader)s/%(playlist)s/%(title)s.%(ext)s");

                AppendLog($"> 다운로드 시작: {url}");
                await RunProcessAsync(ytDlpPath, args);
            }

            if (!stopRequested)
            {
                AppendLog("✅ 모든 다운로드가 완료되었습니다.");
            }

            progressBar.Value = 0;
            txtSpeed.Text = "속도: -";
            txtEta.Text = "ETA: -";
        }

        // 자막 언어 불러오기
        private void BtnLoadSubtitleLang_Click(object sender, RoutedEventArgs e)
        {
            string[] langs = { "ko", "en", "ja", "zh-Hans", "fr", "de", "es" };

            SubtitleLangComboBox.Items.Clear();
            foreach (var lang in langs)
            {
                SubtitleLangComboBox.Items.Add(new ComboBoxItem { Content = lang });
            }

            if (SubtitleLangComboBox.Items.Count > 0)
                SubtitleLangComboBox.SelectedIndex = 0;

            AppendLog("✅ 자막 언어 목록을 불러왔습니다.");
        }

        // 채널 URL TextBox 플레이스홀더
        private void TxtChannelUrl_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtChannelUrl.Text == "채널 URL 입력")
                txtChannelUrl.Text = "";
        }

        private void TxtChannelUrl_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtChannelUrl.Text))
                txtChannelUrl.Text = "채널 URL 입력";
        }

        // 채널 구독 다운로드
        private async void btnChannelDownload_Click(object sender, RoutedEventArgs e)
        {
            stopRequested = false;

            string channelUrl = txtChannelUrl.Text.Trim();
            if (string.IsNullOrEmpty(channelUrl) || channelUrl == "채널 URL 입력")
            {
                MessageBox.Show("채널 URL을 입력하세요.");
                return;
            }
            if (!File.Exists(ytDlpPath))
            {
                MessageBox.Show("yt-dlp.exe가 tools 폴더에 없습니다.");
                return;
            }
            if (!File.Exists(ffmpegPath))
            {
                MessageBox.Show("ffmpeg.exe가 tools 폴더에 없습니다.");
                return;
            }

            string saveBase = txtSavePath.Text.Trim();
            if (string.IsNullOrEmpty(saveBase)) saveBase = defaultDownloadPath;
            if (!Directory.Exists(saveBase)) Directory.CreateDirectory(saveBase);

            int maxDownloads = 5;
            if (!int.TryParse(txtMaxDownloads.Text.Trim(), out maxDownloads) || maxDownloads <= 0)
                maxDownloads = 5;

            AppendLog($"> 채널 구독 다운로드 시작: {channelUrl} (최대 {maxDownloads}개)");

            string args;

            if (comboFormat.SelectedIndex == 0) // Video (MP4)
            {
                args = $"-f \"bv*+ba/best\" --merge-output-format mp4 " +
                       $"--ffmpeg-location \"{Path.GetDirectoryName(ffmpegPath)}\" " +
                       $"--windows-filenames " +
                       $"--download-archive \"{Path.Combine(saveBase, "archive.txt")}\" " +
                       $"--max-downloads {maxDownloads} " +
                       $"-o \"{Path.Combine(saveBase, "%(uploader)s/%(title)s.%(ext)s")}\" " +
                       $"--newline \"{channelUrl}\"";
            }
            else // Music (MP3)
            {
                args = $"--extract-audio --audio-format mp3 " +
                       $"--ffmpeg-location \"{Path.GetDirectoryName(ffmpegPath)}\" " +
                       $"--windows-filenames " +
                       $"--download-archive \"{Path.Combine(saveBase, "archive.txt")}\" " +
                       $"--max-downloads {maxDownloads} " +
                       $"-o \"{Path.Combine(saveBase, "%(uploader)s/%(title)s.%(ext)s")}\" " +
                       $"--newline \"{channelUrl}\"";
            }

            // 자막 옵션
            if (SubtitleCheckBox.IsChecked == true)
            {
                string lang = (SubtitleLangComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ko";
                string fmt = (SubtitleFormatComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "srt";
                args += $" --write-subs --write-auto-subs --sub-langs {lang} --sub-format {fmt} --embed-subs";
            }

            // 썸네일 옵션
            if (ChkWriteThumbnail.IsChecked == true)
                args += " --write-thumbnail";

            // 구조화 폴더
            if (ChkStructuredFolders.IsChecked == true)
                args = args.Replace("%(title)s.%(ext)s", "%(playlist)s/%(title)s.%(ext)s");

            await RunProcessAsync(ytDlpPath, args);

            if (!stopRequested)
            {
                AppendLog("✅ 채널 구독 다운로드 완료.");
            }

            progressBar.Value = 0;
            txtSpeed.Text = "속도: -";
            txtEta.Text = "ETA: -";
        }
    }
}

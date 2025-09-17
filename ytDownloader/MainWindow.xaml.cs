using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Ookii.Dialogs.Wpf; // WinForms 제거, Ookii 사용

// 다운로드 정지 버튼 : 플레이리스트 다운로드등 중간 정지 안됨

namespace YouTubeDownloaderGUI
{
    public partial class MainWindow : Window
    {
        private string toolPath;
        private string ytDlpPath;
        private string ffmpegPath;
        private string defaultDownloadPath;
        private string configPath;

        private Process? currentProcess;
        private bool stopRequested = false;

        public MainWindow()
        {
            InitializeComponent();

            toolPath = AppDomain.CurrentDomain.BaseDirectory;

            ytDlpPath = Path.Combine(toolPath, "tools", "yt-dlp.exe");
            ffmpegPath = Path.Combine(toolPath, "tools", "ffmpeg.exe");

            defaultDownloadPath = Path.Combine(toolPath, "download");
            configPath = Path.Combine(toolPath, "download_path.txt");

            if (!Directory.Exists(defaultDownloadPath))
                Directory.CreateDirectory(defaultDownloadPath);

            if (File.Exists(configPath))
            {
                try { txtSavePath.Text = File.ReadAllText(configPath).Trim(); }
                catch { txtSavePath.Text = defaultDownloadPath; }
            }
            else
            {
                txtSavePath.Text = defaultDownloadPath;
            }

            CheckForYtDlpUpdates();
        }

        private async void CheckForYtDlpUpdates()
        {
            if (!File.Exists(ytDlpPath))
            {
                AppendLog("yt-dlp.exe 파일이 없습니다. ./tools 폴더에 넣어주세요.");
                return;
            }

            AppendLog("[yt-dlp Checking for updates...]");
            try
            {
                var updateProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ytDlpPath,
                        Arguments = "-U",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };

                updateProcess.StartInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
                updateProcess.StartInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                updateProcess.StartInfo.EnvironmentVariables["LANG"] = "ko_KR.UTF-8";

                updateProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Dispatcher.Invoke(() => AppendLog(e.Data));
                };
                updateProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Dispatcher.Invoke(() => AppendLog("[ERROR] " + e.Data));
                };

                updateProcess.Start();
                updateProcess.BeginOutputReadLine();
                updateProcess.BeginErrorReadLine();
                await Task.Run(() => updateProcess.WaitForExit());
                AppendLog("✅ yt-dlp update check complete.");
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] Failed to run yt-dlp update: {ex.Message}");
            }
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "저장할 폴더를 선택하세요",
                UseDescriptionForTitle = true
            };
            if (dialog.ShowDialog() == true)
            {
                txtSavePath.Text = dialog.SelectedPath;
                try { File.WriteAllText(configPath, dialog.SelectedPath); } catch { }
            }
        }

        private void btnPaste_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    var t = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(t))
                    {
                        if (!string.IsNullOrWhiteSpace(txtUrls.Text))
                            txtUrls.AppendText(Environment.NewLine);
                        txtUrls.AppendText(t.Trim());
                        txtUrls.ScrollToEnd();
                    }
                }
            }
            catch { }
        }

        private async void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            stopRequested = false; // 시작 시 초기화

            string urls = txtUrls.Text.Trim();
            if (string.IsNullOrEmpty(urls))
            {
                MessageBox.Show("다운로드할 링크를 입력하세요.");
                return;
            }
            if (!File.Exists(ytDlpPath))
            {
                MessageBox.Show("yt-dlp.exe가 ./tools 폴더에 없습니다.");
                return;
            }
            if (!File.Exists(ffmpegPath))
            {
                MessageBox.Show("ffmpeg.exe가 ./tools 폴더에 없습니다.");
                return;
            }

            string saveBase = txtSavePath.Text.Trim();
            if (string.IsNullOrEmpty(saveBase)) saveBase = defaultDownloadPath;
            if (!Directory.Exists(saveBase)) Directory.CreateDirectory(saveBase);

            string[] urlList = urls.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var url in urlList)
            {
                if (stopRequested) // 중지 요청되면 즉시 종료
                {
                    AppendLog("⏹ 전체 다운로드가 중단되었습니다.");
                    break;
                }

                AppendLog($"> Downloading: {url}");
                string structure = ChkStructuredFolders.IsChecked == true
                    ? Path.Combine(saveBase, "%(uploader)s", "%(playlist_title|NA)s", "%(title)s.%(ext)s")
                    : Path.Combine(saveBase, "%(title)s.%(ext)s");

                string args = BuildYtDlpArgs(url, structure);
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

        private void btnStopDownload_Click(object sender, RoutedEventArgs e)
        {
            stopRequested = true;
            if (currentProcess != null && !currentProcess.HasExited)
            {
                try
                {
                    currentProcess.Kill();
                    AppendLog("⏹ 다운로드가 중지되었습니다.");
                }
                catch (Exception ex)
                {
                    AppendLog("[ERROR] 다운로드 중지 실패: " + ex.Message);
                }
            }
        }

        private string BuildYtDlpArgs(string url, string outputTemplate)
        {
            var sb = new StringBuilder();

            if (comboFormat.SelectedIndex == 0) // Video
                sb.Append("-f \"bv*+ba/best\" --merge-output-format mp4 ");
            else // Music
                sb.Append("-f bestaudio --extract-audio --audio-format mp3 --audio-quality 0 ");

            sb.Append($"--ffmpeg-location \"{Path.GetDirectoryName(ffmpegPath)}\" ");

            if (ChkSingleVideo.IsChecked == true) sb.Append("--no-playlist ");
            if (ChkWriteThumbnail.IsChecked == true) sb.Append("--write-thumbnail ");

            if (SubtitleCheckBox.IsChecked == true)
            {
                string lang = SubtitleLangComboBox.SelectedItem?.ToString() ?? "en";
                if (SubtitleLangComboBox.SelectedItem is ComboBoxItem cbi1 && cbi1.Content != null)
                    lang = cbi1.Content.ToString();
                string subfmt = (SubtitleFormatComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "srt";
                sb.Append($"--write-auto-subs --sub-lang {lang} --convert-subs {subfmt} ");
            }

            // Windows 한글 파일명 깨짐 방지
            sb.Append("--windows-filenames ");

            sb.Append($"-o \"{outputTemplate}\" ");
            sb.Append("--newline ");
            sb.Append($"\"{url}\"");

            return sb.ToString();
        }

        private async Task RunProcessAsync(string fileName, string arguments)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                },
                EnableRaisingEvents = true
            };

            process.StartInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
            process.StartInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            process.StartInfo.EnvironmentVariables["LANG"] = "ko_KR.UTF-8";

            currentProcess = process;

            process.OutputDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                Dispatcher.Invoke(() =>
                {
                    ParseAndUpdateProgress(e.Data);
                    AppendLog(e.Data);
                });
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                Dispatcher.Invoke(() => AppendLog("[ERROR] " + e.Data));
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await Task.Run(() => process.WaitForExit());

            currentProcess = null;
        }

        private void ParseAndUpdateProgress(string line)
        {
            var m = Regex.Match(line, @"\b(\d{1,3}(?:\.\d)?)%\b");
            if (m.Success && double.TryParse(m.Groups[1].Value, out double p))
            {
                progressBar.Value = Math.Min(100, p);
            }

            var speed = Regex.Match(line, @"\b(\d+(?:\.\d+)?(?:[KMGT]?i?B/s))\b");
            if (speed.Success)
            {
                txtSpeed.Text = $"속도: {speed.Groups[1].Value}";
            }

            var eta = Regex.Match(line, @"ETA\s+(\d{2}:\d{2}(?::\d{2})?)");
            if (eta.Success)
            {
                txtEta.Text = $"ETA: {eta.Groups[1].Value}";
            }

            if (line.Contains("100%") || line.Contains("has already been downloaded"))
            {
                txtEta.Text = "ETA: 완료";
            }
        }

        private void AppendLog(string message)
        {
            txtOutput.AppendText(message + Environment.NewLine);
            txtOutput.ScrollToEnd();
        }

        private void btnUrlsClear_Click(object sender, RoutedEventArgs e)
        {
            txtUrls.Clear();
            AppendLog("URL 목록이 지워졌습니다.");
        }

        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = Directory.Exists(txtSavePath.Text) ? txtSavePath.Text : defaultDownloadPath;
            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                AppendLog("저장 폴더를 열었습니다.");
            }
        }

        private async void BtnLoadSubtitleLang_Click(object sender, RoutedEventArgs e)
        {
            var list = txtUrls.Text.Trim().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            string firstUrl = list.Length > 0 ? list[0] : "";
            if (string.IsNullOrEmpty(firstUrl))
            {
                MessageBox.Show("URL을 입력하세요.");
                return;
            }

            SubtitleLangComboBox.Items.Clear();

            string args = $"--list-subs \"{firstUrl}\"";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ytDlpPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            process.StartInfo.EnvironmentVariables["PYTHONUTF8"] = "1";
            process.StartInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            process.StartInfo.EnvironmentVariables["LANG"] = "ko_KR.UTF-8";

            process.OutputDataReceived += (s, ev) =>
            {
                if (string.IsNullOrEmpty(ev.Data)) return;

                string line = ev.Data.Trim();
                var m = Regex.Match(line, @"^([a-zA-Z0-9\-]+)\s+");
                if (m.Success)
                {
                    string code = m.Groups[1].Value;
                    Dispatcher.Invoke(() =>
                    {
                        if (!SubtitleLangComboBox.Items.Contains(code))
                            SubtitleLangComboBox.Items.Add(code);
                        if (SubtitleLangComboBox.SelectedIndex < 0)
                            SubtitleLangComboBox.SelectedIndex = 0;
                    });
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await Task.Run(() => process.WaitForExit());
            AppendLog("자막 언어 목록 불러오기 완료.");
        }
    }
}

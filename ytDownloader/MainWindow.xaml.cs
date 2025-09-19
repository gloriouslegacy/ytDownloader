using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics;
using System.IO;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http;
using System.Net.Http;
using System.Reflection;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks;
using System.Windows;
using System.Windows;
using System.Windows.Navigation;
using ytDownloader.Properties;

namespace ytDownloader
{
    public partial class MainWindow : Window
    {
        private readonly string toolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
        private readonly string ytdlpPath;
        private readonly string ffmpegPath;

        public MainWindow()
        {
            ytdlpPath = Path.Combine(toolsPath, "yt-dlp.exe");
            ffmpegPath = Path.Combine(toolsPath, "ffmpeg.exe");

            InitializeComponent();
            LoadSettings();

            UpdateYtDlp(); // 실행 시 자동 업데이트
            _ = CheckForUpdate();   // GitHub Release 최신 버전 확인 (비동기 실행)
        }

        private void UpdateYtDlp()
        {
            if (!File.Exists(ytdlpPath))
            {
                AppendOutput("❌ yt-dlp.exe가 tools 폴더에 없습니다.");
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    AppendOutput("⏳ yt-dlp 업데이트 확인 중...");
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = ytdlpPath,
                        Arguments = "-U",
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using (Process proc = new Process())
                    {
                        proc.StartInfo = psi;
                        proc.OutputDataReceived += (s, ev) => { if (!string.IsNullOrEmpty(ev.Data)) AppendOutput(ev.Data); };
                        proc.ErrorDataReceived += (s, ev) => { if (!string.IsNullOrEmpty(ev.Data)) AppendOutput(ev.Data); };

                        proc.Start();
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();
                        proc.WaitForExit();
                    }

                    AppendOutput("✅ yt-dlp 업데이트 확인 완료");
                }
                catch (Exception ex)
                {
                    AppendOutput("❌ 업데이트 오류: " + ex.Message);
                }
            });
        }

        private async Task CheckForUpdate()
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ytDownloader/1.0");

                //  GitHub Release API 호출 (정식 + Pre-release 포함)
                var response = await httpClient.GetAsync("https://api.github.com/repos/gloriouslegacy/ytDownloader/releases");
                if (!response.IsSuccessStatusCode)
                {
                    AppendOutput($"❌ 업데이트 확인 실패: {response.StatusCode}");
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var releases = JsonConvert.DeserializeObject<List<dynamic>>(json);

                if (releases == null || releases.Count == 0)
                {
                    AppendOutput("ℹ️ 아직 등록된 릴리스가 없습니다.");
                    return;
                }

                //  최신 Release (첫 번째 항목)
                var latest = releases[0];
                string latestTag = latest.tag_name;
                bool isPre = latest.prerelease;

                //  현재 실행 중인 어셈블리 버전 (csproj의 <Version>)
                string currentVersion = Assembly
                    .GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? "0.0.0";

                //  v 접두사 제거 (ex: v0.3.0 → 0.3.0)
                string latestTagClean = latestTag.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                    ? latestTag.Substring(1)
                    : latestTag;

                if (latestTagClean != currentVersion)
                {
                    Dispatcher.Invoke(() =>
                    {
                        string preMsg = isPre ? "Pre-release" : "정식 릴리스";
                        if (MessageBox.Show(
                                $"새 {preMsg} {latestTag} 버전이 있습니다.\n업데이트 하시겠습니까?",
                                "업데이트 확인",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            // 최신 릴리스 ZIP 다운로드 링크 (Updater.exe는 ZIP에 포함되지 않음!)
                            string assetUrl = latest.assets[0].browser_download_url;
                            _ = RunUpdateAsync(assetUrl);
                        }
                    });
                }
                else
                {
                    AppendOutput("✅ 최신 버전을 사용 중입니다.");
                }
            }
            catch (Exception ex)
            {
                AppendOutput($"❌ 업데이트 확인 실패: {ex.Message}");
            }
        }


        // ETA 계산 → "hh:mm:ss" 또는 "mm:ss" 포맷
        string FormatEta(double seconds)
        {
            if (seconds < 0) return "--:--";
            TimeSpan ts = TimeSpan.FromSeconds(seconds);

            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"; // hh:mm:ss
            else
                return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}"; // mm:ss
        }

        // 용량 자동 변환 (B, KB, MB, GB)
        string FormatSize(double bytes)
        {
            if (bytes < 1024) return $"{bytes:F0} B";
            double kb = bytes / 1024d;
            if (kb < 1024) return $"{kb:F1} KB";
            double mb = kb / 1024d;
            if (mb < 1024) return $"{mb:F1} MB";
            double gb = mb / 1024d;
            return $"{gb:F2} GB";
        }

        // 속도 자동 변환 (B/s, KB/s, MB/s, GB/s)
        string FormatSpeed(double bytesPerSec)
        {
            if (bytesPerSec < 1024) return $"{bytesPerSec:F0} B/s";
            double kb = bytesPerSec / 1024d;
            if (kb < 1024) return $"{kb:F1} KB/s";
            double mb = kb / 1024d;
            if (mb < 1024) return $"{mb:F1} MB/s";
            double gb = mb / 1024d;
            return $"{gb:F2} GB/s";
        }

        private async Task RunUpdateAsync(dynamic latest)
        {
            string logFile = Path.Combine(Path.GetTempPath(), "ytDownloader_update.log");

            var updateWindow = new UpdateWindow();
            updateWindow.Show();

            await Task.Run(async () =>
            {
                try
                {
                    AppendOutput("[RunUpdateAsync] 업데이트 ZIP 선택 중...");
                    File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] RunUpdateAsync 시작{Environment.NewLine}");

                    using var httpClient = new HttpClient();

                    // 📌 ZIP 자산만 찾기
                    string? assetUrl = null;
                    string? assetName = null;
                    foreach (var asset in latest.assets)
                    {
                        string name = (string)asset.name;
                        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            assetUrl = (string)asset.browser_download_url;
                            assetName = name;
                            break;
                        }
                    }

                    if (assetUrl == null)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            updateWindow.Close();
                            MessageBox.Show("❌ 업데이트 ZIP 파일을 찾을 수 없습니다.",
                                "업데이트 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                        return;
                    }

                    // 📌 다운로드 대상 경로
                    string tempZip = Path.Combine(Path.GetTempPath(), "ytDownloader_update.zip");

                    AppendOutput($"[RunUpdateAsync] ZIP 다운로드 시작: {assetName}");

                    using (var response = await httpClient.GetAsync(assetUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        var contentLength = response.Content.Headers.ContentLength ?? -1L;
                        var totalRead = 0L;
                        var buffer = new byte[81920];
                        var stopwatch = Stopwatch.StartNew();

                        await using var stream = await response.Content.ReadAsStreamAsync();
                        await using var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None);

                        int read;
                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fs.WriteAsync(buffer, 0, read);
                            totalRead += read;

                            if (contentLength > 0)
                            {
                                double progress = (double)totalRead / contentLength * 100.0;
                                double speedBytes = totalRead / stopwatch.Elapsed.TotalSeconds; // B/s
                                double etaSec = (contentLength - totalRead) / (speedBytes > 0 ? speedBytes : 1);
                                double remainingBytes = (contentLength - totalRead);
                                double totalBytes = contentLength;

                                Dispatcher.Invoke(() =>
                                {
                                    progressBar.Value = progress;
                                    //txtProgress.Text = $"{progress:F1}%"; // ✅ 퍼센트 표시
                                    txtSpeed.Text = FormatSpeed(speedBytes);
                                    txtEta.Text = $"{FormatEta(etaSec)} / {FormatSize(remainingBytes)} 남음 / 총 {FormatSize(totalBytes)}";
                                });
                            }
                        }
                    }

                    long fileSize = new FileInfo(tempZip).Length;
                    AppendOutput($"[RunUpdateAsync] ZIP 다운로드 완료: {fileSize} bytes");
                    File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ZIP 다운로드 완료 ({fileSize} bytes){Environment.NewLine}");

                    // 📌 ZIP 유효성 검사
                    try
                    {
                        using var archive = ZipFile.OpenRead(tempZip);
                        if (archive.Entries == null || archive.Entries.Count == 0)
                            throw new InvalidDataException("ZIP 파일이 비어 있음");

                        AppendOutput($"[RunUpdateAsync] ZIP 유효성 검사 통과: {archive.Entries.Count} 개 항목");
                    }
                    catch (Exception ex)
                    {
                        AppendOutput($"❌ ZIP 유효성 검사 실패: {ex.Message}");
                        File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ZIP 유효성 검사 실패: {ex}{Environment.NewLine}");
                        Dispatcher.Invoke(() =>
                        {
                            updateWindow.Close();
                            MessageBox.Show("다운로드된 ZIP이 손상되었습니다.\n" + ex.Message,
                                "업데이트 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                        return;
                    }

                    // 📌 실행 경로
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string updaterPath = Path.Combine(baseDir, "Updater.exe");
                    string installDir = baseDir;
                    string targetExe = Process.GetCurrentProcess().MainModule!.FileName;

                    AppendOutput($"[RunUpdateAsync] updaterPath = {updaterPath}");

                    // 📌 Updater 실행
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = updaterPath,
                        Arguments = $"\"{tempZip}\" \"{installDir}\" \"{targetExe}\"",
                        UseShellExecute = true,
                        WorkingDirectory = baseDir
                    });

                    Dispatcher.Invoke(() =>
                    {
                        updateWindow.Close();
                        Application.Current.Shutdown();
                    });
                }
                catch (Exception ex)
                {
                    AppendOutput($"❌ RunUpdateAsync 실패: {ex.Message}");
                    File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] RunUpdateAsync 실패: {ex}{Environment.NewLine}");
                    Dispatcher.Invoke(() =>
                    {
                        updateWindow.Close();
                        MessageBox.Show("업데이트 실패: " + ex.Message,
                            "업데이트 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });
        }






        private void LoadSettings()
        {
            if (string.IsNullOrWhiteSpace(Settings.Default.SavePath))
            {
                Settings.Default.SavePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"
                );
                Settings.Default.Save();
            }

            txtSavePath.Text = Settings.Default.SavePath;
            ChkSingleVideo.IsChecked = Settings.Default.SingleVideoOnly;
            SubtitleCheckBox.IsChecked = Settings.Default.DownloadSubtitle;
            SubtitleLangComboBox.Text = Settings.Default.SubtitleLang;
            SubtitleFormatComboBox.Text = Settings.Default.SubtitleFormat;
            ChkWriteThumbnail.IsChecked = Settings.Default.SaveThumbnail;
            ChkStructuredFolders.IsChecked = Settings.Default.UseStructuredFolder;
            comboFormat.SelectedIndex = Settings.Default.Format;
            txtMaxDownloads.Text = Settings.Default.MaxDownloads.ToString();
        }

        private void SaveSettings()
        {
            Settings.Default.SavePath = txtSavePath.Text;
            Settings.Default.SingleVideoOnly = ChkSingleVideo.IsChecked ?? false;
            Settings.Default.DownloadSubtitle = SubtitleCheckBox.IsChecked ?? false;
            Settings.Default.SubtitleLang = SubtitleLangComboBox.Text;
            Settings.Default.SubtitleFormat = SubtitleFormatComboBox.Text;
            Settings.Default.SaveThumbnail = ChkWriteThumbnail.IsChecked ?? false;
            Settings.Default.UseStructuredFolder = ChkStructuredFolders.IsChecked ?? false;
            Settings.Default.Format = comboFormat.SelectedIndex;
            Settings.Default.MaxDownloads = int.TryParse(txtMaxDownloads.Text, out int n) ? n : 5;
            Settings.Default.Save();
        }

        private void AppendOutput(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtOutput.AppendText(message + Environment.NewLine);
                txtOutput.ScrollToEnd();
            });
        }

        private void StartDownload(string url, bool isChannelMode = false)
        {
            if (!File.Exists(ytdlpPath) || !File.Exists(ffmpegPath))
            {
                AppendOutput("❌ tools 폴더에 yt-dlp.exe 또는 ffmpeg.exe가 없습니다.");
                return;
            }

            string savePath = txtSavePath.Text;
            if (string.IsNullOrWhiteSpace(savePath))
            {
                AppendOutput("❌ 저장 경로가 비어 있습니다.");
                return;
            }

            Directory.CreateDirectory(savePath);

            // 날짜/시간 태그 생성
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            StringBuilder args = new StringBuilder();

            // 포맷에 따라 파일명 패턴 다르게 지정
            if (comboFormat.SelectedIndex == 0) // 영상 (최고화질)
            {
                string outputTemplate = $"%(title)s_{timestamp}_best.%(ext)s";
                args.Append($"-o \"{Path.Combine(savePath, outputTemplate)}\" ");
                args.Append("-f bestvideo+bestaudio ");
            }
            else if (comboFormat.SelectedIndex == 1) // 영상 (1080p)
            {
                string outputTemplate = $"%(title)s_{timestamp}_1080p.%(ext)s";
                args.Append($"-o \"{Path.Combine(savePath, outputTemplate)}\" ");
                args.Append("-f \"bestvideo[height=1080]+bestaudio/best[height=1080]\" ");
            }
            else if (comboFormat.SelectedIndex == 2) // 영상 (720p)
            {
                string outputTemplate = $"%(title)s_{timestamp}_720p.%(ext)s";
                args.Append($"-o \"{Path.Combine(savePath, outputTemplate)}\" ");
                args.Append("-f \"bestvideo[height=720]+bestaudio/best[height=720]\" ");
            }
            else if (comboFormat.SelectedIndex == 3) // 영상 (480p)
            {
                string outputTemplate = $"%(title)s_{timestamp}_480p.%(ext)s";
                args.Append($"-o \"{Path.Combine(savePath, outputTemplate)}\" ");
                args.Append("-f \"bestvideo[height=480]+bestaudio/best[height=480]\" ");
            }
            else if (comboFormat.SelectedIndex == 4) // 음악 (MP3)
            {
                string outputTemplate = $"%(title)s_{timestamp}_audio_mp3.%(ext)s";
                args.Append($"-o \"{Path.Combine(savePath, outputTemplate)}\" ");
                args.Append("--extract-audio --audio-format mp3 --audio-quality 0 ");
                args.Append("--embed-thumbnail --add-metadata ");
            }
            else if (comboFormat.SelectedIndex == 5) // 음악 (Best - 원본 유지)
            {
                string outputTemplate = $"%(title)s_{timestamp}_audio_best.%(ext)s";
                args.Append($"-o \"{Path.Combine(savePath, outputTemplate)}\" ");
                args.Append("--extract-audio --audio-format best ");
                args.Append("--embed-thumbnail --add-metadata ");
            }
            else if (comboFormat.SelectedIndex == 6) // 음악 (FLAC - 무손실 변환)
            {
                string outputTemplate = $"%(title)s_{timestamp}_audio_flac.%(ext)s";
                args.Append($"-o \"{Path.Combine(savePath, outputTemplate)}\" ");
                args.Append("--extract-audio --audio-format flac ");
                args.Append("--embed-thumbnail --add-metadata ");
            }
            if (ChkSingleVideo.IsChecked == true)
                args.Append("--no-playlist ");

            if (SubtitleCheckBox.IsChecked == true)
                args.Append($"--write-sub --sub-lang {SubtitleLangComboBox.Text} --sub-format {SubtitleFormatComboBox.Text} ");

            if (ChkWriteThumbnail.IsChecked == true)
                args.Append("--write-thumbnail ");

            if (ChkStructuredFolders.IsChecked == true)
            {
                // 구조적 폴더를 사용할 경우에도 동일하게 timestamp 적용
                string structuredTemplate = $"%(uploader)s/%(playlist)s/%(title)s_{timestamp}_%(ext)s.%(ext)s";
                args.Append($"-o \"{Path.Combine(savePath, structuredTemplate)}\" ");
            }

            if (isChannelMode)
            {
                int max = int.TryParse(txtMaxDownloads.Text, out int n) ? n : 5;
                args.Append($"--max-downloads {max} ");
            }

            args.Append("--windows-filenames ");
            args.Append($"\"{url}\"");

            Dispatcher.Invoke(() =>
            {
                progressBar.Value = 0;
                txtSpeed.Text = "-";
                txtEta.Text = "-";
            });

            Task.Run(() =>
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = ytdlpPath,
                        Arguments = args.ToString(),
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using (Process proc = new Process())
                    {
                        proc.StartInfo = psi;
                        proc.OutputDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                AppendOutput(e.Data);

                                var match = Regex.Match(e.Data, @"(\d+(?:\.\d+)?)%.*?of.*?at\s+([0-9.]+\w+/s).*?ETA\s+([\d:]+)");
                                if (match.Success)
                                {
                                    double percent = double.Parse(match.Groups[1].Value);
                                    string speed = match.Groups[2].Value;
                                    string eta = match.Groups[3].Value;

                                    Dispatcher.Invoke(() =>
                                    {
                                        progressBar.Value = percent;
                                        txtSpeed.Text = speed;
                                        txtEta.Text = eta;
                                    });
                                }
                            }
                        };
                        proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) AppendOutput(e.Data); };

                        proc.Start();
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();
                        proc.WaitForExit();
                    }

                    AppendOutput("✅ 다운로드 완료");
                    Dispatcher.Invoke(() =>
                    {
                        progressBar.Value = 100;
                        txtSpeed.Text = "-";
                        txtEta.Text = "완료 ✅";
                    });
                }
                catch (Exception ex)
                {
                    AppendOutput("❌ 오류: " + ex.Message);
                }
            });
        }



        private void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            string[] urls = txtUrls.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var url in urls)
                StartDownload(url, false);
        }

        private void btnChannelDownload_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            string[] urls = txtChannelUrl.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var url in urls)
                StartDownload(url, true);
        }

        private void btnPaste_Click(object sender, RoutedEventArgs e) => txtUrls.AppendText(Clipboard.ContainsText() ? Clipboard.GetText() + Environment.NewLine : "");
        private void btnUrlsClear_Click(object sender, RoutedEventArgs e) => txtUrls.Clear();
        private void btnChannelPaste_Click(object sender, RoutedEventArgs e) => txtChannelUrl.AppendText(Clipboard.ContainsText() ? Clipboard.GetText() + Environment.NewLine : "");
        private void btnChannelClear_Click(object sender, RoutedEventArgs e) => txtChannelUrl.Clear();

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtSavePath.Text = dialog.SelectedPath;
                    SaveSettings();
                }
            }
        }

        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(txtSavePath.Text))
                Process.Start("explorer.exe", txtSavePath.Text);
        }

        private void BtnLoadSubtitleLang_Click(object sender, RoutedEventArgs e)
        {
            SubtitleLangComboBox.Items.Clear();
            SubtitleLangComboBox.Items.Add("ko");
            SubtitleLangComboBox.Items.Add("en");
            SubtitleLangComboBox.Items.Add("ja");
            SubtitleLangComboBox.Items.Add("zh");
            SubtitleLangComboBox.Items.Add("fr");
            SubtitleLangComboBox.Items.Add("de");
            SubtitleLangComboBox.SelectedIndex = 0;

            // 자막 포맷 목록
            SubtitleFormatComboBox.Items.Clear();
            SubtitleFormatComboBox.Items.Add("srt");
            SubtitleFormatComboBox.Items.Add("vtt");
            SubtitleFormatComboBox.Items.Add("ass");
            SubtitleFormatComboBox.SelectedIndex = 0;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }


        private void Window_Closing(object sender, CancelEventArgs e) => SaveSettings();
    }
}

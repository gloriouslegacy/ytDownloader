using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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

            UpdateYtDlp();     
            _ = CheckForUpdate();          
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

                var response = await httpClient.GetAsync("https://api.github.com/repos/gloriouslegacy/ytDownloader/releases");
                if (!response.IsSuccessStatusCode)
                {
                    AppendOutput($"❌ 업데이트 확인 실패: {response.StatusCode}");
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var releases = JArray.Parse(json);

                if (releases == null || releases.Count == 0)
                {
                    AppendOutput("ℹ️ 첫 버전입니다. 아직 등록된 릴리스가 없습니다.");
                    return;
                }

                var latest = releases[0];
                string latestTag = latest["tag_name"]?.ToString() ?? "";
                bool isPre = latest["prerelease"]?.ToObject<bool>() ?? false;

                string currentVersion = Assembly
                    .GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? "0.0.0";

                string latestTagClean = latestTag.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                    ? latestTag.Substring(1)
                    : latestTag;

                AppendOutput($"[INFO] 현재 버전: {currentVersion}");
                AppendOutput($"[INFO] 최신 버전: {latestTagClean}");

                if (latestTagClean != currentVersion)
                {
                    Dispatcher.Invoke(() =>
                    {
                        string preMsg = isPre ? "Pre-release" : "정식 릴리스";
                        if (MessageBox.Show($"새 {preMsg} {latestTag} 버전이 있습니다. 업데이트 하시겠습니까?",
                            "업데이트 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            // 메인 ZIP 파일을 명확히 선택 (yt_downloader_로 시작하는 파일)
                            var zipAsset = latest["assets"]?
                                .FirstOrDefault(a =>
                                {
                                    string assetName = a["name"]?.ToString() ?? "";
                                    return assetName.StartsWith("yt_downloader_", StringComparison.OrdinalIgnoreCase) &&
                                           assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                                });

                            if (zipAsset != null)
                            {
                                string assetUrl = zipAsset["browser_download_url"]?.ToString();
                                string assetName = zipAsset["name"]?.ToString() ?? "";

                                AppendOutput($"[INFO] 선택된 에셋: {assetName}");
                                AppendOutput($"[INFO] 다운로드 URL: {assetUrl}");

                                if (!string.IsNullOrEmpty(assetUrl))
                                {
                                    _ = RunUpdateAsync(assetUrl);
                                }
                            }
                            else
                            {
                                AppendOutput("❌ 메인 ZIP 에셋을 찾을 수 없습니다.");
                                AppendOutput("[INFO] 사용 가능한 에셋들:");
                                foreach (var asset in latest["assets"] ?? new JArray())
                                {
                                    AppendOutput($"[INFO]   - {asset["name"]}");
                                }
                            }
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


        //string FormatEta(double seconds)
        //{
        //    if (seconds < 0) return "--:--";
        //    TimeSpan ts = TimeSpan.FromSeconds(seconds);

        //    if (ts.TotalHours >= 1)
        //        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";  
        //    else
        //        return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";  
        //}

        //string FormatSize(double bytes)
        //{
        //    if (bytes < 1024) return $"{bytes:F0} B";
        //    double kb = bytes / 1024d;
        //    if (kb < 1024) return $"{kb:F1} KB";
        //    double mb = kb / 1024d;
        //    if (mb < 1024) return $"{mb:F1} MB";
        //    double gb = mb / 1024d;
        //    return $"{gb:F2} GB";
        //}

        //string FormatSpeed(double bps)
        //    {
        //        if (bps <= 0) return "-";
        //        if (bps < 1024) return $"{bps:F0} B/s";
        //        if (bps < 1024 * 1024) return $"{bps / 1024:F1} KB/s";
        //        return $"{bps / (1024 * 1024):F1} MB/s";
        //    }

        private async Task RunUpdateAsync(string zipUrl)
        {
            try
            {
                using var httpClient = new HttpClient();

                // 다운로드 ZIP을 %TEMP% 에 저장
                string tempZip = Path.Combine(Path.GetTempPath(), "ytDownloader_update.zip");

                // 진행 로그 출력
                AppendOutput("[INFO] 업데이트 ZIP 다운로드 시작");
                using (var response = await httpClient.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    await using var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fs);
                }
                AppendOutput($"[INFO] ZIP 다운로드 완료: {tempZip}");

                // Updater.exe 경로 - updater 서브폴더에서 먼저 찾기
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string updaterPath = Path.Combine(baseDir, "updater", "Updater.exe");

                // 만약 updater 폴더가 없다면 기본 경로에서 찾기 (하위 호환성)
                if (!File.Exists(updaterPath))
                {
                    updaterPath = Path.Combine(baseDir, "Updater.exe");
                }

                // 현재 실행 중인 exe 경로 - 정규화 및 정리
                string targetExe = Process.GetCurrentProcess().MainModule!.FileName;
                targetExe = Path.GetFullPath(targetExe).Trim('"'); // 정규화 및 따옴표 제거

                // 설치 경로 - 정규화하고 마지막 구분자 제거, 따옴표 제거
                string installDir = Path.GetDirectoryName(targetExe) ?? baseDir;
                installDir = Path.GetFullPath(installDir).TrimEnd('\\', '/').Trim('"');

                AppendOutput("[INFO] Updater 실행 준비");
                AppendOutput($"[INFO] baseDir     = '{baseDir}'");
                AppendOutput($"[INFO] updaterPath = '{updaterPath}'");
                AppendOutput($"[INFO] installDir  = '{installDir}'");
                AppendOutput($"[INFO] targetExe   = '{targetExe}'");

                // Updater.exe 존재 확인
                if (!File.Exists(updaterPath))
                {
                    AppendOutput($"[ERROR] Updater.exe를 찾을 수 없습니다:");
                    AppendOutput($"[ERROR]   시도한 경로 1: {Path.Combine(baseDir, "updater", "Updater.exe")}");
                    AppendOutput($"[ERROR]   시도한 경로 2: {Path.Combine(baseDir, "Updater.exe")}");
                    MessageBox.Show($"Updater.exe를 찾을 수 없습니다.\n\n확인된 경로:\n1. {Path.Combine(baseDir, "updater", "Updater.exe")}\n2. {Path.Combine(baseDir, "Updater.exe")}",
                        "업데이트 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // updater 폴더에 있는 경우 작업 디렉터리도 설정
                string workingDir = Path.GetDirectoryName(updaterPath) ?? baseDir;

                // 경로에 공백이 포함된 경우를 대비하여 각각 개별적으로 따옴표 처리
                string arguments = $"\"{tempZip}\" \"{installDir}\" \"{targetExe}\"";

                // 관리자 권한으로 Updater 실행
                var psi = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas", // 관리자 권한 요청
                    WindowStyle = ProcessWindowStyle.Normal,
                    WorkingDirectory = workingDir // Updater 실행 시 적절한 작업 디렉터리 설정
                };

                AppendOutput($"[INFO] 최종 명령줄: {psi.FileName} {psi.Arguments}");
                AppendOutput($"[INFO] 작업 디렉터리: {psi.WorkingDirectory}");

                // 명령줄이 올바른지 검증 (디버그용)
                string[] testArgs = arguments.Split(new[] { "\" \"" }, StringSplitOptions.None);
                AppendOutput($"[DEBUG] 파싱될 인자 개수: {testArgs.Length}");
                for (int i = 0; i < testArgs.Length; i++)
                {
                    string cleanArg = testArgs[i].Trim('"');
                    AppendOutput($"[DEBUG] 인자[{i}]: '{cleanArg}'");
                }

                var process = Process.Start(psi);
                if (process == null)
                {
                    AppendOutput("[ERROR] Updater 프로세스 시작 실패");
                    MessageBox.Show("Updater 실행에 실패했습니다.", "업데이트 오류",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                AppendOutput("[INFO] Updater가 실행되었습니다. 현재 앱을 종료합니다.");

                // 현재 앱 종료
                await Task.Delay(1000);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                AppendOutput($"[ERROR] RunUpdateAsync 실패: {ex.Message}");
                AppendOutput($"[ERROR] StackTrace: {ex.StackTrace}");
                MessageBox.Show("업데이트 실행 실패: " + ex.Message,
                    "업데이트 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            StringBuilder args = new StringBuilder();

            if (comboFormat.SelectedIndex == 0)   
            {
                string outputTemplate = $"%(title)s_{timestamp}_best.%(ext)s";
                args.Append($"-o \"{Path.Combine(savePath, outputTemplate)}\" ");
                args.Append("-f bestvideo+bestaudio ");
            }
            else if (comboFormat.SelectedIndex == 1)   
            {
                string outputTemplate = $"%(title)s_{timestamp}_1080p.%(ext)s";
                args.Append($"-o \"{Path.Combine(savePath, outputTemplate)}\" ");
                args.Append("-f \"bestvideo[height=1080]+bestaudio/best[height=1080]\" ");
            }
            else if (comboFormat.SelectedIndex == 2)   
            {
                string outputTemplate = $"%(title)s_{timestamp}_720p.%(ext)s";
                args.Append($"-o \"{Path.Combine(savePath, outputTemplate)}\" ");
                args.Append("-f \"bestvideo[height=720]+bestaudio/best[height=720]\" ");
            }
            else if (comboFormat.SelectedIndex == 3)   
            {
                string outputTemplate = $"%(title)s_{timestamp}_480p.%(ext)s";
                args.Append($"-o \"{Path.Combine(savePath, outputTemplate)}\" ");
                args.Append("-f \"bestvideo[height=480]+bestaudio/best[height=480]\" ");
            }
            else if (comboFormat.SelectedIndex == 4)   
            {
                string outputTemplate = $"%(title)s_{timestamp}_audio_mp3.%(ext)s";
                args.Append($"-o \"{Path.Combine(savePath, outputTemplate)}\" ");
                args.Append("--extract-audio --audio-format mp3 --audio-quality 0 ");
                args.Append("--embed-thumbnail --add-metadata ");
            }
            else if (comboFormat.SelectedIndex == 5)      
            {
                string outputTemplate = $"%(title)s_{timestamp}_audio_best.%(ext)s";
                args.Append($"-o \"{Path.Combine(savePath, outputTemplate)}\" ");
                args.Append("--extract-audio --audio-format best ");
                args.Append("--embed-thumbnail --add-metadata ");
            }
            else if (comboFormat.SelectedIndex == 6)      
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

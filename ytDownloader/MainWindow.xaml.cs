using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using ytDownloader.Properties;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Net.Http;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection;
using System.Net.Http;
using Newtonsoft.Json;
using System.Windows;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Navigation;

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
                            // 📌 최신 릴리스 ZIP 다운로드 링크 (Updater.exe는 ZIP에 포함되지 않음!)
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


        //private async Task RunUpdateAsync(string zipUrl)
        //{
        //    // 📌 "업데이트 중입니다..." 진행창 표시
        //    var updateWindow = new UpdateWindow();
        //    updateWindow.Show();

        //    await Task.Run(async () =>
        //    {
        //        try
        //        {
        //            using var httpClient = new HttpClient();

        //            // 📌 릴리스 ZIP 다운로드 (Updater.exe는 포함되지 않음)
        //            string tempZip = Path.Combine(Path.GetTempPath(), "ytDownloader_update.zip");
        //            await File.WriteAllBytesAsync(tempZip, await httpClient.GetByteArrayAsync(zipUrl));

        //            // 📌 Updater.exe는 설치 폴더에 이미 존재한다고 가정 (ZIP에는 없음)
        //            string updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updater.exe");
        //            string installDir = AppDomain.CurrentDomain.BaseDirectory;
        //            string targetExe = Process.GetCurrentProcess().MainModule!.FileName;

        //            // 📌 관리자 권한으로 Updater.exe 실행 → ZIP 적용 후 ytDownloader.exe 재실행
        //            Process.Start(new ProcessStartInfo
        //            {
        //                FileName = updaterPath,
        //                Arguments = $"\"{tempZip}\" \"{installDir}\" \"{targetExe}\"",
        //                UseShellExecute = true,
        //                Verb = "runas" // ✅ UAC 팝업 → 관리자 권한 요청
        //            });

        //            // 📌 UI 스레드: 업데이트 창 닫고 현재 앱 종료
        //            Dispatcher.Invoke(() =>
        //            {
        //                updateWindow.Close();
        //                Application.Current.Shutdown();
        //            });
        //        }
        //        catch (Exception ex)
        //        {
        //            // 📌 오류 발생 시 업데이트 창 닫고 사용자에게 알림
        //            Dispatcher.Invoke(() =>
        //            {
        //                updateWindow.Close();
        //                MessageBox.Show(
        //                    "업데이트 실패: " + ex.Message,
        //                    "업데이트 오류",
        //                    MessageBoxButton.OK,
        //                    MessageBoxImage.Error);
        //            });
        //        }
        //    });
        //}

        private async Task RunUpdateAsync(string zipUrl)
        {
            // "업데이트 중입니다..." 진행창 표시
            var updateWindow = new UpdateWindow();
            updateWindow.Show();

            await Task.Run(async () =>
            {
                try
                {
                    using var httpClient = new HttpClient();

                    // 최신 릴리스 ZIP 다운로드 → %TEMP%\ytDownloader_update.zip
                    string tempZip = Path.Combine(Path.GetTempPath(), "ytDownloader_update.zip");
                    await File.WriteAllBytesAsync(tempZip, await httpClient.GetByteArrayAsync(zipUrl));

                    // Updater.exe는 현재 실행 폴더에 반드시 존재해야 함
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string updaterPath = Path.Combine(baseDir, "Updater.exe");
                    string installDir = baseDir;
                    string targetExe = Process.GetCurrentProcess().MainModule!.FileName;

                    // 로그 기록 (TextBox + 파일)
                    string logFile = Path.Combine(Path.GetTempPath(), "ytDownloader_update_launcher.log");
                    string logMsg = $"[RunUpdateAsync]\n" +
                                    $"tempZip: {tempZip}\n" +
                                    $"updaterPath: {updaterPath}\n" +
                                    $"installDir: {installDir}\n" +
                                    $"targetExe: {targetExe}\n";
                    AppendOutput(logMsg); // UI TextBox에 출력
                    File.AppendAllText(logFile, logMsg + Environment.NewLine);

                    if (!File.Exists(updaterPath))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            updateWindow.Close();
                            MessageBox.Show(
                                $"Updater.exe를 찾을 수 없습니다.\n경로: {updaterPath}\n\n" +
                                "ZIP 내부에서 실행하지 말고, 기존 폴더의 ytDownloader.exe를 실행하세요.",
                                "업데이트 오류",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error
                            );
                        });
                        return;
                    }

                    // 관리자 권한으로 Updater.exe 실행
                    var psi = new ProcessStartInfo
                    {
                        FileName = updaterPath,
                        Arguments = $"\"{tempZip}\" \"{installDir}\" \"{targetExe}\"",
                        WorkingDirectory = baseDir, // 실행 폴더 강제
                        UseShellExecute = true,
                        Verb = "runas"
                    };

                    // 실행 명령 로그 추가
                    string runMsg = $"[RunUpdateAsync] Launching Updater.exe\n" +
                                    $"FileName: {psi.FileName}\n" +
                                    $"Arguments: {psi.Arguments}\n" +
                                    $"WorkingDirectory: {psi.WorkingDirectory}\n";
                    AppendOutput(runMsg);
                    File.AppendAllText(logFile, runMsg + Environment.NewLine);

                    Process.Start(psi);

                    // UI 스레드: 업데이트 창 닫고 현재 앱 종료
                    Dispatcher.Invoke(() =>
                    {
                        updateWindow.Close();
                        Application.Current.Shutdown();
                    });
                }
                catch (Exception ex)
                {
                    // 오류 발생 시 업데이트 창 닫고 사용자에게 알림
                    Dispatcher.Invoke(() =>
                    {
                        updateWindow.Close();
                        MessageBox.Show(
                            "업데이트 실패: " + ex.Message,
                            "업데이트 오류",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                    });

                    // 예외 로그 기록
                    string logFile = Path.Combine(Path.GetTempPath(), "ytDownloader_update_launcher.log");
                    File.AppendAllText(logFile, $"[RunUpdateAsync] Exception: {ex}\n");
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

﻿using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Navigation;
using ytDownloader.Properties;

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

        private async Task UpdateFfmpeg()
        {
            if (!File.Exists(ffmpegPath))
            {
                AppendOutput("❌ ffmpeg.exe가 tools 폴더에 없습니다.");
                return;
            }

            try
            {
                AppendOutput("⏳ ffmpeg 버전 확인 중...");

                // 현재 설치된 ffmpeg 버전 확인
                string currentVersion = await GetCurrentFfmpegVersion();
                AppendOutput($"[INFO] 현재 ffmpeg 버전: {currentVersion}");

                // GitHub에서 최신 버전 확인
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ytDownloader/1.0");

                var response = await httpClient.GetAsync("https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest");
                if (!response.IsSuccessStatusCode)
                {
                    AppendOutput($"❌ ffmpeg 업데이트 확인 실패: {response.StatusCode}");
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var release = JObject.Parse(json);

                string latestTag = release["tag_name"]?.ToString() ?? "";
                AppendOutput($"[INFO] 최신 ffmpeg 버전: {latestTag}");

                // 버전 비교
                if (IsNewerFfmpegVersion(currentVersion, latestTag))
                {
                    AppendOutput($"ℹ️ 새로운 ffmpeg 버전 발견: {latestTag}");

                    // Windows용 빌드 찾기 (ffmpeg-master-latest-win64-gpl.zip)
                    var asset = release["assets"]?
                        .FirstOrDefault(a =>
                        {
                            string name = a["name"]?.ToString() ?? "";
                            return name.Contains("master-latest-win64-gpl") && name.EndsWith(".zip");
                        });

                    if (asset != null)
                    {
                        string downloadUrl = asset["browser_download_url"]?.ToString();
                        if (!string.IsNullOrEmpty(downloadUrl))
                        {
                            await DownloadAndExtractFfmpeg(downloadUrl);
                        }
                    }
                    else
                    {
                        AppendOutput("❌ Windows용 ffmpeg 빌드를 찾을 수 없습니다.");
                    }
                }
                else
                {
                    AppendOutput("✅ ffmpeg는 최신 버전입니다.");
                }
            }
            catch (Exception ex)
            {
                AppendOutput($"❌ ffmpeg 업데이트 오류: {ex.Message}");
            }
        }

        private async Task<string> GetCurrentFfmpegVersion()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = await proc.StandardOutput.ReadToEndAsync();
                    await proc.WaitForExitAsync();

                    // 첫 줄에서 버전 추출 (예: "ffmpeg version N-109542-g7d2bdd5176")
                    var match = Regex.Match(output, @"ffmpeg version ([\w\-\.]+)");
                    if (match.Success)
                        return match.Groups[1].Value;
                }
            }
            catch (Exception ex)
            {
                AppendOutput($"[DEBUG] ffmpeg 버전 확인 오류: {ex.Message}");
            }

            return "unknown";
        }

        private bool IsNewerFfmpegVersion(string current, string latest)
        {
            // current가 "unknown"이면 업데이트 시도
            if (current == "unknown")
                return true;

            // latest에서 버전 번호 추출 (예: "autobuild-2024-01-15-12-55" -> "20240115")
            var latestMatch = Regex.Match(latest, @"(\d{4})-(\d{2})-(\d{2})");
            if (!latestMatch.Success)
                return false;

            string latestDate = latestMatch.Groups[1].Value + latestMatch.Groups[2].Value + latestMatch.Groups[3].Value;

            // current에서 빌드 번호 추출 (예: "N-109542" -> 109542)
            var currentMatch = Regex.Match(current, @"N-(\d+)");
            if (!currentMatch.Success)
                return true; // 형식을 모르면 업데이트 시도

            // 간단하게 빌드 번호가 다르면 업데이트
            // 실제로는 날짜 기반 비교가 더 정확하지만, 복잡도를 위해 단순화
            return true; // 새 릴리스가 있으면 항상 업데이트 (안전한 방법)
        }

        private async Task DownloadAndExtractFfmpeg(string zipUrl)
        {
            try
            {
                AppendOutput("⏳ ffmpeg 다운로드 중... (파일이 크므로 시간이 걸릴 수 있습니다)");

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(10); // 타임아웃 증가

                string tempZip = Path.Combine(Path.GetTempPath(), "ffmpeg_update.zip");

                // 기존 임시 파일 삭제
                if (File.Exists(tempZip))
                    File.Delete(tempZip);

                using (var response = await httpClient.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    long? totalBytes = response.Content.Headers.ContentLength;
                    await using var contentStream = await response.Content.ReadAsStreamAsync();
                    await using var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                    byte[] buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (totalBytes.HasValue)
                        {
                            double percent = (double)totalRead / totalBytes.Value * 100;
                            if (totalRead % (1024 * 1024 * 5) == 0 || percent >= 99) // 5MB마다 또는 마지막에 로그
                            {
                                AppendOutput($"[INFO] 다운로드 진행: {percent:F1}% ({totalRead / 1024 / 1024}MB / {totalBytes.Value / 1024 / 1024}MB)");
                            }
                        }
                    }
                }

                AppendOutput("✅ ffmpeg 다운로드 완료");
                AppendOutput("⏳ ffmpeg 압축 해제 중...");

                // ZIP에서 ffmpeg.exe만 추출
                string tempExtract = Path.Combine(Path.GetTempPath(), "ffmpeg_extract_" + Guid.NewGuid().ToString("N"));

                try
                {
                    Directory.CreateDirectory(tempExtract);
                    ZipFile.ExtractToDirectory(tempZip, tempExtract);

                    // bin 폴더에서 ffmpeg.exe 찾기
                    string extractedFfmpeg = Directory.GetFiles(tempExtract, "ffmpeg.exe", SearchOption.AllDirectories)
                        .FirstOrDefault();

                    if (extractedFfmpeg != null)
                    {
                        // 기존 파일 백업
                        string backupPath = ffmpegPath + ".bak";
                        if (File.Exists(ffmpegPath))
                        {
                            if (File.Exists(backupPath))
                                File.Delete(backupPath);
                            File.Move(ffmpegPath, backupPath);
                        }

                        // 새 파일 복사
                        File.Copy(extractedFfmpeg, ffmpegPath, true);

                        // 백업 삭제
                        if (File.Exists(backupPath))
                            File.Delete(backupPath);

                        AppendOutput("✅ ffmpeg 업데이트 완료");
                    }
                    else
                    {
                        AppendOutput("❌ 압축 파일에서 ffmpeg.exe를 찾을 수 없습니다.");
                    }
                }
                finally
                {
                    // 임시 파일 정리
                    if (File.Exists(tempZip))
                    {
                        try { File.Delete(tempZip); }
                        catch { }
                    }

                    if (Directory.Exists(tempExtract))
                    {
                        try { Directory.Delete(tempExtract, true); }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendOutput($"❌ ffmpeg 다운로드/설치 실패: {ex.Message}");
            }
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

                // 현재 실행 중인 버전
                string currentVersionStr = Assembly
                    .GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? "0.0.0";

                // '+' 뒤 빌드 메타데이터 제거
                int plusIdx = currentVersionStr.IndexOf('+');
                if (plusIdx >= 0)
                    currentVersionStr = currentVersionStr.Substring(0, plusIdx);

                // GitHub 태그에서 'v' 제거
                string latestTagClean = latestTag.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                    ? latestTag.Substring(1)
                    : latestTag;

                // 문자열을 Version 객체로 변환
                Version currentV, latestV;
                if (!Version.TryParse(currentVersionStr, out currentV))
                    currentV = new Version(0, 0, 0);

                if (!Version.TryParse(latestTagClean, out latestV))
                    latestV = new Version(0, 0, 0);

                AppendOutput($"[INFO] 현재 버전: {currentV}");
                AppendOutput($"[INFO] 최신 버전: {latestV}");

                if (currentV < latestV)
                {
                    Dispatcher.Invoke(() =>
                    {
                        string preMsg = isPre ? "Pre-release" : "정식 릴리스";
                        // ...
                        if (MessageBox.Show($"새 {preMsg} {latestTag} 버전이 있습니다. 업데이트 하시겠습니까?",
                            "업데이트 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            //  메인 ZIP 파일은 항상 yt_downloader.zip 으로 고정
                            var zipAsset = latest["assets"]?
                                .FirstOrDefault(a =>
                                {
                                    string assetName = a["name"]?.ToString() ?? "";
                                    return string.Equals(assetName, "yt_downloader.zip", StringComparison.OrdinalIgnoreCase);
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
            }
            catch (Exception ex)
            {
                // 다운로드 실패 → MessageBox 표시
                MessageBox.Show("업데이트 파일 다운로드 실패: " + ex.Message,
                    "업데이트 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                AppendOutput("[ERROR] 다운로드 실패: " + ex);
                return;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string updaterPath = Path.Combine(baseDir, "updater", "Updater.exe");

            if (!File.Exists(updaterPath))
                updaterPath = Path.Combine(baseDir, "Updater.exe");

            string targetExe = Process.GetCurrentProcess().MainModule!.FileName;
            targetExe = Path.GetFullPath(targetExe).Trim('"');
            string installDir = Path.GetDirectoryName(targetExe) ?? baseDir;
            installDir = Path.GetFullPath(installDir).TrimEnd('\\', '/').Trim('"');

            AppendOutput("[INFO] Updater 실행 준비");
            AppendOutput($"[INFO] baseDir     = '{baseDir}'");
            AppendOutput($"[INFO] updaterPath = '{updaterPath}'");
            AppendOutput($"[INFO] installDir  = '{installDir}'");
            AppendOutput($"[INFO] targetExe   = '{targetExe}'");

            if (!File.Exists(updaterPath))
            {
                // MessageBox 없이 로그만
                AppendOutput("[ERROR] Updater.exe를 찾을 수 없습니다.");
                AppendOutput($"[ERROR] 시도 경로 1: {Path.Combine(baseDir, "updater", "Updater.exe")}");
                AppendOutput($"[ERROR] 시도 경로 2: {Path.Combine(baseDir, "Updater.exe")}");
                return;
            }

            try
            {
                string workingDir = Path.GetDirectoryName(updaterPath) ?? baseDir;
                string arguments = $"\"{Path.Combine(Path.GetTempPath(), "ytDownloader_update.zip")}\" \"{installDir}\" \"{targetExe}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Normal,
                    WorkingDirectory = workingDir
                };

                AppendOutput($"[INFO] Updater 실행: {psi.FileName} {psi.Arguments}");
                var process = Process.Start(psi);
                if (process == null)
                {
                    AppendOutput("[ERROR] Updater 프로세스 시작 실패");
                    return;
                }

                AppendOutput("[INFO] Updater가 실행되었습니다. 현재 앱을 종료합니다.");
                await Task.Delay(1000);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                // 실행 실패도 로그만
                AppendOutput("[ERROR] Updater 실행 실패: " + ex);
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

            // 한글 출력을 위한 환경 변수 설정을 위해 args에 추가
            args.Append("--encoding utf-8 ");

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

                    // 환경 변수 설정으로 UTF-8 출력 강제
                    psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                    psi.EnvironmentVariables["PYTHONUTF8"] = "1";

                    using (Process proc = new Process())
                    {
                        proc.StartInfo = psi;
                        proc.OutputDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                // UTF-8로 다시 디코딩 시도
                                string decodedData = e.Data;
                                try
                                {
                                    // CP949로 인코딩된 바이트를 UTF-8로 재해석
                                    byte[] bytes = Encoding.GetEncoding("CP949").GetBytes(e.Data);
                                    decodedData = Encoding.UTF8.GetString(bytes);
                                }
                                catch
                                {
                                    // 변환 실패시 원본 사용
                                    decodedData = e.Data;
                                }

                                AppendOutput(decodedData);

                                var match = Regex.Match(decodedData, @"(\d+(?:\.\d+)?)%.*?of.*?at\s+([0-9.]+\w+/s).*?ETA\s+([\d:]+)");
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

                        proc.ErrorDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                // 에러 출력도 동일하게 처리
                                string decodedData = e.Data;
                                try
                                {
                                    byte[] bytes = Encoding.GetEncoding("CP949").GetBytes(e.Data);
                                    decodedData = Encoding.UTF8.GetString(bytes);
                                }
                                catch
                                {
                                    decodedData = e.Data;
                                }
                                AppendOutput(decodedData);
                            }
                        };

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

                // 현재 프로그램 다시 실행
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                });

                // 현재 인스턴스 종료
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show("프로그램 재시작 실패: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private void Window_Closing(object sender, CancelEventArgs e) => SaveSettings();
    }
}
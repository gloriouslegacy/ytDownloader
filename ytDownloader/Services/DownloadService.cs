using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ytDownloader.Models;

namespace ytDownloader.Services
{
    /// <summary>
    /// 다운로드 진행 상태 데이터
    /// </summary>
    public class DownloadProgressEventArgs : EventArgs
    {
        public double Percent { get; set; }
        public string Speed { get; set; } = string.Empty;
        public string Eta { get; set; } = string.Empty;
    }

    /// <summary>
    /// 다운로드 서비스
    /// </summary>
    public class DownloadService
    {
        private readonly string _toolsPath;
        private readonly string _ytdlpPath;
        private readonly string _ffmpegPath;

        /// <summary>
        /// 로그 메시지 출력 이벤트
        /// </summary>
        public event Action<string>? LogMessage;

        /// <summary>
        /// 다운로드 진행 상태 이벤트
        /// </summary>
        public event EventHandler<DownloadProgressEventArgs>? ProgressChanged;

        /// <summary>
        /// 다운로드 완료 이벤트
        /// </summary>
        public event EventHandler? DownloadCompleted;

        public DownloadService()
        {
            _toolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
            _ytdlpPath = Path.Combine(_toolsPath, "yt-dlp.exe");
            _ffmpegPath = Path.Combine(_toolsPath, "ffmpeg.exe");
        }

        /// <summary>
        /// 다운로드 시작
        /// </summary>
        public async Task StartDownloadAsync(DownloadOptions options)
        {
            if (!File.Exists(_ytdlpPath) || !File.Exists(_ffmpegPath))
            {
                LogMessage?.Invoke("❌ tools 폴더에 yt-dlp.exe 또는 ffmpeg.exe가 없습니다.");
                return;
            }

            if (string.IsNullOrWhiteSpace(options.SavePath))
            {
                LogMessage?.Invoke("❌ 저장 경로가 비어 있습니다.");
                return;
            }

            Directory.CreateDirectory(options.SavePath);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            StringBuilder args = new StringBuilder();

            // 한글 출력을 위한 환경 변수 설정을 위해 args에 추가
            args.Append("--encoding utf-8 ");

            // 포맷에 따른 옵션 설정
            switch (options.Format)
            {
                case VideoFormat.BestVideo:
                    {
                        string outputTemplate = $"%(title)s_{timestamp}_best.%(ext)s";
                        args.Append($"-o \"{Path.Combine(options.SavePath, outputTemplate)}\" ");
                        args.Append("-f bestvideo+bestaudio ");
                        break;
                    }
                case VideoFormat.Video1080p:
                    {
                        string outputTemplate = $"%(title)s_{timestamp}_1080p.%(ext)s";
                        args.Append($"-o \"{Path.Combine(options.SavePath, outputTemplate)}\" ");
                        args.Append("-f \"bestvideo[height=1080]+bestaudio/best[height=1080]\" ");
                        break;
                    }
                case VideoFormat.Video720p:
                    {
                        string outputTemplate = $"%(title)s_{timestamp}_720p.%(ext)s";
                        args.Append($"-o \"{Path.Combine(options.SavePath, outputTemplate)}\" ");
                        args.Append("-f \"bestvideo[height=720]+bestaudio/best[height=720]\" ");
                        break;
                    }
                case VideoFormat.Video480p:
                    {
                        string outputTemplate = $"%(title)s_{timestamp}_480p.%(ext)s";
                        args.Append($"-o \"{Path.Combine(options.SavePath, outputTemplate)}\" ");
                        args.Append("-f \"bestvideo[height=480]+bestaudio/best[height=480]\" ");
                        break;
                    }
                case VideoFormat.AudioMP3:
                    {
                        string outputTemplate = $"%(title)s_{timestamp}_audio_mp3.%(ext)s";
                        args.Append($"-o \"{Path.Combine(options.SavePath, outputTemplate)}\" ");
                        args.Append("--extract-audio --audio-format mp3 --audio-quality 0 ");
                        args.Append("--embed-thumbnail --add-metadata ");
                        break;
                    }
                case VideoFormat.AudioBest:
                    {
                        string outputTemplate = $"%(title)s_{timestamp}_audio_best.%(ext)s";
                        args.Append($"-o \"{Path.Combine(options.SavePath, outputTemplate)}\" ");
                        args.Append("--extract-audio --audio-format best ");
                        args.Append("--embed-thumbnail --add-metadata ");
                        break;
                    }
                case VideoFormat.AudioFLAC:
                    {
                        string outputTemplate = $"%(title)s_{timestamp}_audio_flac.%(ext)s";
                        args.Append($"-o \"{Path.Combine(options.SavePath, outputTemplate)}\" ");
                        args.Append("--extract-audio --audio-format flac ");
                        args.Append("--embed-thumbnail --add-metadata ");
                        break;
                    }
            }

            if (options.SingleVideoOnly)
                args.Append("--no-playlist ");

            if (options.DownloadSubtitle)
                args.Append($"--write-sub --sub-lang {options.SubtitleLang} --sub-format {options.SubtitleFormat} ");

            if (options.SaveThumbnail)
                args.Append("--write-thumbnail ");

            if (options.UseStructuredFolder)
            {
                string structuredTemplate = $"%(uploader)s/%(playlist)s/%(title)s_{timestamp}_%(ext)s.%(ext)s";
                args.Append($"-o \"{Path.Combine(options.SavePath, structuredTemplate)}\" ");
            }

            if (options.IsChannelMode)
            {
                args.Append($"--max-downloads {options.MaxDownloads} ");
            }

            args.Append("--windows-filenames ");
            args.Append($"\"{options.Url}\"");

            // 진행률 초기화
            ProgressChanged?.Invoke(this, new DownloadProgressEventArgs
            {
                Percent = 0,
                Speed = "-",
                Eta = "-"
            });

            await Task.Run(() =>
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = _ytdlpPath,
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

                                LogMessage?.Invoke(decodedData);

                                var match = Regex.Match(decodedData, @"(\d+(?:\.\d+)?)%.*?of.*?at\s+([0-9.]+\w+/s).*?ETA\s+([\d:]+)");
                                if (match.Success)
                                {
                                    double percent = double.Parse(match.Groups[1].Value);
                                    string speed = match.Groups[2].Value;
                                    string eta = match.Groups[3].Value;

                                    ProgressChanged?.Invoke(this, new DownloadProgressEventArgs
                                    {
                                        Percent = percent,
                                        Speed = speed,
                                        Eta = eta
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
                                LogMessage?.Invoke(decodedData);
                            }
                        };

                        proc.Start();
                        proc.BeginOutputReadLine();
                        proc.BeginErrorReadLine();
                        proc.WaitForExit();
                    }

                    LogMessage?.Invoke("✅ 다운로드 완료");
                    ProgressChanged?.Invoke(this, new DownloadProgressEventArgs
                    {
                        Percent = 100,
                        Speed = "-",
                        Eta = "완료 ✅"
                    });

                    DownloadCompleted?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke("❌ 오류: " + ex.Message);
                }
            });
        }
    }
}

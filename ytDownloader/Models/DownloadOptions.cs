namespace ytDownloader.Models
{
    /// <summary>
    /// 다운로드 옵션 데이터 모델
    /// </summary>
    public class DownloadOptions
    {
        /// <summary>다운로드할 URL</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>저장 경로</summary>
        public string SavePath { get; set; } = string.Empty;

        /// <summary>비디오 포맷</summary>
        public VideoFormat Format { get; set; }

        /// <summary>단일 비디오만 다운로드 (재생목록 무시)</summary>
        public bool SingleVideoOnly { get; set; }

        /// <summary>자막 다운로드 여부</summary>
        public bool DownloadSubtitle { get; set; }

        /// <summary>자막 언어</summary>
        public string SubtitleLang { get; set; } = "ko";

        /// <summary>자막 포맷</summary>
        public string SubtitleFormat { get; set; } = "srt";

        /// <summary>썸네일 저장 여부</summary>
        public bool SaveThumbnail { get; set; }

        /// <summary>채널/재생목록 폴더 구조 사용 여부</summary>
        public bool UseStructuredFolder { get; set; }

        /// <summary>채널 모드 여부 (재생목록/채널 다운로드)</summary>
        public bool IsChannelMode { get; set; }

        /// <summary>채널 모드 시 최대 다운로드 개수</summary>
        public int MaxDownloads { get; set; } = 5;

        /// <summary>
        /// AppSettings로부터 DownloadOptions 생성
        /// </summary>
        public static DownloadOptions FromAppSettings(AppSettings settings, string url, bool isChannelMode = false)
        {
            return new DownloadOptions
            {
                Url = url,
                SavePath = settings.SavePath,
                Format = settings.Format,
                SingleVideoOnly = settings.SingleVideoOnly,
                DownloadSubtitle = settings.DownloadSubtitle,
                SubtitleLang = settings.SubtitleLang,
                SubtitleFormat = settings.SubtitleFormat,
                SaveThumbnail = settings.SaveThumbnail,
                UseStructuredFolder = settings.UseStructuredFolder,
                IsChannelMode = isChannelMode,
                MaxDownloads = settings.MaxDownloads
            };
        }

        /// <summary>
        /// SchedulerSettings로부터 DownloadOptions 생성
        /// </summary>
        public static DownloadOptions FromSchedulerSettings(SchedulerSettings settings, string url, bool isChannelMode = false)
        {
            return new DownloadOptions
            {
                Url = url,
                SavePath = settings.SavePath,
                Format = settings.Format,
                SingleVideoOnly = settings.SingleVideoOnly,
                DownloadSubtitle = settings.DownloadSubtitle,
                SubtitleLang = settings.SubtitleLang,
                SubtitleFormat = settings.SubtitleFormat,
                SaveThumbnail = settings.SaveThumbnail,
                UseStructuredFolder = settings.UseStructuredFolder,
                IsChannelMode = isChannelMode,
                MaxDownloads = settings.MaxDownloads
            };
        }
    }
}

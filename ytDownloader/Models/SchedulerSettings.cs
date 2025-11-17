namespace ytDownloader.Models
{
    /// <summary>
    /// 스케줄러별 개별 설정 데이터 모델
    /// </summary>
    public class SchedulerSettings
    {
        /// <summary>스케줄러 TaskName (식별자)</summary>
        public string TaskName { get; set; } = string.Empty;

        /// <summary>채널 URL (선택사항)</summary>
        public string ChannelUrl { get; set; } = string.Empty;

        /// <summary>저장 경로</summary>
        public string SavePath { get; set; } = string.Empty;

        /// <summary>비디오 포맷 (0-6)</summary>
        public VideoFormat Format { get; set; } = VideoFormat.BestVideo;

        /// <summary>자막 다운로드 여부</summary>
        public bool DownloadSubtitle { get; set; }

        /// <summary>자막 언어 (예: ko, en, ja)</summary>
        public string SubtitleLang { get; set; } = "ko";

        /// <summary>자막 포맷 (예: srt, vtt, ass)</summary>
        public string SubtitleFormat { get; set; } = "srt";

        /// <summary>다운로드 완료 알림 여부</summary>
        public bool EnableNotification { get; set; } = true;

        /// <summary>채널 다운로드 시 최대 개수</summary>
        public int MaxDownloads { get; set; } = 5;

        /// <summary>썸네일 저장 여부</summary>
        public bool SaveThumbnail { get; set; }

        /// <summary>채널/재생목록 폴더 구조 사용 여부</summary>
        public bool UseStructuredFolder { get; set; } = true;

        /// <summary>단일 비디오만 다운로드 (재생목록 무시)</summary>
        public bool SingleVideoOnly { get; set; }

        /// <summary>
        /// 기본 설정값으로 초기화
        /// </summary>
        public static SchedulerSettings CreateDefault(string taskName, AppSettings defaultSettings)
        {
            return new SchedulerSettings
            {
                TaskName = taskName,
                SavePath = defaultSettings.SavePath,
                Format = defaultSettings.Format,
                DownloadSubtitle = defaultSettings.DownloadSubtitle,
                SubtitleLang = defaultSettings.SubtitleLang,
                SubtitleFormat = defaultSettings.SubtitleFormat,
                EnableNotification = defaultSettings.EnableNotification,
                MaxDownloads = defaultSettings.MaxDownloads,
                SaveThumbnail = defaultSettings.SaveThumbnail,
                UseStructuredFolder = defaultSettings.UseStructuredFolder,
                SingleVideoOnly = defaultSettings.SingleVideoOnly
            };
        }
    }
}

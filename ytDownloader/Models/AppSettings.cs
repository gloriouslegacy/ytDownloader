using System.IO;

namespace ytDownloader.Models
{
    /// <summary>
    /// 앱 설정 데이터 모델
    /// </summary>
    public class AppSettings
    {
        /// <summary>저장 경로</summary>
        public string SavePath { get; set; } = string.Empty;

        /// <summary>단일 비디오만 다운로드 (재생목록 무시)</summary>
        public bool SingleVideoOnly { get; set; }

        /// <summary>자막 다운로드 여부</summary>
        public bool DownloadSubtitle { get; set; }

        /// <summary>자막 언어 (예: ko, en, ja)</summary>
        public string SubtitleLang { get; set; } = "ko";

        /// <summary>자막 포맷 (예: srt, vtt, ass)</summary>
        public string SubtitleFormat { get; set; } = "srt";

        /// <summary>썸네일 저장 여부</summary>
        public bool SaveThumbnail { get; set; }

        /// <summary>채널/재생목록 폴더 구조 사용 여부</summary>
        public bool UseStructuredFolder { get; set; }

        /// <summary>비디오 포맷 (0-6)</summary>
        public VideoFormat Format { get; set; } = VideoFormat.BestVideo;

        /// <summary>채널 다운로드 시 최대 개수</summary>
        public int MaxDownloads { get; set; } = 5;

        /// <summary>테마 설정 (Dark, Light)</summary>
        public string Theme { get; set; } = "Dark";

        /// <summary>언어 설정 (ko, en)</summary>
        public string Language { get; set; } = "ko";

        /// <summary>
        /// 기본 다운로드 경로 반환
        /// </summary>
        public static string GetDefaultSavePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads"
            );
        }
    }
}

namespace ytDownloader.Models
{
    /// <summary>
    /// 비디오/오디오 다운로드 포맷
    /// </summary>
    public enum VideoFormat
    {
        /// <summary>영상 (최고화질)</summary>
        BestVideo = 0,

        /// <summary>영상 (1080p)</summary>
        Video1080p = 1,

        /// <summary>영상 (720p)</summary>
        Video720p = 2,

        /// <summary>영상 (480p)</summary>
        Video480p = 3,

        /// <summary>음악 (MP3 V0 VBR)</summary>
        AudioMP3 = 4,

        /// <summary>음악 (Best 원본유지)</summary>
        AudioBest = 5,

        /// <summary>음악 (FLAC 무손실 변환)</summary>
        AudioFLAC = 6
    }
}

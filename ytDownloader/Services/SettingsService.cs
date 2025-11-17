using Newtonsoft.Json.Linq;
using ytDownloader.Models;

namespace ytDownloader.Services
{
    /// <summary>
    /// 앱 설정 관리 서비스
    /// </summary>
    public class SettingsService
    {
        private readonly string _settingsPath;
        private readonly string _settingsFile;

        public SettingsService()
        {
            // %appdata%\ytDownloader 폴더에 설정 저장
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ytDownloader"
            );
            _settingsFile = Path.Combine(_settingsPath, "settings.json");

            // 설정 폴더가 없으면 생성
            if (!Directory.Exists(_settingsPath))
            {
                Directory.CreateDirectory(_settingsPath);
            }
        }

        /// <summary>
        /// 설정 로드
        /// </summary>
        public AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFile))
                {
                    var json = File.ReadAllText(_settingsFile);
                    var settings = JObject.Parse(json);

                    return new AppSettings
                    {
                        SavePath = settings["SavePath"]?.ToString() ?? AppSettings.GetDefaultSavePath(),
                        SingleVideoOnly = settings["SingleVideoOnly"]?.ToObject<bool>() ?? false,
                        DownloadSubtitle = settings["DownloadSubtitle"]?.ToObject<bool>() ?? false,
                        SubtitleLang = settings["SubtitleLang"]?.ToString() ?? "ko",
                        SubtitleFormat = settings["SubtitleFormat"]?.ToString() ?? "srt",
                        SaveThumbnail = settings["SaveThumbnail"]?.ToObject<bool>() ?? false,
                        UseStructuredFolder = settings["UseStructuredFolder"]?.ToObject<bool>() ?? false,
                        Format = (VideoFormat)(settings["Format"]?.ToObject<int>() ?? 0),
                        MaxDownloads = settings["MaxDownloads"]?.ToObject<int>() ?? 5
                    };
                }
                else
                {
                    // 기본값 설정
                    var defaultSettings = new AppSettings
                    {
                        SavePath = AppSettings.GetDefaultSavePath(),
                        SingleVideoOnly = false,
                        DownloadSubtitle = false,
                        SubtitleLang = "ko",
                        SubtitleFormat = "srt",
                        SaveThumbnail = false,
                        UseStructuredFolder = false,
                        Format = VideoFormat.BestVideo,
                        MaxDownloads = 5
                    };

                    // 기본값으로 저장
                    SaveSettings(defaultSettings);
                    return defaultSettings;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"설정 로드 오류: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 설정 저장
        /// </summary>
        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var json = new JObject
                {
                    ["SavePath"] = settings.SavePath,
                    ["SingleVideoOnly"] = settings.SingleVideoOnly,
                    ["DownloadSubtitle"] = settings.DownloadSubtitle,
                    ["SubtitleLang"] = settings.SubtitleLang,
                    ["SubtitleFormat"] = settings.SubtitleFormat,
                    ["SaveThumbnail"] = settings.SaveThumbnail,
                    ["UseStructuredFolder"] = settings.UseStructuredFolder,
                    ["Format"] = (int)settings.Format,
                    ["MaxDownloads"] = settings.MaxDownloads
                };

                File.WriteAllText(_settingsFile, json.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception($"설정 저장 오류: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 설정 파일 경로 반환
        /// </summary>
        public string GetSettingsFilePath() => _settingsFile;

        /// <summary>
        /// 설정 폴더 경로 반환
        /// </summary>
        public string GetSettingsFolderPath() => _settingsPath;
    }
}

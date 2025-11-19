using Newtonsoft.Json.Linq;
using System.IO;
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
        private readonly string _scheduleSettingsPath;

        public SettingsService()
        {
            // %appdata%\ytDownloader 폴더에 설정 저장
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ytDownloader"
            );
            _settingsFile = Path.Combine(_settingsPath, "settings.json");
            _scheduleSettingsPath = Path.Combine(_settingsPath, "ytSchedule");

            // 설정 폴더가 없으면 생성
            if (!Directory.Exists(_settingsPath))
            {
                Directory.CreateDirectory(_settingsPath);
            }

            // 스케줄 설정 폴더가 없으면 생성
            if (!Directory.Exists(_scheduleSettingsPath))
            {
                Directory.CreateDirectory(_scheduleSettingsPath);
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

                    var appSettings = new AppSettings
                    {
                        SavePath = settings["SavePath"]?.ToString() ?? AppSettings.GetDefaultSavePath(),
                        SingleVideoOnly = settings["SingleVideoOnly"]?.ToObject<bool>() ?? false,
                        DownloadSubtitle = settings["DownloadSubtitle"]?.ToObject<bool>() ?? false,
                        SubtitleLang = settings["SubtitleLang"]?.ToString() ?? "ko",
                        SubtitleFormat = settings["SubtitleFormat"]?.ToString() ?? "srt",
                        SaveThumbnail = settings["SaveThumbnail"]?.ToObject<bool>() ?? false,
                        UseStructuredFolder = settings["UseStructuredFolder"]?.ToObject<bool>() ?? false,
                        Format = (VideoFormat)(settings["Format"]?.ToObject<int>() ?? 0),
                        MaxDownloads = settings["MaxDownloads"]?.ToObject<int>() ?? 5,
                        Theme = settings["Theme"]?.ToString() ?? "Dark",
                        Language = settings["Language"]?.ToString() ?? "ko",
                        EnableNotification = settings["EnableNotification"]?.ToObject<bool>() ?? true,
                        DontShowDefenderWarning = settings["DontShowDefenderWarning"]?.ToObject<bool>() ?? false
                    };

                    // ScheduledChannels 로드
                    if (settings["ScheduledChannels"] != null)
                    {
                        appSettings.ScheduledChannels = settings["ScheduledChannels"]?.ToObject<List<ScheduledChannel>>() ?? new List<ScheduledChannel>();
                    }

                    // SchedulerSettingsMap 로드 (개별 파일에서)
                    appSettings.SchedulerSettingsMap = LoadAllSchedulerSettings();

                    return appSettings;
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
                        MaxDownloads = 5,
                        Theme = "Dark",
                        Language = "ko",
                        EnableNotification = true
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
                    ["MaxDownloads"] = settings.MaxDownloads,
                    ["Theme"] = settings.Theme,
                    ["Language"] = settings.Language,
                    ["EnableNotification"] = settings.EnableNotification,
                    ["DontShowDefenderWarning"] = settings.DontShowDefenderWarning,
                    ["ScheduledChannels"] = JToken.FromObject(settings.ScheduledChannels)
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

        /// <summary>
        /// 스케줄 설정 폴더 경로 반환
        /// </summary>
        public string GetScheduleSettingsFolderPath() => _scheduleSettingsPath;

        /// <summary>
        /// 개별 스케줄러 설정 저장
        /// </summary>
        public void SaveSchedulerSettings(SchedulerSettings settings)
        {
            try
            {
                // Windows 작업 스케줄러는 작업 이름 앞에 백슬래시를 붙일 수 있으므로 제거
                string cleanTaskName = settings.TaskName.TrimStart('\\').TrimEnd('\\');
                // 파일명에 사용할 수 없는 문자 제거
                string safeFileName = string.Join("_", cleanTaskName.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(_scheduleSettingsPath, $"{safeFileName}.json");

                var json = new JObject
                {
                    ["TaskName"] = settings.TaskName,
                    ["ChannelUrl"] = settings.ChannelUrl,
                    ["SavePath"] = settings.SavePath,
                    ["Format"] = (int)settings.Format,
                    ["DownloadSubtitle"] = settings.DownloadSubtitle,
                    ["SubtitleLang"] = settings.SubtitleLang,
                    ["SubtitleFormat"] = settings.SubtitleFormat,
                    ["EnableNotification"] = settings.EnableNotification,
                    ["MaxDownloads"] = settings.MaxDownloads,
                    ["UseStructuredFolder"] = settings.UseStructuredFolder,
                    ["SaveThumbnail"] = settings.SaveThumbnail,
                    ["SingleVideoOnly"] = settings.SingleVideoOnly
                };

                File.WriteAllText(filePath, json.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception($"스케줄러 설정 저장 오류: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 개별 스케줄러 설정 로드
        /// </summary>
        public SchedulerSettings? LoadSchedulerSettings(string taskName)
        {
            try
            {
                // Windows 작업 스케줄러는 작업 이름 앞에 백슬래시를 붙일 수 있으므로 제거
                string cleanTaskName = taskName.TrimStart('\\').TrimEnd('\\');
                string safeFileName = string.Join("_", cleanTaskName.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(_scheduleSettingsPath, $"{safeFileName}.json");

                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var settings = JObject.Parse(json);

                    return new SchedulerSettings
                    {
                        TaskName = settings["TaskName"]?.ToString() ?? taskName,
                        ChannelUrl = settings["ChannelUrl"]?.ToString() ?? "",
                        SavePath = settings["SavePath"]?.ToString() ?? "",
                        Format = (VideoFormat)(settings["Format"]?.ToObject<int>() ?? 0),
                        DownloadSubtitle = settings["DownloadSubtitle"]?.ToObject<bool>() ?? false,
                        SubtitleLang = settings["SubtitleLang"]?.ToString() ?? "ko",
                        SubtitleFormat = settings["SubtitleFormat"]?.ToString() ?? "srt",
                        EnableNotification = settings["EnableNotification"]?.ToObject<bool>() ?? true,
                        MaxDownloads = settings["MaxDownloads"]?.ToObject<int>() ?? 5,
                        UseStructuredFolder = settings["UseStructuredFolder"]?.ToObject<bool>() ?? true,
                        SaveThumbnail = settings["SaveThumbnail"]?.ToObject<bool>() ?? false,
                        SingleVideoOnly = settings["SingleVideoOnly"]?.ToObject<bool>() ?? false
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"스케줄러 설정 로드 오류: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 모든 스케줄러 설정 로드
        /// </summary>
        public Dictionary<string, SchedulerSettings> LoadAllSchedulerSettings()
        {
            var settingsMap = new Dictionary<string, SchedulerSettings>();

            try
            {
                if (!Directory.Exists(_scheduleSettingsPath))
                {
                    return settingsMap;
                }

                var files = Directory.GetFiles(_scheduleSettingsPath, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var settings = JObject.Parse(json);

                        var schedulerSettings = new SchedulerSettings
                        {
                            TaskName = settings["TaskName"]?.ToString() ?? "",
                            ChannelUrl = settings["ChannelUrl"]?.ToString() ?? "",
                            SavePath = settings["SavePath"]?.ToString() ?? "",
                            Format = (VideoFormat)(settings["Format"]?.ToObject<int>() ?? 0),
                            DownloadSubtitle = settings["DownloadSubtitle"]?.ToObject<bool>() ?? false,
                            SubtitleLang = settings["SubtitleLang"]?.ToString() ?? "ko",
                            SubtitleFormat = settings["SubtitleFormat"]?.ToString() ?? "srt",
                            EnableNotification = settings["EnableNotification"]?.ToObject<bool>() ?? true,
                            MaxDownloads = settings["MaxDownloads"]?.ToObject<int>() ?? 5,
                            UseStructuredFolder = settings["UseStructuredFolder"]?.ToObject<bool>() ?? true,
                            SaveThumbnail = settings["SaveThumbnail"]?.ToObject<bool>() ?? false,
                            SingleVideoOnly = settings["SingleVideoOnly"]?.ToObject<bool>() ?? false
                        };

                        if (!string.IsNullOrEmpty(schedulerSettings.TaskName))
                        {
                            settingsMap[schedulerSettings.TaskName] = schedulerSettings;
                        }
                    }
                    catch
                    {
                        // 개별 파일 로드 실패 시 무시하고 계속
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"스케줄러 설정 전체 로드 오류: {ex.Message}", ex);
            }

            return settingsMap;
        }

        /// <summary>
        /// 개별 스케줄러 설정 삭제
        /// </summary>
        public bool DeleteSchedulerSettings(string taskName)
        {
            try
            {
                // Windows 작업 스케줄러는 작업 이름 앞에 백슬래시를 붙일 수 있으므로 제거
                string cleanTaskName = taskName.TrimStart('\\').TrimEnd('\\');
                string safeFileName = string.Join("_", cleanTaskName.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(_scheduleSettingsPath, $"{safeFileName}.json");

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"스케줄러 설정 삭제 오류: {ex.Message}", ex);
            }
        }
    }
}

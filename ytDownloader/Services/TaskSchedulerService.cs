using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace ytDownloader.Services
{
    /// <summary>
    /// 스케줄 작업 정보
    /// </summary>
    public class ScheduleTaskInfo : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string TaskName { get; set; } = string.Empty;
        public int Frequency { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
        public string DisplayText => $"{TaskName}: {FrequencyText} {Hour:D2}:{Minute:D2}";

        /// <summary>체크박스 선택 여부</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        /// <summary>실행 주기 (DataGrid용)</summary>
        public string FrequencyDays => FrequencyText;

        /// <summary>실행 시간 (DataGrid용)</summary>
        public string ExecutionTime => $"{Hour:D2}:{Minute:D2}";

        /// <summary>다음 실행 시간 (DataGrid용)</summary>
        public DateTime NextRunTime
        {
            get
            {
                var now = DateTime.Now;
                var today = new DateTime(now.Year, now.Month, now.Day, Hour, Minute, 0);

                if (today > now)
                {
                    return today;
                }
                else
                {
                    return today.AddDays(Frequency);
                }
            }
        }

        private string FrequencyText
        {
            get
            {
                return Frequency switch
                {
                    1 => "매일",
                    7 => "매주",
                    14 => "2주마다",
                    21 => "3주마다",
                    30 => "매월",
                    _ => $"{Frequency}일마다"
                };
            }
        }

        /// <summary>
        /// ToString 오버라이드 - ListBox에서 자동으로 표시됨
        /// </summary>
        public override string ToString()
        {
            return DisplayText;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Windows 작업 스케줄러 관리 서비스
    /// </summary>
    public class TaskSchedulerService
    {
        private const string TaskNamePrefix = "ytDownloader_Schedule_";

        /// <summary>
        /// 작업 스케줄러에 작업 등록
        /// </summary>
        /// <param name="frequency">실행 주기 (일 단위, 1-31)</param>
        /// <param name="hour">실행 시간 (시)</param>
        /// <param name="minute">실행 시간 (분)</param>
        /// <returns>생성된 작업 이름 (실패 시 null)</returns>
        public string? CreateScheduledTask(int frequency, int hour, int minute)
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    return null;
                }

                // 작업 이름에 주기 정보 포함 (파싱 용이)
                // 형식: ytDownloader_Schedule_{frequency}D_{hour:D2}{minute:D2}_{timestamp}
                string taskName = $"{TaskNamePrefix}{frequency}D_{hour:D2}{minute:D2}_{DateTime.Now:yyyyMMdd_HHmmss}";

                // schtasks 명령어로 작업 생성 (taskName을 인자로 전달)
                string arguments = $"/Create /TN \"{taskName}\" " +
                    $"/TR \"\\\"{exePath}\\\" --scheduled \\\"{taskName}\\\"\" " +
                    $"/SC DAILY /MO {frequency} " +
                    $"/ST {hour:D2}:{minute:D2} " +
                    $"/F"; // /F: 강제 생성

                var processInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null) return null;

                    process.WaitForExit();
                    return process.ExitCode == 0 ? taskName : null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 작업 스케줄러에서 특정 작업 삭제
        /// </summary>
        /// <param name="taskName">삭제할 작업 이름</param>
        /// <returns>성공 여부</returns>
        public bool DeleteScheduledTask(string taskName)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Delete /TN \"{taskName}\" /F",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null) return false;

                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 등록된 모든 ytDownloader 스케줄 작업 가져오기
        /// </summary>
        /// <returns>등록된 작업 목록</returns>
        public List<ScheduleTaskInfo> GetAllScheduledTasks()
        {
            var tasks = new List<ScheduleTaskInfo>();

            try
            {
                // 먼저 ytDownloader 작업 목록 가져오기
                var processInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/Query /FO LIST",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null) return tasks;

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0) return tasks;

                    // ytDownloader 작업 이름 추출
                    var taskNames = new List<string>();
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var line in lines)
                    {
                        if (line.StartsWith("작업 이름:") || line.StartsWith("TaskName:"))
                        {
                            var taskName = line.Split(':', 2)[1].Trim();
                            if (taskName.Contains(TaskNamePrefix))
                            {
                                taskNames.Add(taskName);
                            }
                        }
                    }

                    // 각 작업에 대해 XML로 상세 정보 가져오기
                    foreach (var taskName in taskNames)
                    {
                        var taskInfo = GetTaskInfoFromXml(taskName);
                        if (taskInfo != null)
                        {
                            tasks.Add(taskInfo);
                        }
                    }
                }
            }
            catch
            {
                // 예외 발생 시 빈 목록 반환
            }

            return tasks;
        }

        /// <summary>
        /// 작업 이름에서 작업 정보 추출
        /// </summary>
        private ScheduleTaskInfo? GetTaskInfoFromXml(string taskName)
        {
            try
            {
                // 작업 이름 형식: ytDownloader_Schedule_{frequency}D_{hour:D2}{minute:D2}_{timestamp}
                // 예: ytDownloader_Schedule_3D_0200_20251118_020000
                var match = Regex.Match(taskName, @"ytDownloader_Schedule_(\d+)D_(\d{2})(\d{2})_");
                if (match.Success)
                {
                    int frequency = int.Parse(match.Groups[1].Value);
                    int hour = int.Parse(match.Groups[2].Value);
                    int minute = int.Parse(match.Groups[3].Value);

                    return new ScheduleTaskInfo
                    {
                        TaskName = taskName,
                        Frequency = frequency,
                        Hour = hour,
                        Minute = minute
                    };
                }

                // 파싱 실패 시 기본값 반환 (하위 호환성)
                return new ScheduleTaskInfo
                {
                    TaskName = taskName,
                    Frequency = 1,
                    Hour = 0,
                    Minute = 0
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 시간 문자열에서 시간과 분 추출
        /// </summary>
        private bool TryParseScheduleInfo(string timeStr, out int hour, out int minute)
        {
            hour = 0;
            minute = 0;

            try
            {
                // 예: "2025-11-18 오전 2:00:00" 형식
                var match = Regex.Match(timeStr, @"(\d{1,2}):(\d{2})");
                if (match.Success)
                {
                    hour = int.Parse(match.Groups[1].Value);
                    minute = int.Parse(match.Groups[2].Value);

                    // 오후 처리
                    if (timeStr.Contains("오후") || timeStr.Contains("PM"))
                    {
                        if (hour < 12) hour += 12;
                    }
                    else if (timeStr.Contains("오전") || timeStr.Contains("AM"))
                    {
                        if (hour == 12) hour = 0;
                    }

                    return true;
                }
            }
            catch
            {
                // 파싱 실패
            }

            return false;
        }

        /// <summary>
        /// 주기 정보 문자열에서 숫자 추출
        /// </summary>
        private int ExtractFrequency(string line)
        {
            try
            {
                // 한글: "간격: 3일 마다" 또는 "간격:                  3일 마다"
                // 영어: "Repeat:                       Every 3 Day(s)"
                var match = Regex.Match(line, @"(\d+)\s*[일Day]");
                if (match.Success)
                {
                    return int.Parse(match.Groups[1].Value);
                }
            }
            catch
            {
                // 파싱 실패
            }

            return 1; // 기본값
        }

        /// <summary>
        /// 작업 스케줄러에 작업이 하나라도 등록되어 있는지 확인
        /// </summary>
        /// <returns>등록 여부</returns>
        public bool IsTaskScheduled()
        {
            return GetAllScheduledTasks().Count > 0;
        }

        /// <summary>
        /// 모든 ytDownloader 스케줄 작업 삭제
        /// </summary>
        /// <returns>성공한 삭제 개수</returns>
        public int DeleteAllScheduledTasks()
        {
            int deletedCount = 0;
            var tasks = GetAllScheduledTasks();

            foreach (var task in tasks)
            {
                if (DeleteScheduledTask(task.TaskName))
                {
                    deletedCount++;
                }
            }

            return deletedCount;
        }
    }
}

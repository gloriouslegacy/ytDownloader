using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace ytDownloader.Services
{
    /// <summary>
    /// 스케줄 작업 정보
    /// </summary>
    public class ScheduleTaskInfo
    {
        public string TaskName { get; set; } = string.Empty;
        public int Frequency { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
        public string DisplayText => $"{TaskName}: {FrequencyText} {Hour:D2}:{Minute:D2}";

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

                // 고유한 작업 이름 생성 (타임스탬프 사용)
                string taskName = $"{TaskNamePrefix}{DateTime.Now:yyyyMMdd_HHmmss}";

                // schtasks 명령어로 작업 생성
                string arguments = $"/Create /TN \"{taskName}\" " +
                    $"/TR \"\\\"{exePath}\\\" --scheduled\" " +
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
                var processInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/Query /FO LIST /V",
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

                    // 출력 파싱하여 ytDownloader 작업만 필터링
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    string? currentTaskName = null;
                    string? nextRunTime = null;

                    foreach (var line in lines)
                    {
                        if (line.StartsWith("작업 이름:") || line.StartsWith("TaskName:"))
                        {
                            var taskName = line.Split(':', 2)[1].Trim();
                            if (taskName.Contains(TaskNamePrefix))
                            {
                                currentTaskName = taskName;
                            }
                            else
                            {
                                currentTaskName = null;
                            }
                        }
                        else if (currentTaskName != null && (line.StartsWith("다음 실행 시간:") || line.StartsWith("Next Run Time:")))
                        {
                            nextRunTime = line.Split(':', 2)[1].Trim();

                            // 시간 정보 추출 시도
                            if (TryParseScheduleInfo(nextRunTime, out int hour, out int minute))
                            {
                                tasks.Add(new ScheduleTaskInfo
                                {
                                    TaskName = currentTaskName,
                                    Hour = hour,
                                    Minute = minute,
                                    Frequency = 1 // 기본값 (실제 값은 더 복잡한 파싱 필요)
                                });
                            }
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

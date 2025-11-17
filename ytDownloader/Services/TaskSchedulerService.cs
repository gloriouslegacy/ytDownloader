using System.Diagnostics;
using System.IO;

namespace ytDownloader.Services
{
    /// <summary>
    /// Windows 작업 스케줄러 관리 서비스
    /// </summary>
    public class TaskSchedulerService
    {
        private const string TaskName = "ytDownloader_ScheduledDownload";

        /// <summary>
        /// 작업 스케줄러에 작업 등록
        /// </summary>
        /// <param name="frequency">실행 주기 (일 단위, 1-31)</param>
        /// <param name="hour">실행 시간 (시)</param>
        /// <param name="minute">실행 시간 (분)</param>
        /// <returns>성공 여부</returns>
        public bool CreateScheduledTask(int frequency, int hour, int minute)
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    return false;
                }

                // 기존 작업 삭제
                DeleteScheduledTask();

                // schtasks 명령어로 작업 생성
                string arguments = $"/Create /TN \"{TaskName}\" " +
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
        /// 작업 스케줄러에서 작업 삭제
        /// </summary>
        /// <returns>성공 여부</returns>
        public bool DeleteScheduledTask()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Delete /TN \"{TaskName}\" /F",
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
        /// 작업 스케줄러에 작업이 등록되어 있는지 확인
        /// </summary>
        /// <returns>등록 여부</returns>
        public bool IsTaskScheduled()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Query /TN \"{TaskName}\"",
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
        /// 등록된 작업의 정보 가져오기
        /// </summary>
        /// <returns>작업 정보 문자열</returns>
        public string GetTaskInfo()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Query /TN \"{TaskName}\" /FO LIST /V",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null) return string.Empty;

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    return process.ExitCode == 0 ? output : string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}

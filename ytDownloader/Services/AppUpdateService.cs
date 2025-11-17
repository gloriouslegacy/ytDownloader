using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;

namespace ytDownloader.Services
{
    /// <summary>
    /// 업데이트 확인 결과 데이터
    /// </summary>
    public class UpdateCheckResult
    {
        public bool UpdateAvailable { get; set; }
        public string LatestVersion { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public bool IsPrerelease { get; set; }
        public bool IsInstalledVersion { get; set; }
        public string AssetName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 앱 자체 업데이트 서비스
    /// </summary>
    public class AppUpdateService
    {
        /// <summary>
        /// 로그 메시지 출력 이벤트
        /// </summary>
        public event Action<string>? LogMessage;

        /// <summary>
        /// 업데이트 확인
        /// </summary>
        public async Task<UpdateCheckResult?> CheckForUpdateAsync()
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ytDownloader/1.0");

                var response = await httpClient.GetAsync("https://api.github.com/repos/gloriouslegacy/ytDownloader/releases");
                if (!response.IsSuccessStatusCode)
                {
                    LogMessage?.Invoke($"❌ 업데이트 확인 실패: {response.StatusCode}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var releases = JArray.Parse(json);

                if (releases == null || releases.Count == 0)
                {
                    LogMessage?.Invoke("ℹ️ 첫 버전입니다. 아직 등록된 릴리스가 없습니다.");
                    return null;
                }

                var latest = releases[0];
                string latestTag = latest["tag_name"]?.ToString() ?? "";
                bool isPre = latest["prerelease"]?.ToObject<bool>() ?? false;

                // 현재 실행 중인 버전
                string currentVersionStr = Assembly
                    .GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? "0.0.0";

                // '+' 뒤 빌드 메타데이터 제거
                int plusIdx = currentVersionStr.IndexOf('+');
                if (plusIdx >= 0)
                    currentVersionStr = currentVersionStr.Substring(0, plusIdx);

                // GitHub 태그에서 'v' 제거
                string latestTagClean = latestTag.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                    ? latestTag.Substring(1)
                    : latestTag;

                // 문자열을 Version 객체로 변환
                Version? currentV, latestV;
                if (!Version.TryParse(currentVersionStr, out currentV))
                    currentV = new Version(0, 0, 0);

                if (!Version.TryParse(latestTagClean, out latestV))
                    latestV = new Version(0, 0, 0);

                LogMessage?.Invoke($"[INFO] 현재 버전: {currentV}");
                LogMessage?.Invoke($"[INFO] 최신 버전: {latestV}");

                if (currentV < latestV)
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string installDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName) ?? baseDir;
                    bool isInstalledVersion = installDir.Contains(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ytDownloader", "app"));

                    string targetAssetName = isInstalledVersion ? "ytDownloader-setup.exe" : "ytdownloader.zip";

                    var updateAsset = latest["assets"]?
                        .FirstOrDefault(a =>
                        {
                            string assetName = a["name"]?.ToString() ?? "";
                            return string.Equals(assetName, targetAssetName, StringComparison.OrdinalIgnoreCase);
                        });

                    if (updateAsset != null)
                    {
                        string assetUrl = updateAsset["browser_download_url"]?.ToString() ?? "";
                        string assetName = updateAsset["name"]?.ToString() ?? "";

                        LogMessage?.Invoke($"[INFO] 설치형 여부: {isInstalledVersion}");
                        LogMessage?.Invoke($"[INFO] 선택된 에셋: {assetName}");
                        LogMessage?.Invoke($"[INFO] 다운로드 URL: {assetUrl}");

                        return new UpdateCheckResult
                        {
                            UpdateAvailable = true,
                            LatestVersion = latestTag,
                            DownloadUrl = assetUrl,
                            IsPrerelease = isPre,
                            IsInstalledVersion = isInstalledVersion,
                            AssetName = assetName
                        };
                    }
                    else
                    {
                        LogMessage?.Invoke($"❌ 업데이트 파일을 찾을 수 없습니다: {targetAssetName}");
                        LogMessage?.Invoke("[INFO] 사용 가능한 에셋들:");
                        foreach (var asset in latest["assets"] ?? new JArray())
                        {
                            LogMessage?.Invoke($"[INFO]   - {asset["name"]}");
                        }
                        return null;
                    }
                }
                else
                {
                    LogMessage?.Invoke("✅ 최신 버전을 사용 중입니다.");
                    return new UpdateCheckResult { UpdateAvailable = false };
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"❌ 업데이트 확인 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 업데이트 실행
        /// </summary>
        public async Task<bool> RunUpdateAsync(UpdateCheckResult updateInfo)
        {
            string downloadPath = updateInfo.IsInstalledVersion
                ? Path.Combine(Path.GetTempPath(), "ytDownloader-setup.exe")
                : Path.Combine(Path.GetTempPath(), "ytDownloader_update.zip");

            try
            {
                using var httpClient = new HttpClient();

                LogMessage?.Invoke("[INFO] 업데이트 파일 다운로드 시작");
                using (var response = await httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    await using var fs = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fs);
                }
                LogMessage?.Invoke($"[INFO] 다운로드 완료: {downloadPath}");

                if (updateInfo.IsInstalledVersion)
                {
                    // 설치형: setup.exe 실행
                    return await RunSetupInstallerAsync(downloadPath);
                }
                else
                {
                    // 포터블: Updater.exe 사용
                    return await RunPortableUpdateAsync(downloadPath);
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke("[ERROR] 다운로드 실패: " + ex);
                return false;
            }
        }

        /// <summary>
        /// 설치형 업데이트 (setup.exe 실행)
        /// </summary>
        private async Task<bool> RunSetupInstallerAsync(string setupPath)
        {
            try
            {
                LogMessage?.Invoke("[INFO] 설치 프로그램 실행 중...");

                var psi = new ProcessStartInfo
                {
                    FileName = setupPath,
                    UseShellExecute = true,
                    Arguments = "/VERYSILENT /CLOSEAPPLICATIONS"
                };

                Process.Start(psi);
                LogMessage?.Invoke("[INFO] 설치 프로그램이 실행되었습니다. 현재 앱을 종료합니다.");

                await Task.Delay(1000);
                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[ERROR] 설치 실행 실패: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 포터블 업데이트 (Updater.exe 사용)
        /// </summary>
        private async Task<bool> RunPortableUpdateAsync(string zipPath)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string updaterPath = Path.Combine(baseDir, "updater", "Updater.exe");

            if (!File.Exists(updaterPath))
                updaterPath = Path.Combine(baseDir, "Updater.exe");

            string targetExe = Process.GetCurrentProcess().MainModule!.FileName;
            targetExe = Path.GetFullPath(targetExe).Trim('"');
            string installDir = Path.GetDirectoryName(targetExe) ?? baseDir;
            installDir = Path.GetFullPath(installDir).TrimEnd('\\', '/').Trim('"');

            LogMessage?.Invoke("[INFO] Updater 실행 준비");
            LogMessage?.Invoke($"[INFO] baseDir     = '{baseDir}'");
            LogMessage?.Invoke($"[INFO] updaterPath = '{updaterPath}'");
            LogMessage?.Invoke($"[INFO] installDir  = '{installDir}'");
            LogMessage?.Invoke($"[INFO] targetExe   = '{targetExe}'");

            if (!File.Exists(updaterPath))
            {
                LogMessage?.Invoke("[ERROR] Updater.exe를 찾을 수 없습니다.");
                LogMessage?.Invoke($"[ERROR] 시도 경로 1: {Path.Combine(baseDir, "updater", "Updater.exe")}");
                LogMessage?.Invoke($"[ERROR] 시도 경로 2: {Path.Combine(baseDir, "Updater.exe")}");
                return false;
            }

            try
            {
                string workingDir = Path.GetDirectoryName(updaterPath) ?? baseDir;
                string arguments = $"\"{zipPath}\" \"{installDir}\" \"{targetExe}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Normal,
                    WorkingDirectory = workingDir
                };

                LogMessage?.Invoke($"[INFO] Updater 실행: {psi.FileName} {psi.Arguments}");
                var process = Process.Start(psi);
                if (process == null)
                {
                    LogMessage?.Invoke("[ERROR] Updater 프로세스 시작 실패");
                    return false;
                }

                LogMessage?.Invoke("[INFO] Updater가 실행되었습니다. 현재 앱을 종료합니다.");
                await Task.Delay(1000);
                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke("[ERROR] Updater 실행 실패: " + ex);
                return false;
            }
        }
    }
}

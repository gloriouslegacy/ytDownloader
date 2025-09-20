using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using System.Text;
using System.Diagnostics;

namespace Updater
{
    public partial class App : Application
    {
        private readonly string logFile =
            Path.Combine(Path.GetTempPath(), "ytDownloader_updater.log");

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string debugLog = Path.Combine(Path.GetTempPath(), "updater_debug.log");
            try
            {
                File.AppendAllText(debugLog, $"[INFO] [{DateTime.Now}] 시작됨\n", Encoding.UTF8);
                File.AppendAllText(debugLog, $"[INFO] 전체 인자 문자열: {string.Join(" ", e.Args)}\n", Encoding.UTF8);
                File.AppendAllText(debugLog, $"[INFO] 인자 개수: {e.Args.Length}\n", Encoding.UTF8);

                for (int i = 0; i < e.Args.Length; i++)
                {
                    File.AppendAllText(debugLog, $"[INFO] 인자[{i}]: '{e.Args[i]}'\n", Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(debugLog, $"[ERROR] 로그 작성 실패: {ex.Message}\n", Encoding.UTF8);
            }

            var window = new UpdateWindow();
            MainWindow = window;
            window.Show();

            if (e.Args.Length < 3)
            {
                string errorMsg = $"잘못된 인자 개수입니다. 예상: 3개, 실제: {e.Args.Length}개";
                File.AppendAllText(debugLog, $"[ERROR] {errorMsg}\n", Encoding.UTF8);
                File.AppendAllText(debugLog, "[ERROR] 필요한 인자: <zipPath> <installDir> <targetExe>\n", Encoding.UTF8);

                window.UpdateStatus(errorMsg);
                Task.Delay(5000).ContinueWith(_ => Shutdown());
                return;
            }

            string zipPath = e.Args[0];
            string installDir = e.Args[1];
            string targetExe = e.Args[2];

            File.AppendAllText(debugLog, $"[INFO] 파싱된 인자들:\n", Encoding.UTF8);
            File.AppendAllText(debugLog, $"[INFO]   zipPath    = '{zipPath}'\n", Encoding.UTF8);
            File.AppendAllText(debugLog, $"[INFO]   installDir = '{installDir}'\n", Encoding.UTF8);
            File.AppendAllText(debugLog, $"[INFO]   targetExe  = '{targetExe}'\n", Encoding.UTF8);

            Task.Run(() => RunUpdaterAsync(zipPath, installDir, targetExe, window));
        }

        private async Task<bool> TryDeleteFileWithRetry(string filePath, int maxRetries = 5)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        File.AppendAllText(logFile, $"[INFO] 파일 삭제 성공 (시도 {i + 1}/{maxRetries}): {filePath}\n", Encoding.UTF8);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    File.AppendAllText(logFile, $"[WARNING] 파일 삭제 실패 (시도 {i + 1}/{maxRetries}): {ex.Message}\n", Encoding.UTF8);

                    if (i < maxRetries - 1)
                    {
                        await Task.Delay(1000 * (i + 1)); // 점진적 지연: 1초, 2초, 3초...

                        // 프로세스 종료 대기
                        await WaitForProcessesToClose(filePath);
                    }
                }
            }
            return false;
        }

        private async Task WaitForProcessesToClose(string filePath)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                var processes = Process.GetProcessesByName(fileName);

                foreach (var proc in processes)
                {
                    try
                    {
                        File.AppendAllText(logFile, $"[INFO] 프로세스 대기 중: {proc.ProcessName} (PID: {proc.Id})\n", Encoding.UTF8);

                        if (!proc.WaitForExit(2000)) // 2초 대기
                        {
                            File.AppendAllText(logFile, $"[WARNING] 프로세스가 종료되지 않음: {proc.ProcessName}\n", Encoding.UTF8);
                        }
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText(logFile, $"[WARNING] 프로세스 대기 오류: {ex.Message}\n", Encoding.UTF8);
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(logFile, $"[WARNING] 프로세스 확인 오류: {ex.Message}\n", Encoding.UTF8);
            }
        }

        private async Task RunUpdaterAsync(string zipPath, string installDir, string targetExe, UpdateWindow window)
        {
            try
            {
                File.AppendAllText(logFile, $"[INFO] Updater 시작: {DateTime.Now}\n", Encoding.UTF8);
                File.AppendAllText(logFile, $"[INFO] zipPath    = {zipPath}\n", Encoding.UTF8);
                File.AppendAllText(logFile, $"[INFO] installDir = {installDir}\n", Encoding.UTF8);
                File.AppendAllText(logFile, $"[INFO] targetExe  = {targetExe}\n", Encoding.UTF8);

                if (!File.Exists(zipPath))
                {
                    string errorMsg = "업데이트 ZIP 파일이 존재하지 않습니다.";
                    window.Dispatcher.Invoke(() => window.UpdateStatus(errorMsg));
                    File.AppendAllText(logFile, $"[ERROR] {errorMsg}: {zipPath}\n", Encoding.UTF8);
                    return;
                }

                window.Dispatcher.Invoke(() => window.UpdateStatus("업데이트 파일 압축 해제 중..."));

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    if (archive.Entries.Count == 0)
                    {
                        string errorMsg = "ZIP 파일이 비어 있습니다.";
                        window.Dispatcher.Invoke(() => window.UpdateStatus(errorMsg));
                        File.AppendAllText(logFile, $"[ERROR] {errorMsg}\n", Encoding.UTF8);
                        return;
                    }

                    File.AppendAllText(logFile, $"[INFO] ZIP 엔트리 개수: {archive.Entries.Count}\n", Encoding.UTF8);

                    int processedCount = 0;
                    foreach (var entry in archive.Entries)
                    {
                        try
                        {
                            string full = entry.FullName.Replace('\\', '/');

                            // 제외 규칙: tools/, updater/, Updater.exe
                            if (full.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) ||
                                full.StartsWith("updater/", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(Path.GetFileName(full), "Updater.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                File.AppendAllText(logFile, $"[INFO] Skipped: {entry.FullName}\n", Encoding.UTF8);
                                continue;
                            }

                            string destinationPath = Path.Combine(installDir, entry.FullName);
                            File.AppendAllText(logFile, $"[INFO] 처리 중: {entry.FullName} -> {destinationPath}\n", Encoding.UTF8);

                            // 폴더인 경우
                            if (string.IsNullOrEmpty(entry.Name))
                            {
                                if (!Directory.Exists(destinationPath))
                                {
                                    Directory.CreateDirectory(destinationPath);
                                    File.AppendAllText(logFile, $"[INFO] 폴더 생성: {destinationPath}\n", Encoding.UTF8);
                                }
                                continue;
                            }

                            // 파일인 경우
                            string? directoryPath = Path.GetDirectoryName(destinationPath);
                            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                            {
                                Directory.CreateDirectory(directoryPath);
                                File.AppendAllText(logFile, $"[INFO] 상위 폴더 생성: {directoryPath}\n", Encoding.UTF8);
                            }

                            // 기존 파일 삭제 (재시도 로직 적용)
                            if (File.Exists(destinationPath))
                            {
                                bool deleted = await TryDeleteFileWithRetry(destinationPath);
                                if (!deleted)
                                {
                                    File.AppendAllText(logFile, $"[ERROR] 파일 삭제 최종 실패, 건너뜀: {destinationPath}\n", Encoding.UTF8);
                                    continue;
                                }
                            }

                            // 파일 압축 해제
                            entry.ExtractToFile(destinationPath, true);
                            File.AppendAllText(logFile, $"[INFO] 추출 완료: {entry.FullName}\n", Encoding.UTF8);
                            processedCount++;

                            // UI 업데이트
                            window.Dispatcher.Invoke(() =>
                                window.UpdateStatus($"파일 처리 중... ({processedCount}/{archive.Entries.Count})"));
                        }
                        catch (Exception entryEx)
                        {
                            File.AppendAllText(logFile, $"[ERROR] 엔트리 처리 실패 ({entry.FullName}): {entryEx}\n", Encoding.UTF8);
                        }
                    }
                }

                window.Dispatcher.Invoke(() => window.UpdateStatus("업데이트 완료. 프로그램을 다시 시작합니다."));
                File.AppendAllText(logFile, "[INFO] 업데이트 성공\n", Encoding.UTF8);

                // 잠시 대기 후 원래 실행파일 다시 실행
                await Task.Delay(2000);

                if (File.Exists(targetExe))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = targetExe,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(targetExe)
                    };

                    Process.Start(startInfo);
                    File.AppendAllText(logFile, $"[INFO] 프로그램 재시작: {targetExe}\n", Encoding.UTF8);
                }
                else
                {
                    File.AppendAllText(logFile, $"[ERROR] 대상 실행파일이 존재하지 않음: {targetExe}\n", Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"오류 발생: {ex.Message}";
                window.Dispatcher.Invoke(() => window.UpdateStatus(errorMsg));
                File.AppendAllText(logFile, $"[ERROR] 예외: {ex}\n", Encoding.UTF8);
                File.AppendAllText(logFile, $"[ERROR] StackTrace: {ex.StackTrace}\n", Encoding.UTF8);
            }
            finally
            {
                // ZIP 파일 정리
                try
                {
                    if (File.Exists(zipPath))
                    {
                        File.Delete(zipPath);
                        File.AppendAllText(logFile, "[INFO] 임시 ZIP 파일 삭제 완료\n", Encoding.UTF8);
                    }
                }
                catch (Exception deleteEx)
                {
                    File.AppendAllText(logFile, $"[WARNING] ZIP 파일 삭제 실패: {deleteEx.Message}\n", Encoding.UTF8);
                }

                await Task.Delay(5000);
                Dispatcher.Invoke(() => Shutdown());
            }
        }
    }
}
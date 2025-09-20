using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace Updater
{
    internal class Program
    {
        private static readonly string LogFile =
            Path.Combine(Path.GetTempPath(), "ytDownloader_updater.log");

        private static void Log(string msg, bool isError = false)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}";
            File.AppendAllText(LogFile, line + Environment.NewLine);

            if (isError) Console.Error.WriteLine(line);
            else Console.WriteLine(line);
        }

        static int Main(string[] args)
        {
            try
            {
                // 무조건 진입 로그
                Log("=== Updater 진입 ===");
                Log($"args.Length = {args.Length}, args = {string.Join(" | ", args)}");

                if (args.Length < 3)
                {
                    Log("❌ 인자가 부족합니다. (Usage: Updater <zipPath> <installDir> <targetExe>)", true);
                    return 1;
                }

                string zipPath = args[0];
                string installDir = args[1];
                string targetExe = args[2];

                Log($"zipPath   = {zipPath}");
                Log($"installDir= {installDir}");
                Log($"targetExe = {targetExe}");

                // 1) ZIP 파일 존재 확인
                if (!File.Exists(zipPath))
                {
                    Log("❌ ZIP 파일이 존재하지 않습니다.", true);
                    return 1;
                }

                // 2) ZIP 유효성 검사
                Log("📦 ZIP 유효성 검사...");
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    if (archive.Entries.Count == 0)
                    {
                        Log("❌ ZIP 파일이 비어 있습니다.", true);
                        return 1;
                    }
                }
                Log("✅ ZIP 유효성 검사 완료");

                // 3) 기존 프로세스 종료
                string procName = Path.GetFileNameWithoutExtension(targetExe);
                foreach (var p in Process.GetProcessesByName(procName))
                {
                    try
                    {
                        Log($"⏳ 기존 프로세스 종료 대기: {p.ProcessName} (PID {p.Id})");
                        p.Kill();
                        p.WaitForExit();
                        Log("✅ 기존 프로세스 종료됨");
                    }
                    catch (Exception ex)
                    {
                        Log("⚠ 기존 프로세스 종료 실패: " + ex.Message, true);
                    }
                }

                // 4) 압축 해제 (tools 제외)
                Log("📂 압축 해제 시작...");
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        var name = entry.FullName.Replace('\\', '/');
                        if (name.StartsWith("tools/", StringComparison.OrdinalIgnoreCase))
                        {
                            Log($"➡️  제외됨: {entry.FullName}");
                            continue;
                        }

                        string dest = Path.Combine(installDir, entry.FullName);

                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(dest);
                            continue;
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        entry.ExtractToFile(dest, true);
                        Log($"📄 교체됨: {entry.FullName}");
                    }
                }
                Log("✅ 압축 해제 완료");

                // 5) 새 프로그램 실행
                Log("🚀 새 버전 실행...");
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExe,
                    WorkingDirectory = installDir,
                    UseShellExecute = true,
                    Verb = "runas" // 관리자 권한 요청
                });

                Log("🎉 업데이트 완료 - 새 버전 실행됨");
                return 0;
            }
            catch (Exception ex)
            {
                Log("❌ 업데이트 중 예외 발생: " + ex, true);
                return 1;
            }
            finally
            {
                Log("=== Updater 종료 ===");
            }
        }
    }
}

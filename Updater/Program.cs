using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace Updater
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length < 3)
                return; // 인자 부족 → 조용히 종료

            string zipPath = args[0];
            string installDir = args[1];
            string targetExe = args[2];

            // 로그 파일은 %TEMP% 폴더에 저장
            string logFile = Path.Combine(Path.GetTempPath(), "ytDownloader_updater.log");

            try
            {
                // 📌 대상 exe가 완전히 종료될 때까지 대기 (최대 10초)
                bool unlocked = false;
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        using (FileStream fs = new FileStream(targetExe, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            unlocked = true;
                            break;
                        }
                    }
                    catch
                    {
                        Thread.Sleep(1000);
                    }
                }

                if (!unlocked)
                    throw new Exception("대상 프로그램이 아직 종료되지 않아 업데이트를 진행할 수 없습니다.");

                // 📌 압축 해제 (tools 폴더 제외)
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        // tools 폴더는 무시
                        if (entry.FullName.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.StartsWith("tools\\", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string destinationPath = Path.Combine(installDir, entry.FullName);

                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            // 디렉토리인 경우
                            Directory.CreateDirectory(destinationPath);
                            continue;
                        }

                        // 상위 폴더 생성
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                        // 파일 덮어쓰기
                        entry.ExtractToFile(destinationPath, true);
                    }
                }

                // 📌 원래 프로그램 재실행
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExe,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                try
                {
                    // 매번 새 파일로 덮어쓰기 (에러 메시지 + 스택 트레이스 기록)
                    File.WriteAllText(logFile,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 업데이트 실패\n" +
                        $"메시지: {ex.Message}\n" +
                        $"스택 트레이스:\n{ex.StackTrace}\n");
                }
                catch
                {
                    // 로그 작성 실패 시에도 아무 동작 안 함 (Silent)
                }
            }
        }
    }
}

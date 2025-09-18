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

            Thread.Sleep(2000); // 메인 프로그램이 완전히 종료되도록 잠시 대기

            try
            {
                // 압축 해제 (덮어쓰기)
                ZipFile.ExtractToDirectory(zipPath, installDir, true);

                // 원래 프로그램 재실행
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

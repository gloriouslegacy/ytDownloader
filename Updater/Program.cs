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
                return; // 인자 부족 → 종료

            string zipPath = args[0];
            string installDir = args[1];
            string targetExe = args[2];

            string logFile = Path.Combine(Path.GetTempPath(), "ytDownloader_updater.log");

            try
            {
                Thread.Sleep(2000); // 메인 프로그램 종료 대기

                // 🔥 ZIP 유효성 검사
                if (!File.Exists(zipPath))
                    throw new FileNotFoundException("업데이트 ZIP 파일이 존재하지 않습니다.", zipPath);

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    if (archive.Entries.Count == 0)
                        throw new InvalidDataException("ZIP 파일이 비어 있습니다. (Entries.Count == 0)");

                    foreach (var entry in archive.Entries)
                    {
                        // tools 폴더 무시
                        if (entry.FullName.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.StartsWith("tools\\", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string destinationPath = Path.Combine(installDir, entry.FullName);

                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(destinationPath);
                            continue;
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                        entry.ExtractToFile(destinationPath, true);
                    }
                }

                // ✅ 로그 기록
                File.AppendAllText(logFile,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 업데이트 성공: {zipPath} → {installDir}{Environment.NewLine}");

                // 메인 프로그램 재실행
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
                    File.AppendAllText(logFile,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 업데이트 실패\n" +
                        $"ZIP: {zipPath}\n설치경로: {installDir}\n에러: {ex}\n");
                }
                catch
                {
                    // 로그 작성 실패 시 무시
                }
            }
        }
    }
}

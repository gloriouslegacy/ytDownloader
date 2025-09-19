using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace Updater
{
    internal class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: Updater <zipPath> <installDir> <targetExe>");
                return 1;
            }

            string zipPath = args[0];
            string installDir = args[1];
            string targetExe = args[2];

            try
            {
                Console.WriteLine("[Updater] 업데이트 시작...");
                if (!File.Exists(zipPath))
                {
                    Console.Error.WriteLine("[Updater] ZIP 파일이 존재하지 않습니다: " + zipPath);
                    return 1;
                }

                // ZIP 유효성 검사
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    if (archive.Entries.Count == 0)
                    {
                        Console.Error.WriteLine("[Updater] ZIP 파일이 비어 있습니다.");
                        return 1;
                    }
                }

                // 기존 파일들 덮어쓰기
                ZipFile.ExtractToDirectory(zipPath, installDir, overwriteFiles: true);
                Console.WriteLine("[Updater] 압축 해제 완료");

                // 프로그램 다시 실행
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExe,
                    WorkingDirectory = installDir
                });

                Console.WriteLine("[Updater] 업데이트가 완료되었습니다.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[Updater] 업데이트 실패: " + ex.Message);
                return 1;
            }
        }
    }
}

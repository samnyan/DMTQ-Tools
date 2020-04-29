using System;
using System.IO;
using LZ4;

namespace lz4_tool
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("DMTQ Tools - lz4 tool");
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: lz4_tool.exe [-d <folder_path>] [-c <folder_path>] [files_path]");
                Console.WriteLine("-d <folder_path> : Decompress all file with .lz4 extension in that folder, will overwrite all file");
                Console.WriteLine("-c <folder_path> : Compress all file without .lz4 extension in that folder, will overwrite all file");
                Console.WriteLine("files_path : Multiple files, auto detect file extension to decompress or compress to the same folder");
                Console.ReadLine();
                return;
            }
            if(args.Length == 1)
            {
                // Single file
                AutoDetect(args[0]);
                Console.WriteLine("Done, press enter to continue");
                Console.ReadLine();
                return;
            }
            int decompressIndex = -1;
            int compressIndex = -1;
            for (int i=0; i < args.Length; i++)
            {
                if(args[i].Equals("-d"))
                {
                    decompressIndex = i;
                }
                if (args[i].Equals("-c"))
                {
                    compressIndex = i;
                }
            }

            // Do Folder
            if(decompressIndex > -1)
            {
                foreach (string file in Directory.EnumerateFiles(args[decompressIndex + 1], "*.lz4"))
                {
                    Decompress(file);
                }
            }
            if (compressIndex > -1)
            {
                foreach (string file in Directory.EnumerateFiles(args[compressIndex + 1]))
                {
                    FileInfo info = new FileInfo(file);
                    if (!info.Extension.Equals(".lz4", StringComparison.OrdinalIgnoreCase))
                    {
                        Compress(file);
                    }
                }
            }
            Console.WriteLine("Done, press enter to continue");
            Console.ReadLine();
        }

        public static void AutoDetect(string file)
        {
            FileInfo info = new FileInfo(file);
            if(info.Extension.Equals(".lz4", StringComparison.OrdinalIgnoreCase))
            {
                Decompress(file);
            }
            else
            {
                Compress(file);
            }
        }

        public static void Decompress(string file)
        {
            Console.WriteLine("Decompressing " + file);
            using (var fileStream = new FileStream(file, FileMode.Open))
            using (var outFileStream = new FileStream(file.Replace(".lz4", ""), FileMode.Create))
            using (var lz4Stream = new LZ4Stream(fileStream, LZ4StreamMode.Decompress))
            {
                lz4Stream.CopyTo(outFileStream);
                outFileStream.Flush();
            }
        }

        public static void Compress(string file)
        {
            Console.WriteLine("Compressing " + file);
            using (var fileStream = new FileStream(file, FileMode.Open))
            using (var outFileStream = new FileStream(file + ".lz4", FileMode.Create))
            using (var lz4Stream = new LZ4Stream(outFileStream, LZ4StreamMode.Compress))
            {
                fileStream.CopyTo(lz4Stream);
                outFileStream.Flush();
            }
        }
    }
}

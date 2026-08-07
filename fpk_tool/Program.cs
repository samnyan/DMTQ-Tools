using System;
using System.IO;
using K4os.Compression.LZ4.Legacy;

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

            // 遍历所有传入的参数，支持混合使用指令和直接拖拽多个文件
            for (int i = 0; i < args.Length; i++)
            {
                // 处理解压文件夹指令
                if (args[i].Equals("-d", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length && Directory.Exists(args[i + 1]))
                    {
                        foreach (string file in Directory.EnumerateFiles(args[i + 1], "*.lz4"))
                        {
                            Decompress(file);
                        }
                        i++; // 跳过下一个参数（因为它是文件夹路径）
                    }
                    else
                    {
                        Console.WriteLine("Invalid folder path for -d");
                    }
                }
                // 处理压缩文件夹指令
                else if (args[i].Equals("-c", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length && Directory.Exists(args[i + 1]))
                    {
                        foreach (string file in Directory.EnumerateFiles(args[i + 1]))
                        {
                            FileInfo info = new FileInfo(file);
                            if (!info.Extension.Equals(".lz4", StringComparison.OrdinalIgnoreCase))
                            {
                                Compress(file);
                            }
                        }
                        i++; // 跳过下一个参数（因为它是文件夹路径）
                    }
                    else
                    {
                        Console.WriteLine("Invalid folder path for -c");
                    }
                }
                // 处理直接传入的文件（支持批量多选拖拽）
                else
                {
                    if (File.Exists(args[i]))
                    {
                        AutoDetect(args[i]);
                    }
                    else if (Directory.Exists(args[i]))
                    {
                        Console.WriteLine($"Skipping directory '{args[i]}'. Please use -c or -d to process directories.");
                    }
                    else
                    {
                        Console.WriteLine($"File not found: {args[i]}");
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
            using (var lz4Stream = LZ4Legacy.Decode(fileStream, leaveOpen: false))
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
            using (var lz4Stream = LZ4Legacy.Encode(outFileStream, leaveOpen: false))
            {
                fileStream.CopyTo(lz4Stream);
                outFileStream.Flush();
            }
        }
    }
}

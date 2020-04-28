using System;
using System.Text;
using System.IO;

namespace fpk_tool
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length <= 0)
            {
                Console.WriteLine("DMTQ Tools - FPK Tool");
                Console.WriteLine("Drag a .fpk file to unpack, or drag a folder to repack it back to .fpk file");
                Console.ReadLine();
                return;
            }
            foreach (var path in args)
            {
                FileAttributes attr = File.GetAttributes(path);
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    Console.WriteLine("Repacking " + path);
                    // do pack
                    DirectoryInfo dir = new DirectoryInfo(path);
                    FileInfo[] files = dir.GetFiles();

                    using (FileStream fs = new FileStream(Path.Combine(dir.Parent.FullName, dir.Name+".fpk"), FileMode.Create))
                    using (MemoryStream fileInfo = new MemoryStream(100))
                    using (MemoryStream fileData = new MemoryStream(100))
                    {
                        fs.Write(new byte[] { 0x0, 0x10, 0x0, 0x0 }, 0, 4); // Header?
                        fs.Write(new byte[] { 0x3, 0x0, 0x0, 0x0 }, 0, 4); // Header?
                        fs.Write(new byte[] { 0x0, 0x0, 0x0, 0x0 }, 0, 4); // Pack Size
                        fs.Write(new byte[] { 0x0, 0x0, 0x0, 0x0 }, 0, 4); // Compress Pack Size
                        fs.Write(new byte[] { 0x0, 0x0, 0x0, 0x0 }, 0, 4); // Info Offset
                        fs.Write(BitConverter.GetBytes(files.Length), 0, 4); // Files Count
                        fs.Write(new byte[] { 0x1, 0x0, 0x0, 0x0 }, 0, 4); // Unknown header


                        long fileOffset = fs.Position;
                        foreach (FileInfo file in files)
                        {
                            Console.WriteLine("Adding file: " + file.Name);
                            // Write File Table
                            byte[] length = BitConverter.GetBytes(file.Length);
                            fileInfo.Write(BitConverter.GetBytes(fileOffset), 0, 4); // File offset
                            fileInfo.Write(length, 0, 4); // File length
                            fileInfo.Write(length, 0, 4); // Compress file length
                            string fileName = file.Name;

                            fileInfo.Write(BitConverter.GetBytes(fileName.Length), 0, 4); // Filename length

                            byte[] fileNameBytes = new byte[0x80];
                            byte[] fileNameTemp = Encoding.ASCII.GetBytes(fileName);
                            if (fileNameTemp.Length > 0x80)
                            {
                                Console.WriteLine("Warning: File name too long: " + fileName);
                            }
                            for (int i = 0; i < fileNameTemp.Length && i < 0x80; i++)
                            {
                                fileNameBytes[i] = fileNameTemp[i];
                            }
                            fileInfo.Write(fileNameBytes, 0, 0x80); // Filename

                            // Write File
                            fs.Seek(fileOffset, SeekOrigin.Begin);

                            using (FileStream inputfs = new FileStream(file.FullName, FileMode.Open))
                            {
                                inputfs.CopyTo(fs);
                            }
                            fileOffset += file.Length;
                        }

                        fs.Seek(fileOffset, SeekOrigin.Begin);
                        fileInfo.Seek(0, SeekOrigin.Begin);
                        fileInfo.CopyTo(fs);

                        // Write File Table Offset
                        fs.Seek(0x10, SeekOrigin.Begin);
                        fs.Write(BitConverter.GetBytes(fileOffset), 0, 4);

                        // Write Pack Size
                        fs.Seek(0x8, SeekOrigin.Begin);
                        fs.Write(BitConverter.GetBytes(fileOffset - 0x1C), 0, 4);
                        fs.Write(BitConverter.GetBytes(fileOffset - 0x1C), 0, 4);

                    }

                }
                else
                {
                    Console.WriteLine("Unpacking " + path);
                    // do unpack
                    FileInfo info = new FileInfo(path);
                    string outPath = Path.Combine(info.DirectoryName, Path.GetFileNameWithoutExtension(path));
                    Directory.CreateDirectory(outPath);
                    Console.WriteLine("Output path: " + outPath);

                    using (FileStream fs = new FileStream(path, FileMode.Open))
                    using (BinaryReader reader = new BinaryReader(fs))
                    {
                        fs.Seek(0x8, SeekOrigin.Begin);


                        long packSize = reader.ReadInt32();
                        long compressPackSize = reader.ReadInt32();
                        if (packSize != compressPackSize)
                        {
                            Console.WriteLine("Warning: This file may contains compressed file, which doesn't decompress automatically by this tool.");
                        }
                        long fileTableOffset = reader.ReadInt32();
                        long fileCount = reader.ReadInt32();
                        Console.WriteLine("FileTable Offset: " + fileTableOffset);
                        Console.WriteLine("File Count: " + fileCount);

                        long currentOffset = fileTableOffset;

                        for (int i = 0; i < fileCount; i++)
                        {
                            fs.Seek(currentOffset, SeekOrigin.Begin);
                            int fileOffset = reader.ReadInt32();
                            int size = reader.ReadInt32();
                            int compressSize = reader.ReadInt32();
                            int fileNameLength = reader.ReadInt32();
                            string fileName = new string(reader.ReadChars(fileNameLength));
                            fs.Seek(fileOffset, SeekOrigin.Begin);

                            byte[] file = new byte[size];
                            fs.Read(file, 0, size);


                            using (FileStream os = new FileStream(Path.Combine(outPath, fileName), FileMode.Create))
                            {
                                Console.WriteLine("Writing " + fileName);
                                os.Write(file, 0, size);
                            }
                            currentOffset += 0x90;
                        }
                    }
                }
            }
            
            Console.WriteLine("Done, Press enter to exit");
            Console.ReadLine();
            
        }
    }
}

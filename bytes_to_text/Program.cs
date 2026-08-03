using System;
using System.IO;
using System.Linq;
using System.Text;

namespace bytes_to_text
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("DMTQ Tools - bytes to text");
            Console.WriteLine("This tool can help you better understanding the bytes format.");
            Console.WriteLine("Usage: bytes_to_text.exe <anyfile>  - Convert from bytes to text interchange format.");
            Console.WriteLine("Usage: bytes_to_text.exe <filename>.txt  - Convert from the file create by this tool back to .bytes file.");
            Console.WriteLine("");
            Console.WriteLine(@"For the detailed explain please check the github repository");

            // 遍历传入的所有文件参数，支持拖拽多个文件进行批量处理
            foreach(string arg in args)
            {
                FileInfo file = new FileInfo(arg);
                
                // 根据文件扩展名判断：如果不是 .txt，则认为是二进制谱面文件，进行解包 (Decode)
                if(!file.Extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    // ==========================================
                    //               解码阶段 (Bytes -> Txt)
                    // ==========================================
                    using (FileStream ifs = new FileStream(file.FullName, FileMode.Open))
                    using (BinaryReader reader = new BinaryReader(ifs))
                    using (FileStream ofs = new FileStream(Path.Combine(file.DirectoryName, Path.GetFileNameWithoutExtension(file.Name) + ".txt"), FileMode.Create))
                    using (StreamWriter writer = new StreamWriter(ofs))
                    {
                        // 1. 读取文件最开头的两个重要偏移量
                        int header = reader.ReadInt32();      // 未知标识或文件头魔数
                        int infoOffset = reader.ReadInt32();  // 谱面元数据（头信息）所在的字节偏移量

                        // 2. 将流指针跳转到信息段开始处，读取基础属性
                        ifs.Seek(infoOffset, SeekOrigin.Begin);

                        int soundCount = reader.ReadInt16();            // 音效文件总数
                        int trackCount = reader.ReadInt16();            // 轨道总数
                        int positionsPerMeasure = reader.ReadInt16();   // 每小节的位置数 (通常代表谱面的解析精度，如 192)
                        float initialBpm = reader.ReadSingle();         // 初始 BPM
                        int endPosition = reader.ReadInt32();           // 谱面结束的绝对位置
                        int tagB = reader.ReadInt32();                  // 未知标签B (可能是时间戳或校验和)
                        int tagC = reader.ReadInt32();                  // 未知标签C (经查证，常与 endPosition 保持一致)
                        int totalCommandCount = reader.ReadInt32();     // 全局指令/音符总数
                        
                        // 3. 将这些基础信息写入 .txt 文件
                        writer.WriteLine("#SOUND_COUNT " + soundCount);
                        writer.WriteLine("#TRACK_COUNT " + trackCount);
                        writer.WriteLine("#POSITION_PER_MEASURE " + positionsPerMeasure);
                        writer.WriteLine("#BPM " + Math.Round((decimal)initialBpm, 2)); // 修正：保留正常10进制小数
                        writer.WriteLine("#END_POSITION " + endPosition);
                        writer.WriteLine("#TAGB " + tagB);
                        writer.WriteLine("#TAGC " + tagC);
                        writer.WriteLine("#TOTOAL_CMD_COUNT " + totalCommandCount);


                        // 4. 读取音效表 (WAV 表)
                        long currentOffset = 0x8; // 音效表从文件的 0x8 位置开始
                        for (int i = 0; i < soundCount; i++)
                        {
                            ifs.Seek(currentOffset, SeekOrigin.Begin);
                            int id = reader.ReadInt16();           // 读取音效 ID (2字节)
                            reader.ReadByte();                     // 跳过一个未知字节
                            char[] fileNameChars = reader.ReadChars(0x40); // 读取固定64字节长度的文件名
                            // 去除字符串末尾多余的空字符(\0)和空格
                            string fileName = new string(fileNameChars).Replace("\0", string.Empty).Trim();
                            
                            // 格式化输出为 16进制ID + 文件名
                            writer.WriteLine("#WAV" + id.ToString("X4") + " " + fileName);

                            currentOffset += 0x43; // 每条音效记录固定长 0x43 (67) 字节，指针往后推
                        }

                        // 5. 开始解析轨道和音符指令
                        writer.WriteLine("POSITION COMMAND PARAMETER");
                        int currentTrackCount = 0;
                        
                        // 当还没读到文件尾部(infoOffset)时，循环读取每个轨道
                        while (currentOffset < infoOffset)
                        {
                            int trackHeader = reader.ReadInt16();      // 轨道头部标识
                            char[] trackName = reader.ReadChars(0x3B); // 轨道名称 (通常为空)

                            int trackPosition = reader.ReadInt32();    // 轨道的起始位置
                            byte cmd = reader.ReadByte();              // 读取第一条指令类型
                            
                            if (cmd == 0x0) // 指令 0x0 代表 TRACK_START (轨道开始)
                            {
                                int shiftedNoteCount = reader.ReadInt32(); // 可能是带偏移的计数
                                int noteCount = reader.ReadInt32();        // 当前轨道包含的音符/指令数量
                                
                                Console.WriteLine("#" + trackPosition + " " + "TRACK_START " + currentTrackCount + " '" + new string(trackName).Replace("\0", string.Empty).Trim() + "' " + noteCount);
                                writer.WriteLine("#" + trackPosition + " " + "TRACK_START " + currentTrackCount + " '" + new string(trackName).Replace("\0", string.Empty).Trim() + "' " + noteCount);

                                currentOffset = ifs.Position;
                                
                                // 遍历当前轨道内的所有指令
                                for (int i = 0; i < noteCount; i++)
                                {
                                    int position = reader.ReadInt32(); // 指令所在的绝对位置 (时间轴)
                                    cmd = reader.ReadByte();           // 指令类型代码
                                    
                                    // 根据指令类型进行解析
                                    switch (cmd)
                                    {
                                        case 0x0: // 异常情况：上个轨道没结束就遇到了新的 Track Start
                                            {
                                                char[] temp = reader.ReadChars(0x8);
                                                Console.WriteLine("Warning: New track start before track end");
                                                writer.WriteLine("#" + trackPosition + " " + "TRACK_START " + currentTrackCount + " '" + new string(trackName).Replace("\0", string.Empty).Trim() + "' " + noteCount);
                                                break;
                                            }
                                        case 0x1: // NOTE (常规音符)
                                            {
                                                int soundIndex = reader.ReadInt16(); // 引用的音效ID
                                                int volume = reader.ReadByte();      // 音量
                                                int pan = reader.ReadByte();         // 声相 (左右声道平衡)
                                                int type = reader.ReadByte();        // 音符类型 (如普攻、长按等)
                                                int length = reader.ReadByte();      // 音符长度
                                                int unknown = reader.ReadInt16();    // 未知保留字
                                                writer.WriteLine(
                                                    "#" + position + " " +
                                                    "NOTE" + " " +
                                                    soundIndex.ToString("X4") + " " +
                                                    volume + " " +
                                                    pan + " " +
                                                    type + " " +
                                                    length + " " +
                                                    unknown);
                                                break;
                                            }
                                        case 0x2: // VOLUME (音量变化)
                                            {
                                                int volume = reader.ReadByte();
                                                int unknown1 = reader.ReadByte();
                                                int unknown2 = reader.ReadByte();
                                                int unknown3 = reader.ReadByte();
                                                int unknown4 = reader.ReadInt32();
                                                writer.WriteLine("#" + position + " " + "VOLUME" + " " + volume + " " + unknown1 + " " + unknown2 + " " + unknown3 + " " + unknown4);
                                                break;
                                            }
                                        case 0x3: // BPM_CHANGE (变速指令)
                                            {
                                                float bpm = reader.ReadSingle(); // 读出的是单精度浮点数
                                                int unknown = reader.ReadInt32();
                                                writer.WriteLine("#" + position + " " + "BPM_CHANGE" + " " + Math.Round((decimal)bpm, 2) + " " + unknown);
                                                break;
                                            }
                                        default: // 其他未知的自定义指令
                                            {
                                                long unknown1 = reader.ReadInt64();
                                                writer.WriteLine("#" + position + " " + cmd + " " + unknown1);
                                                break;
                                            }
                                    }
                                    currentOffset = ifs.Position; // 更新当前指针
                                }
                            }
                            currentTrackCount++;
                            currentOffset = ifs.Position;
                        }
                    }
                } else
                {
                    // ==========================================
                    //               编码阶段 (Txt -> Bytes)
                    // ==========================================
                    using (FileStream ifs = new FileStream(file.FullName, FileMode.Open))
                    using (StreamReader reader = new StreamReader(ifs))
                    using (FileStream ofs = new FileStream(Path.Combine(file.DirectoryName, Path.GetFileNameWithoutExtension(file.Name) + "_converted.bytes"), FileMode.Create))
                    using (BinaryWriter writer = new BinaryWriter(ofs))
                    
                    // 为了方便处理，程序使用三个内存流分别暂存不同区域的数据，最后拼接到一起
                    using (MemoryStream track = new MemoryStream(100))  // 轨道和指令区
                    using (MemoryStream sounds = new MemoryStream(100)) // 音效表区
                    using (MemoryStream info = new MemoryStream(100))   // 头信息区
                    {
                        string line;
                        int commandCounter = 0; // 当前轨道的指令计数器
                        int trackCounter = -1;  // 当前轨道的ID
                        long trackOffset = 0;   // 记录当前轨道头部的位置，用于之后回去写入 commandCounter
                        long commandPos = -1;   // 指令所在的时间位置
                        
                        int globalCommandCount = 0; // 全局指令总数
                        int globalEndPosition = 0;  // 全局结束位置

                        // 逐行解析 txt 文件
                        while ((line = reader.ReadLine())!=null)
                        {
                            // 忽略非 # 开头的注释或空行
                            if(line.StartsWith("#"))
                            {
                                string[] par = line.Split(' ');
                                
                                if(par[0].StartsWith("#WAV"))
                                {
                                    // ---- 解析音频表 ----
                                    // 截取 #WAV 后面的 16 进制 ID 并转为整数
                                    int id = Int32.Parse(par[0].Substring(4), System.Globalization.NumberStyles.HexNumber);
                                    sounds.Write(BitConverter.GetBytes(id), 0, 2);
                                    sounds.Write(new byte[]{ 0x0 }, 0, 1); // 未知填充字节

                                    byte[] fileNameBytes = new byte[0x40];
                                    
                                    // 修正：支持带空格的文件名。直接找第一个空格，后面全部作为文件名
                                    string fileName = line.Substring(line.IndexOf(' ') + 1);
                                    byte[] fileNameTemp = Encoding.ASCII.GetBytes(fileName);
                                    if (fileNameTemp.Length > 0x40)
                                    {
                                        Console.WriteLine("Warning: File name too long: " + fileName);
                                    }

                                    // 保证文件名强制只占 64 字节，不足的保留 0x00
                                    for (int i = 0; i < fileNameTemp.Length && i < 0x80; i++)
                                    {
                                        fileNameBytes[i] = fileNameTemp[i];
                                    }
                                    sounds.Write(fileNameBytes, 0, 0x40);
                                } 
                                // ---- 以下为写入头部基础信息的各种属性 ----
                                else if(par[0].StartsWith("#SOUND_COUNT"))
                                {
                                    info.Seek(0x0, SeekOrigin.Begin);
                                    int soundCount = Int32.Parse(par[1]);
                                    info.Write(BitConverter.GetBytes(soundCount), 0, 2);
                                }
                                else if (par[0].StartsWith("#TRACK_COUNT"))
                                {
                                    info.Seek(0x2, SeekOrigin.Begin);
                                    int trackCount = Int32.Parse(par[1]);
                                    info.Write(BitConverter.GetBytes(trackCount), 0, 2);
                                }
                                else if (par[0].StartsWith("#POSITION_PER_MEASURE"))
                                {
                                    info.Seek(0x4, SeekOrigin.Begin);
                                    int PPM = Int32.Parse(par[1]);
                                    info.Write(BitConverter.GetBytes(PPM), 0, 2);
                                }
                                else if (par[0].StartsWith("#BPM"))
                                {
                                    info.Seek(0x6, SeekOrigin.Begin);
                                    float BPM = Single.Parse(par[1]);
                                    info.Write(BitConverter.GetBytes(BPM), 0, 4);
                                }
                                else if (par[0].StartsWith("#END_POSITION"))
                                {
                                    info.Seek(0xA, SeekOrigin.Begin);
                                    int endPos = Int32.Parse(par[1]);
                                    info.Write(BitConverter.GetBytes(endPos), 0, 4);
                                    globalEndPosition = endPos; // 保存一份用于同步给 TAGC
                                }
                                else if (par[0].StartsWith("#TAGB"))
                                {
                                    info.Seek(0xE, SeekOrigin.Begin);
                                    int tagB = Int32.Parse(par[1]);
                                    info.Write(BitConverter.GetBytes(tagB), 0, 4);
                                }
                                else if (par[0].StartsWith("#TAGC"))
                                {
                                    info.Seek(0x12, SeekOrigin.Begin);
                                    int tagC = Int32.Parse(par[1]);
                                    info.Write(BitConverter.GetBytes(tagC), 0, 4);
                                }
                                else if (par[0].StartsWith("#TOTOAL_CMD_COUNT"))
                                {
                                    info.Seek(0x16, SeekOrigin.Begin);
                                    int cmdCount = Int32.Parse(par[1]);
                                    info.Write(BitConverter.GetBytes(cmdCount), 0, 4);
                                }
                                // ---- 以下为指令解析区 ----
                                // 判断行首是否以带数字的 # 开头 (例如 #432 NOTE ...)
                                else if (par[0].Length > 1 && long.TryParse(par[0].Substring(1), out commandPos))
                                {
                                    switch(par[1])
                                    {
                                        case "TRACK_START": // 0x0
                                            {
                                                long currentPos = track.Position;
                                                
                                                // 如果不是第一条轨道，需要将上一个轨道的总指令数写回其头部的占位符中
                                                if(commandCounter > 0)
                                                {
                                                    track.Seek(trackOffset, SeekOrigin.Begin); // 返回轨道头
                                                    track.Seek(4, SeekOrigin.Current);         // 跳过前4字节
                                                    track.Seek(1, SeekOrigin.Current);         // 再跳过1字节
                                                    track.Write(BitConverter.GetBytes(commandCounter << 4), 0, 4); // 写入带偏移的个数
                                                    track.Write(BitConverter.GetBytes(commandCounter), 0, 4);      // 写入真实的个数
                                                    
                                                    // 操作完后把流指针归位，继续写新轨道
                                                    track.Seek(currentPos, SeekOrigin.Begin);
                                                }
                                                // 计数器清零，开始新的轨道
                                                commandCounter = 0;
                                                trackCounter++;
                                                
                                                // 修正：从文本中动态读取轨道 ID，解决原版全部写死为 0 导致被覆盖瞬间结算的 bug
                                                short currentTrackId = short.Parse(par[2]);
                                                track.Write(BitConverter.GetBytes(currentTrackId), 0, 2);
                                                
                                                // 填充 0x3B 字节的空轨道名称
                                                byte[] emptyName = Enumerable.Repeat((byte)0x0, 0x3B).ToArray();
                                                track.Write(emptyName, 0, 0x3B);

                                                // 记录下此时的位置，用于下个轨道开始时回来填写本轨的指令总数
                                                trackOffset = track.Position;

                                                // 写入指令时间位置
                                                track.Write(BitConverter.GetBytes(commandPos), 0, 4);
                                                track.Write(new byte[] { 0 }, 0, 1); // cmd: 0
                                                track.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
                                                track.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
                                                break;
                                            }
                                        case "NOTE": // 0x1
                                            {
                                                commandCounter++;
                                                globalCommandCount++; // 更新全局指令数
                                                
                                                track.Write(BitConverter.GetBytes(commandPos), 0, 4);
                                                track.Write(new byte[] { 1 }, 0, 1); // cmd: 1
                                                
                                                // 将文本转回对应的各种字节长度
                                                int soundIndex = int.Parse(par[2], System.Globalization.NumberStyles.HexNumber); 
                                                int volume = int.Parse(par[3]);
                                                int pan = int.Parse(par[4]);
                                                int attribute = int.Parse(par[5]);
                                                int length = int.Parse(par[6]); 
                                                int unknown = int.Parse(par[7]);
                                                
                                                track.Write(BitConverter.GetBytes(soundIndex), 0, 2);
                                                track.Write(BitConverter.GetBytes(volume), 0, 1);
                                                track.Write(BitConverter.GetBytes(pan), 0, 1);
                                                track.Write(BitConverter.GetBytes(attribute), 0, 1);
                                                track.Write(BitConverter.GetBytes(length), 0, 1);
                                                track.Write(BitConverter.GetBytes(unknown), 0, 2);
                                                break;
                                            }
                                        case "VOLUME": // 0x2
                                            {
                                                commandCounter++;
                                                globalCommandCount++;
                                                
                                                track.Write(BitConverter.GetBytes(commandPos), 0, 4);
                                                track.Write(new byte[] { 2 }, 0, 1); // cmd: 2
                                                
                                                int vol = int.Parse(par[2]);
                                                int unknown1 = int.Parse(par[3]);
                                                int unknown2 = int.Parse(par[4]);
                                                int unknown3 = int.Parse(par[5]);
                                                int unknown4 = int.Parse(par[6]); 
                                                
                                                track.Write(BitConverter.GetBytes(vol), 0, 1);
                                                track.Write(BitConverter.GetBytes(unknown1), 0, 1);
                                                track.Write(BitConverter.GetBytes(unknown2), 0, 1);
                                                track.Write(BitConverter.GetBytes(unknown3), 0, 1);
                                                track.Write(BitConverter.GetBytes(unknown4), 0, 4);
                                                break;
                                            }
                                        case "BPM_CHANGE": // 0x3
                                            {
                                                commandCounter++;
                                                globalCommandCount++;
                                                
                                                track.Write(BitConverter.GetBytes(commandPos), 0, 4);
                                                track.Write(new byte[] { 3 }, 0, 1); // cmd: 3
                                                
                                                float bpm = Single.Parse(par[2]); 
                                                
                                                // 修正：兼容由旧版或有缺陷解析器产生的巨大 BPM 数值
                                                // 当遇到 >10000 的数字时，通常是因为工具错误地将 float 二进制值转成了 int
                                                // 这里重新将其对应的二进制 bit 转换回正确的 float
                                                if (bpm > 10000f) 
                                                {
                                                    int rawInt;
                                                    if (Int32.TryParse(par[2], out rawInt)) {
                                                        bpm = BitConverter.ToSingle(BitConverter.GetBytes(rawInt), 0);
                                                    }
                                                }
                                                
                                                track.Write(BitConverter.GetBytes(bpm), 0, 4);
                                                track.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
                                                break;
                                            }
                                        default: // 自定义/未知指令
                                            {
                                                int cmdCode;
                                                if(int.TryParse(par[1], out cmdCode))
                                                {
                                                    commandCounter++;
                                                    globalCommandCount++;
                                                    
                                                    long value = long.Parse(par[2]);
                                                    track.Write(BitConverter.GetBytes(commandPos), 0, 4);
                                                    track.Write(BitConverter.GetBytes(cmdCode), 0, 1);
                                                    track.Write(BitConverter.GetBytes(value), 0, 8);
                                                }
                                                break;
                                            }
                                    }
                                }
                            }
                        }
                        
                        // 修正：txt 遍历结束后，最后一条轨道由于没有后续的 TRACK_START 触发写回，
                        // 会导致最后一条轨道的 Note 数为 0。在这里额外触发一次写回。
                        if(commandCounter > 0)
                        {
                            long currentPos = track.Position;
                            track.Seek(trackOffset, SeekOrigin.Begin);
                            track.Seek(4, SeekOrigin.Current);
                            track.Seek(1, SeekOrigin.Current);
                            track.Write(BitConverter.GetBytes(commandCounter << 4), 0, 4);
                            track.Write(BitConverter.GetBytes(commandCounter), 0, 4);
                            track.Seek(currentPos, SeekOrigin.Begin);
                        }

                        // ==========================================
                        //          数据合并：将内存流写入文件
                        // ==========================================

                        // 1. 将音频表数据放在文件头 0x8 的位置
                        ofs.Seek(0x8, SeekOrigin.Begin);
                        sounds.Seek(0x0, SeekOrigin.Begin);
                        sounds.CopyTo(ofs);

                        // 2. 紧接着音频表，拼接所有的轨道和指令数据
                        track.Seek(0, SeekOrigin.Begin);
                        track.CopyTo(ofs);

                        // 3. 计算此时的位置，也就是头部信息 (Info) 需要放在哪
                        long infoOffset = ofs.Position;
                        
                        // 确保 Info 块至少有 0x1A 这么大
                        info.Seek(0, SeekOrigin.End);
                        while (info.Length < 0x1A) {
                            info.WriteByte(0x0);
                        }

                        // 修正：直接把计算出的全局 EndPosition 注入到 TAGC 位置 (0x12)
                        info.Seek(0x12, SeekOrigin.Begin);
                        info.Write(BitConverter.GetBytes(globalEndPosition), 0, 4);
                        
                        // 修正：直接把全局总指令数注入到 TOTOAL_CMD_COUNT 位置 (0x16)
                        info.Seek(0x16, SeekOrigin.Begin);
                        info.Write(BitConverter.GetBytes(globalCommandCount), 0, 4);

                        // 4. 将 Info 块拼接到文件末尾
                        info.Seek(0, SeekOrigin.Begin);
                        info.CopyTo(ofs);

                        // 5. 最后，返回文件头部 0x4 的位置，写入 Info 块的起始偏移量
                        ofs.Seek(0x4, SeekOrigin.Begin);
                        ofs.Write(BitConverter.GetBytes(infoOffset), 0, 4);
                    }
                }

            }
            Console.WriteLine("Done");
            Console.ReadLine();
        }
    }
}

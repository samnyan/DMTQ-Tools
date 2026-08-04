using System;
using System.IO;
using System.Collections.Generic;

namespace pt_to_text
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("DMTQ Tools - pt to text");
            Console.WriteLine("将 .pt 文件转换为文本 (离线解密版)");
            Console.WriteLine("警告：部分未知的命令参数将被忽略。");

            // 检查是否传入了文件参数
            if (args.Length == 0)
            {
                Console.WriteLine("请将 .pt 文件拖放到此可执行文件上。");
                Console.ReadLine();
                return;
            }

            // 遍历所有传入的文件
            foreach (string arg in args)
            {
                FileInfo file = new FileInfo(arg);
                using (FileStream ifs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
                using (BinaryReader reader = new BinaryReader(ifs))
                using (MemoryStream ms = new MemoryStream(100))
                using (BinaryReader msReader = new BinaryReader(ms, System.Text.Encoding.ASCII))
                using (FileStream ofs = new FileStream(Path.Combine(file.DirectoryName, Path.GetFileNameWithoutExtension(file.Name) + ".txt"), FileMode.Create))
                using (StreamWriter sw = new StreamWriter(ofs))
                {
                    Writer writer = new Writer(sw);
                    
                    // 读取前4个字节，检查文件魔数 (Magic Number) 是否为 "PTFF"
                    string header = new string(reader.ReadChars(0x4));
                    if (header != "PTFF")
                    {
                        Console.WriteLine($"[{file.Name}] 格式无效");
                        continue;
                    }

                    // 跳转到 0x18 (24字节) 处，即文件头结束、数据开始的地方
                    // 读取第一个标识字节，判断文件是否已被解密
                    ifs.Seek(0x18, SeekOrigin.Begin);
                    int firstId = reader.ReadByte(); 
                    
                    if (firstId != 1)
                    {
                        // 如果标识不为1，说明文件仍然是加密状态，调用离线解密算法
                        Console.WriteLine($"[{file.Name}] 检测到加密的 pt 文件，正在本地解密...");
                        ifs.Seek(0, SeekOrigin.Begin);
                        byte[] result = Decrypt(ifs);
                        ms.Write(result, 0, result.Length);
                    }
                    else
                    {
                        // 已经是明文，直接拷贝到内存流中进行后续解析
                        Console.WriteLine($"[{file.Name}] 检测到已解密的 pt 文件，直接读取...");
                        ifs.Seek(0, SeekOrigin.Begin);
                        ifs.CopyTo(ms);
                    }

                    // 再次检查确认第一个声音表的索引是否为 1
                    ms.Seek(0x18, SeekOrigin.Begin);
                    if (ms.ReadByte() != 0x1)
                    {
                        Console.WriteLine("警告：第一个声音表索引不是 1");
                    }

                    // 检查文件版本 (位于 0x4 处)
                    ms.Seek(0x4, SeekOrigin.Begin);
                    int version = msReader.ReadInt16();
                    bool isPadded = (version == 1); // 版本 1 的结构会有额外的字节填充 (Padding)

                    // 读取全局音频与谱面信息
                    int positionsPerMeasure = msReader.ReadInt16(); // 每小节的位置数
                    float initialBpm = msReader.ReadSingle();       // 初始 BPM (每分钟节拍数)
                    int trackCount = msReader.ReadInt16();          // 轨道总数
                    int endPostion = msReader.ReadInt32();          // 结束位置
                    int tagB = msReader.ReadInt32();                // 未知标签 B
                    int soundCount = msReader.ReadInt16();          // 音频总数

                    // 将基础信息写入 txt
                    writer.WriteLine("#SOUND_COUNT " + soundCount);
                    writer.WriteLine("#TRACK_COUNT " + trackCount);
                    writer.WriteLine("#POSITION_PER_MEASURE " + positionsPerMeasure);
                    writer.WriteLine("#BPM " + initialBpm.ToString());
                    writer.WriteLine("#END_POSITION " + endPostion);
                    writer.WriteLine("#TAGB " + tagB);

                    // 读取音频文件映射表 (Sound Table)
                    long currentOffset = 0x18; // 数据段起始偏移
                    for (int i = 0; i < soundCount; i++)
                    {
                        ms.Seek(currentOffset, SeekOrigin.Begin);
                        int id;
                        int flag;
                        
                        // 根据是否带有填充来决定读取 16 位还是 8 位
                        if (isPadded)
                        {
                            id = msReader.ReadInt16();
                            flag = msReader.ReadInt16();
                        }
                        else
                        {
                            id = msReader.ReadByte();
                            flag = msReader.ReadByte();
                        }
                        
                        // 读取 64 字节长度的 WAV 文件名
                        string fileName = new string(msReader.ReadChars(0x40));
                        int nullIndex = fileName.IndexOf('\0');
                        if (nullIndex >= 0)
                        {
                            fileName = fileName.Substring(0, nullIndex); // 截断 '\0' 之后的空白字符
                        }
                        writer.WriteLine("#WAV" + id.ToString("X4") + " " + fileName.Trim());

                        // 推进偏移量到下一条记录
                        if (isPadded)
                        {
                            currentOffset += 0x44;
                        }
                        else
                        {
                            currentOffset += 0x42;
                        }
                    }

                    writer.WriteLine("POSITION COMMAND PARAMETER");
                    int currentTrackCount = 0;
                    
                    // 循环读取所有的谱面指令，直到文件末尾
                    while (currentOffset < ms.Length)
                    {
                        int trackHeader = msReader.ReadInt32();

                        if (trackHeader == 1381259845) // 检查是否为 "EZTR" 轨道头 (对应的整数值为 1381259845)
                        {
                            // 读取轨道头部信息
                            msReader.ReadInt16();
                            string trackName = new string(msReader.ReadChars(0x40));
                            int ticks = msReader.ReadInt32();
                            int commandCount = msReader.ReadInt32();
                            if (isPadded) msReader.ReadInt16();
                            writer.WriteLine("#0 " + "TRACK_START " + currentTrackCount + " '' ");
                            currentTrackCount++;
                        }
                        else
                        {
                            ms.Seek(-4, SeekOrigin.Current); // 回退 4 字节，因为刚才读的不是 EZTR 而是普通位置信息
                            int position = msReader.ReadInt32();
                            int cmd = msReader.ReadByte(); // 指令类型
                            
                            switch (cmd)
                            {
                                case 0x1: // NOTE (按键/音符指令)
                                    {
                                        if (isPadded) ms.Seek(0x3, SeekOrigin.Current);
                                        int soundIndex;
                                        if (isPadded)
                                        {
                                            soundIndex = msReader.ReadInt16();
                                        }
                                        else
                                        {
                                            soundIndex = msReader.ReadByte();
                                        }
                                        int volume = msReader.ReadByte();
                                        int pan = msReader.ReadByte(); // 声相 (左右声道)
                                        int type = msReader.ReadByte(); // 音符类型 (如长条、单点)
                                        int length = msReader.ReadByte(); // 音符长度
                                        int unknown;
                                        if (isPadded)
                                        {
                                            unknown = msReader.ReadInt16();
                                        }
                                        else
                                        {
                                            unknown = msReader.ReadByte();
                                        }
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
                                case 0x2: // VOLUME (音量控制指令)
                                    {
                                        if (isPadded) ms.Seek(0x3, SeekOrigin.Current);
                                        int volume = msReader.ReadByte();
                                        int unknown1 = msReader.ReadByte();
                                        int unknown2 = msReader.ReadByte();
                                        int unknown3 = msReader.ReadByte();
                                        int unknown4;
                                        if (isPadded)
                                        {
                                            unknown4 = msReader.ReadInt32();
                                        }
                                        else
                                        {
                                            unknown4 = msReader.ReadInt16();
                                        }
                                        writer.WriteLine("#" + position + " " + "VOLUME" + " " + volume + " " + unknown1 + " " + unknown2 + " " + unknown3 + " " + unknown4);
                                        break;
                                    }
                                case 0x3: // BPM_CHANGE (BPM变速指令)
                                    {
                                        if (isPadded) ms.Seek(0x3, SeekOrigin.Current);
                                        float bpm = msReader.ReadSingle();
                                        if (isPadded)
                                        {
                                            ms.Seek(0x4, SeekOrigin.Current);
                                        }
                                        else
                                        {
                                            ms.Seek(0x2, SeekOrigin.Current);
                                        }

                                        writer.WriteLine("#" + position + " " + "BPM_CHANGE" + " " + bpm);
                                        break;
                                    }
                                case 0x4: // BEAT (节拍指令)
                                    {
                                        if (isPadded) ms.Seek(0x3, SeekOrigin.Current);
                                        int beat = msReader.ReadInt16();
                                        writer.WriteLine("#" + position + " " + cmd + " " + beat);
                                        if (isPadded)
                                        {
                                            ms.Seek(0x6, SeekOrigin.Current);
                                        }
                                        else
                                        {
                                            ms.Seek(0x4, SeekOrigin.Current);
                                        }

                                        break;
                                    }
                                default: // Other (其他未知指令)
                                    {
                                        if (isPadded) ms.Seek(0x3, SeekOrigin.Current);
                                        long unknown1 = msReader.ReadInt64();
                                        writer.WriteLine("#" + position + " " + cmd + " " + unknown1);
                                        break;
                                    }
                            }
                        }
                        currentOffset = ms.Position;
                    }
                }
            }
            Console.WriteLine("完成");
            Console.ReadLine();
        }

        // 调用离线解密器处理数据的封装方法
        private static byte[] Decrypt(Stream data)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                data.CopyTo(ms);
                byte[] encryptedBuffer = ms.ToArray();
                PtCipher cipher = new PtCipher();
                return cipher.Decrypt(encryptedBuffer); // 返回解密后的字节数组
            }
        }
    }

    // 简单的文本写入封装器
    class Writer
    {
        private StreamWriter writer;

        public Writer(StreamWriter writer)
        {
            this.writer = writer;
        }

        public void WriteLine(string val)
        {
            writer.WriteLine(val);
            // 注意：如果文件很大，注释掉下面这行可大幅提升转换速度，因为它向控制台输出大量内容非常耗时
            Console.WriteLine(val);
        }
    }

    /// <summary>
    /// DJMax .pt 离线核心加解密器 (C# 移植版)
    /// 该类整合了 CRC32校验、MT19937伪随机数和魔改XTEA流密码算法。
    /// </summary>
    public class PtCipher
    {
        // MT19937 特有的矩阵参数 A
        private static readonly uint[] MT_MATRIX_A = new uint[] { 0x0, 0x9908B0DF };
        
        // MT19937 的状态数组 (长度 624，索引 624 作为计数器)
        private uint[] mt_state = new uint[625]; 
        
        // 用于异或操作的密钥流 (各 8 字节)
        private byte[] key1_bytes = new byte[8];
        private byte[] key2_bytes = new byte[8];
        
        // Key1 的核心状态，用于 TEA 轮函数的计算
        private uint[] key_1_state = new uint[2];

        // 核心解密 (及加密) 方法
        public byte[] Decrypt(byte[] input)
        {
            if (input.Length < 24) return input; // 如果连 24 字节的文件头都不够，直接返回

            // 分离 24 字节的文件头
            byte[] header = new byte[24];
            Buffer.BlockCopy(input, 0, header, 0, 24);

            // 分离真实加密的数据段
            byte[] data = new byte[input.Length - 24];
            Buffer.BlockCopy(input, 24, data, 0, data.Length);

            // 读取数据段的前 4 个字节，如果数值 <= 10，则被认为是已经解密的明文标志，转为加密模式
            uint decFlag = BitConverter.ToUInt32(data, 0);
            bool isEncode = (decFlag <= 10);

            // 1. 利用文件头初始化 MT19937 状态
            FillData(header);
            
            // 生成初始的 Key2 (提取两次随机数，拼成 8 字节)
            uint k2_0 = CalcParam2();
            uint k2_1 = CalcParam2();
            Buffer.BlockCopy(BitConverter.GetBytes(k2_0), 0, key2_bytes, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(k2_1), 0, key2_bytes, 4, 4);

            // 2. 利用 CRC32 和 简单的加法 Checksum 算法，生成初始的 Key1 状态
            key_1_state[1] = GetCrc32(header);
            key_1_state[0] = GetChecksum(header);
            Buffer.BlockCopy(BitConverter.GetBytes(key_1_state[0]), 0, key1_bytes, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(key_1_state[1]), 0, key1_bytes, 4, 4);

            // 用于存储前一个块明文的反馈数组 (8字节块)，以实现类似 CBC/CFB 的自同步流密码机制
            byte[] pt_block = new byte[8];

            int y = 0; // 块内 8 字节游标
            // 3. 逐字节遍历数据进行核心加解密
            for (int x = 0; x < data.Length; x++)
            {
                byte originalByte = data[x];

                // 如果是加密模式，先将明文保存到反馈块中
                if (isEncode)
                {
                    pt_block[y] = originalByte;
                }

                // 核心异或：明文/密文 = 原数据 ^ (Key2流 ^ Key1流)
                data[x] ^= (byte)(key2_bytes[y] ^ key1_bytes[y]);

                // 如果是解密模式，将解密出的明文保存到反馈块中
                if (!isEncode)
                {
                    pt_block[y] = data[x];
                }

                y++;

                // 当满 8 个字节（即完成一个 Block）时，更新一次密钥流
                if (y == 8)
                {
                    // 使用上一块的 8 字节明文驱动 XTEA 更新 Key1
                    UpdateParam(pt_block);

                    // 从 MT19937 再次提取新的随机数作为下一块的 Key2
                    k2_0 = CalcParam2();
                    k2_1 = CalcParam2();
                    Buffer.BlockCopy(BitConverter.GetBytes(k2_0), 0, key2_bytes, 0, 4);
                    Buffer.BlockCopy(BitConverter.GetBytes(k2_1), 0, key2_bytes, 4, 4);

                    y = 0; // 游标归零
                }
            }

            // 4. 重组头部和处理后的数据并返回
            byte[] result = new byte[input.Length];
            Buffer.BlockCopy(header, 0, result, 0, 24);
            Buffer.BlockCopy(data, 0, result, 24, data.Length);

            return result;
        }

        // 基于上一组明文 (8字节) 更新密钥的魔改 TEA / XTEA 轮函数
        private void UpdateParam(byte[] pt_block)
        {
            uint v8 = key_1_state[0];
            uint v5 = key_1_state[1];
            uint v6 = 0;

            uint pt0 = BitConverter.ToUInt32(pt_block, 0); // 取前 4 字节作为参数 1
            uint pt1 = BitConverter.ToUInt32(pt_block, 4); // 取后 4 字节作为参数 2

            // 执行 32 轮加密迭代
            for (int i = 0; i < 32; i++)
            {
                // 魔数 0x61C88647 的补码表示 (-1640531527)，是 TEA 算法的特征
                v6 = unchecked(v6 - 1640531527); 
                v8 = unchecked(v8 + ((pt1 + (v5 >> 5)) ^ (v6 + v5) ^ (pt0 + (v5 << 4))));
                v5 = unchecked(v5 + ((pt1 + (v8 >> 5)) ^ (v6 + v8) ^ (pt0 + (v8 << 4))));
            }

            // 更新状态并写入字节流
            key_1_state[0] = v8;
            key_1_state[1] = v5;
            Buffer.BlockCopy(BitConverter.GetBytes(v8), 0, key1_bytes, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(v5), 0, key1_bytes, 4, 4);
        }

        // 使用文件头的数据对 MT19937 的初始状态矩阵进行复杂的扰动初始化
        private void FillData(byte[] header)
        {
            // MT19937 标准初始化种子步骤
            mt_state[0] = 0x12BD6AA;
            for (mt_state[624] = 1; mt_state[624] < 624; mt_state[624]++)
            {
                uint prev = mt_state[mt_state[624] - 1];
                mt_state[mt_state[624]] = unchecked(mt_state[624] + 1812433253 * (prev ^ (prev >> 30)));
            }

            int v9 = 1;
            int v6 = 0;
            // 混入 24 字节的文件头数据
            for (int i = 624; i > 0; i--)
            {
                uint header_val = BitConverter.ToUInt32(header, 4 * v6);
                uint prev = mt_state[v9 - 1];
                mt_state[v9] = unchecked((uint)v6 + header_val + (mt_state[v9] ^ (1664525 * (prev ^ (prev >> 30)))));
                v9++;
                v6++;
                if (v9 >= 624) { mt_state[0] = mt_state[623]; v9 = 1; }
                if (v6 >= 6) { v6 = 0; } // 头长 24 字节 = 6 个 uint
            }

            // 再次扰乱状态矩阵
            for (int j = 623; j > 0; j--)
            {
                uint prev = mt_state[v9 - 1];
                mt_state[v9] = unchecked((mt_state[v9] ^ (1566083941 * (prev ^ (prev >> 30)))) - (uint)v9);
                v9++;
                if (v9 >= 624) { mt_state[0] = mt_state[623]; v9 = 1; }
            }
            mt_state[0] = 0x80000000;
        }

        // 标准 MT19937 随机数提取算法 (Twist & Tempering)
        private uint CalcParam2()
        {
            // 如果 624 个随机数都用完了，执行 Twist 旋转生成新一批状态
            if (mt_state[624] >= 624)
            {
                int i;
                for (i = 0; i < 227; ++i)
                {
                    uint v1 = (mt_state[i + 1] & 0x7FFFFFFF) | (mt_state[i] & 0x80000000);
                    mt_state[i] = MT_MATRIX_A[v1 & 1] ^ mt_state[i + 397] ^ (v1 >> 1);
                }
                while (i < 623)
                {
                    uint v2 = (mt_state[i + 1] & 0x7FFFFFFF) | (mt_state[i] & 0x80000000);
                    mt_state[i] = MT_MATRIX_A[v2 & 1] ^ mt_state[i - 227] ^ (v2 >> 1);
                    ++i;
                }
                uint v3 = (mt_state[0] & 0x7FFFFFFF) | (mt_state[623] & 0x80000000);
                mt_state[623] = MT_MATRIX_A[v3 & 1] ^ mt_state[396] ^ (v3 >> 1);
                mt_state[624] = 0;
            }

            // Tempering 操作，提取并返回当前随机数
            uint v4 = mt_state[mt_state[624]++];
            uint v5 = v4 ^ (v4 >> 11);
            v5 ^= ((v5 << 7) & 0x9D2C5680);
            v5 ^= ((v5 << 15) & 0xEFC60000);
            v5 ^= (v5 >> 18);
            return v5;
        }

        // 计算标准的 IEEE 802.3 CRC32 校验码，作为密钥初始化的一部分
        private uint GetCrc32(byte[] data)
        {
            uint[] table = new uint[256];
            uint poly = 0xEDB88320; // 经典的 CRC32 多项式
            
            // 生成查表表
            for (uint i = 0; i < 256; i++)
            {
                uint temp = i;
                for (int j = 8; j > 0; j--)
                {
                    if ((temp & 1) == 1) temp = (temp >> 1) ^ poly;
                    else temp >>= 1;
                }
                table[i] = temp;
            }

            uint crc = 0xFFFFFFFF;
            // 计算 24 字节文件头的 CRC
            for (int i = 0; i < 24; i++)
            {
                byte index = (byte)((crc & 0xFF) ^ data[i]);
                crc = (crc >> 8) ^ table[index];
            }
            return ~crc; // 返回 CRC32 的最终按位取反结果
        }

        // 计算 24 字节头部所有字节的简单加和，作为另一半初始密钥
        private uint GetChecksum(byte[] data)
        {
            uint sum = 0;
            for (int i = 0; i < 24; i++) sum += data[i];
            return sum;
        }
    }
}

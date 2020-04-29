using System;
using System.IO;

namespace pt_to_text
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("DMTQ Tools - pt to text");
            Console.WriteLine("WARNING: Only decrypted pt is supported.");
            Console.WriteLine("WARNING: Some of the commad will be ignored.");

            foreach (string arg in args)
            {
                FileInfo file = new FileInfo(arg);
                using (FileStream ifs = new FileStream(file.FullName, FileMode.Open))
                using (BinaryReader reader = new BinaryReader(ifs))
                using (FileStream ofs = new FileStream(Path.Combine(file.DirectoryName, Path.GetFileNameWithoutExtension(file.Name) + ".txt"), FileMode.Create))
                using (StreamWriter writer = new StreamWriter(ofs))
                {
                    string header = new string(reader.ReadChars(0x4));
                    if(header != "PTFF")
                    {
                        Console.WriteLine("Invalid format, maybe the file is enctypted.");
                        break;
                    }
                    int unknownFlag = reader.ReadInt16();

                    // Read sound info
                    int positionsPerMeasure = reader.ReadInt16();
                    float initialBpm = reader.ReadSingle();
                    int trackCount = reader.ReadInt16();
                    int endPostion = reader.ReadInt32();
                    int tagB = reader.ReadInt32();
                    int soundCount = reader.ReadInt16();
                    
                    
                    writer.WriteLine("#SOUND_COUNT " + soundCount);
                    writer.WriteLine("#TRACK_COUNT " + trackCount);
                    writer.WriteLine("#POSITION_PER_MEASURE " + positionsPerMeasure);
                    writer.WriteLine("#BPM " + initialBpm.ToString());
                    writer.WriteLine("#END_POSITION " + endPostion);
                    writer.WriteLine("#TAGB " + tagB);

                    // Read sound table
                    long currentOffset = 0x18;
                    for (int i = 0; i < soundCount; i++)
                    {
                        ifs.Seek(currentOffset, SeekOrigin.Begin);
                        int id = reader.ReadInt32();
                        char[] fileNameChars = reader.ReadChars(0x40);
                        string fileName = new string(fileNameChars).Replace("\0", string.Empty).Trim();
                        writer.WriteLine("#WAV" + id.ToString("X4") + " " + fileName);
                        Console.WriteLine("#WAV" + id.ToString("X4") + " " + fileName);

                        currentOffset += 0x44;
                    }

                    writer.WriteLine("POSITION COMMAND PARAMETER");
                    int currentTrackCount = 0;
                    while (currentOffset < ifs.Length)
                    {
                        // Read by 0x10

                        int trackHeader = reader.ReadInt32();
                        if(trackHeader == 1381259845) // Check EZTR header
                        {
                            // Skip header
                            ifs.Seek(0x3C, SeekOrigin.Current);

                        } else
                        {
                            int position = trackHeader;
                            int cmd = reader.ReadByte();
                            switch (cmd)
                            {
                                case 0x0: // Track Start
                                    {
                                        char[] temp = reader.ReadChars(0xB);
                                        writer.WriteLine("#" + position + " " + "TRACK_START " + currentTrackCount + " '' ");
                                        break;
                                    }
                                case 0x1: // Note
                                    {
                                        ifs.Seek(0x3, SeekOrigin.Current);
                                        int soundIndex = reader.ReadInt16();
                                        int volume = reader.ReadByte();
                                        int pan = reader.ReadByte();
                                        int type = reader.ReadByte();
                                        int length = reader.ReadByte();
                                        int unknown = reader.ReadInt16();
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
                                case 0x2: // Volume
                                    {
                                        ifs.Seek(0x3, SeekOrigin.Current);
                                        int volume = reader.ReadByte();
                                        int unknown1 = reader.ReadByte();
                                        int unknown2 = reader.ReadByte();
                                        int unknown3 = reader.ReadByte();
                                        int unknown4 = reader.ReadInt32();
                                        writer.WriteLine("#" + position + " " + "VOLUME" + " " + volume + " " + unknown1 + " " + unknown2 + " " + unknown3 + " " + unknown4);
                                        break;
                                    }
                                case 0x3: // BPM Change
                                    {
                                        ifs.Seek(0x3, SeekOrigin.Current);
                                        int bpm = reader.ReadInt32();
                                        ifs.Seek(0x4, SeekOrigin.Current);
                                        writer.WriteLine("#" + position + " " + "BPM_CHANGE" + " " + bpm);
                                        break;
                                    }
                                default: // Other
                                    {
                                        ifs.Seek(0x3, SeekOrigin.Current);
                                        long unknown1 = reader.ReadInt64();
                                        writer.WriteLine("#" + position + " " + cmd + " " + unknown1);
                                        break;
                                    }
                            }
                           
                        }

                        currentOffset = ifs.Position;
                    }

                }
            }
            Console.WriteLine("Done");
            Console.ReadLine();
        }
    }
}

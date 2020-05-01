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
            //// Test 
            //string fstr = "141.0003";
            //float fnum = Single.Parse(fstr);
            //Console.WriteLine(fnum);
            //Console.WriteLine(BitConverter.ToString(BitConverter.GetBytes(fnum)));
            //Console.ReadLine();
            //return;
            Console.WriteLine("DMTQ Tools - bytes to text");
            Console.WriteLine("This tool can help you better understanding the bytes format.");
            Console.WriteLine("Usage: bytes_to_text.exe <anyfile>  - Convert from bytes to text interchange format.");
            Console.WriteLine("Usage: bytes_to_text.exe <filename>.txt  - Convert from the file create by this tool back to .bytes file.");
            Console.WriteLine("");
            Console.WriteLine(@"For the detailed explain please check the github repository");

            foreach(string arg in args)
            {
                FileInfo file = new FileInfo(arg);
                if(!file.Extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    // Do Decode
                    using (FileStream ifs = new FileStream(file.FullName, FileMode.Open))
                    using (BinaryReader reader = new BinaryReader(ifs))
                    using (FileStream ofs = new FileStream(Path.Combine(file.DirectoryName, Path.GetFileNameWithoutExtension(file.Name) + ".txt"), FileMode.Create))
                    using (StreamWriter writer = new StreamWriter(ofs))
                    {
                        int header = reader.ReadInt32();
                        int infoOffset = reader.ReadInt32();

                        // Read sound info
                        ifs.Seek(infoOffset, SeekOrigin.Begin);

                        int soundCount = reader.ReadInt16();
                        int trackCount = reader.ReadInt16();
                        int positionsPerMeasure = reader.ReadInt16(); // ?
                        float initialBpm = reader.ReadSingle();
                        int endPosition = reader.ReadInt32();
                        int tagB = reader.ReadInt32();
                        int tagC = reader.ReadInt32();
                        int totalCommandCount = reader.ReadInt32();
                        writer.WriteLine("#SOUND_COUNT " + soundCount);
                        writer.WriteLine("#TRACK_COUNT " + trackCount);
                        writer.WriteLine("#POSITION_PER_MEASURE " + positionsPerMeasure);
                        writer.WriteLine("#END_POSITION " + endPosition);
                        writer.WriteLine("#TAGB " + tagB);
                        writer.WriteLine("#TAGC " + tagC);
                        writer.WriteLine("#TOTOAL_CMD_COUNT " + totalCommandCount);


                        // Read sound table
                        long currentOffset = 0x8;
                        for (int i = 0; i < soundCount; i++)
                        {
                            ifs.Seek(currentOffset, SeekOrigin.Begin);
                            int id = reader.ReadInt16();
                            reader.ReadByte();
                            char[] fileNameChars = reader.ReadChars(0x40);
                            string fileName = new string(fileNameChars).Replace("\0", string.Empty).Trim();
                            writer.WriteLine("#WAV" + id.ToString("X4") + " " + fileName);

                            currentOffset += 0x43;
                        }

                        writer.WriteLine("POSITION COMMAND PARAMETER");
                        int currentTrackCount = 0;
                        while (currentOffset < infoOffset)
                        {
                            int trackHeader = reader.ReadInt16();
                            char[] trackName = reader.ReadChars(0x3B);

                            int trackPosition = reader.ReadInt32();
                            byte cmd = reader.ReadByte();
                            if (cmd == 0x0)
                            {
                                // Start track reading
                                int shiftedNoteCount = reader.ReadInt32();
                                int noteCount = reader.ReadInt32();
                                Console.WriteLine("#" + trackPosition + " " + "TRACK_START " + currentTrackCount + " '" + new string(trackName).Replace("\0", string.Empty).Trim() + "' " + noteCount);
                                writer.WriteLine("#" + trackPosition + " " + "TRACK_START " + currentTrackCount + " '" + new string(trackName).Replace("\0", string.Empty).Trim() + "' " + noteCount);

                                currentOffset = ifs.Position;
                                for (int i = 0; i < noteCount; i++)
                                {
                                    int position = reader.ReadInt32();
                                    cmd = reader.ReadByte();
                                    switch (cmd)
                                    {
                                        case 0x0: // Track Start
                                            {
                                                char[] temp = reader.ReadChars(0x8);
                                                Console.WriteLine("Warning: New track start before track end");
                                                writer.WriteLine("#" + trackPosition + " " + "TRACK_START " + currentTrackCount + " '" + new string(trackName).Replace("\0", string.Empty).Trim() + "' " + noteCount);
                                                break;
                                            }
                                        case 0x1: // Note
                                            {
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
                                                float bpm = reader.ReadSingle();
                                                int unknown = reader.ReadInt32();
                                                writer.WriteLine("#" + position + " " + "BPM_CHANGE" + " " + bpm + " " + unknown);
                                                break;
                                            }
                                        default: // Other
                                            {
                                                long unknown1 = reader.ReadInt64();
                                                writer.WriteLine("#" + position + " " + cmd + " " + unknown1);
                                                break;
                                            }
                                    }
                                    currentOffset = ifs.Position;
                                }

                            }
                            currentTrackCount++;
                            currentOffset = ifs.Position;
                        }

                    }
                } else
                {
                    // Do Encode
                    using (FileStream ifs = new FileStream(file.FullName, FileMode.Open))
                    using (StreamReader reader = new StreamReader(ifs))
                    using (FileStream ofs = new FileStream(Path.Combine(file.DirectoryName, Path.GetFileNameWithoutExtension(file.Name) + "_converted.bytes"), FileMode.Create))
                    using (BinaryWriter writer = new BinaryWriter(ofs))
                    using (MemoryStream track = new MemoryStream(100))
                    using (MemoryStream sounds = new MemoryStream(100))
                    using (MemoryStream info = new MemoryStream(100))
                    {
                        string line;
                        int commandCounter = 0;
                        int trackCounter = -1;
                        long trackOffset = 0;
                        long commandPos = -1;
                        while ((line = reader.ReadLine())!=null)
                        {
                            if(line.StartsWith("#"))
                            {
                                string[] par = line.Split(' ');
                                if(par[0].StartsWith("#WAV"))
                                {
                                    // Build sounds table
                                    int id = Int32.Parse(par[0].Substring(4), System.Globalization.NumberStyles.HexNumber);
                                    sounds.Write(BitConverter.GetBytes(id), 0, 2);
                                    sounds.Write(new byte[]{ 0x0 }, 0, 1);

                                    byte[] fileNameBytes = new byte[0x40];
                                    byte[] fileNameTemp = Encoding.ASCII.GetBytes(par[1]);
                                    if (fileNameTemp.Length > 0x40)
                                    {
                                        Console.WriteLine("Warning: File name too long: " + par[1]);
                                    }
                                    for (int i = 0; i < fileNameTemp.Length && i < 0x80; i++)
                                    {
                                        fileNameBytes[i] = fileNameTemp[i];
                                    }
                                    sounds.Write(fileNameBytes, 0, 0x40);
                                } 
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
                                    info.Seek(0x12, SeekOrigin.Begin);
                                    info.Write(BitConverter.GetBytes(endPos), 0, 4);
                                }
                                else if (par[0].StartsWith("#TAGB"))
                                {
                                    info.Seek(0xE, SeekOrigin.Begin);
                                    int tagB = Int32.Parse(par[1]);
                                    info.Write(BitConverter.GetBytes(tagB), 0, 4);
                                }
                                else if (par[0].Length > 1 && long.TryParse(par[0].Substring(1), out commandPos))
                                {
                                    switch(par[1])
                                    {
                                        case "TRACK_START": // cmd 0x0
                                            {
                                                // Writer note count to previous header
                                                
                                                long currentPos = track.Position;
                                                if(commandCounter > 0)
                                                {
                                                    track.Seek(trackOffset, SeekOrigin.Begin);
                                                    track.Seek(4, SeekOrigin.Current);
                                                    track.Seek(1, SeekOrigin.Current);
                                                    track.Write(BitConverter.GetBytes(commandCounter << 4), 0, 4);
                                                    track.Write(BitConverter.GetBytes(commandCounter), 0, 4);
                                                    // Go back
                                                    track.Seek(currentPos, SeekOrigin.Begin);
                                                }
                                                // Reset counter
                                                commandCounter = 0;
                                                trackCounter++;
                                                

                                                // Write track header and name
                                                track.Write(new byte[] { 0, 0 }, 0, 2);
                                                byte[] emptyName = Enumerable.Repeat((byte)0x0, 0x3B).ToArray();
                                                track.Write(emptyName, 0, 0x3B);

                                                trackOffset = track.Position;

                                                // Write command
                                                track.Write(BitConverter.GetBytes(commandPos), 0, 4);
                                                track.Write(new byte[] { 0 }, 0, 1);
                                                track.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
                                                track.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
                                                break;
                                            }
                                        case "NOTE": // cmd 0x1
                                            {
                                                commandCounter++;
                                                track.Write(BitConverter.GetBytes(commandPos), 0, 4);
                                                track.Write(new byte[] { 1 }, 0, 1);
                                                int soundIndex = int.Parse(par[2], System.Globalization.NumberStyles.HexNumber); // 2 bytes
                                                int volume = int.Parse(par[3]);
                                                int pan = int.Parse(par[4]);
                                                int attribute = int.Parse(par[5]);
                                                int length = int.Parse(par[6]); // 2 bytes
                                                int unknown = int.Parse(par[7]);
                                                track.Write(BitConverter.GetBytes(soundIndex), 0, 2);
                                                track.Write(BitConverter.GetBytes(volume), 0, 1);
                                                track.Write(BitConverter.GetBytes(pan), 0, 1);
                                                track.Write(BitConverter.GetBytes(attribute), 0, 1);
                                                track.Write(BitConverter.GetBytes(length), 0, 1);
                                                track.Write(BitConverter.GetBytes(unknown), 0, 2);
                                                break;
                                            }
                                        case "VOLUME": // cmd 0x2
                                            {
                                                commandCounter++;
                                                track.Write(BitConverter.GetBytes(commandPos), 0, 4);
                                                track.Write(new byte[] { 2 }, 0, 1);
                                                int vol = int.Parse(par[2]);
                                                int unknown1 = int.Parse(par[3]);
                                                int unknown2 = int.Parse(par[4]);
                                                int unknown3 = int.Parse(par[5]);
                                                int unknown4 = int.Parse(par[6]); // 4 bytes
                                                track.Write(BitConverter.GetBytes(vol), 0, 1);
                                                track.Write(BitConverter.GetBytes(unknown1), 0, 1);
                                                track.Write(BitConverter.GetBytes(unknown2), 0, 1);
                                                track.Write(BitConverter.GetBytes(unknown3), 0, 1);
                                                track.Write(BitConverter.GetBytes(unknown4), 0, 4);
                                                break;
                                            }
                                        case "BPM_CHANGE": // cmd 0x3
                                            {
                                                commandCounter++;
                                                track.Write(BitConverter.GetBytes(commandPos), 0, 4);
                                                track.Write(new byte[] { 3 }, 0, 1);
                                                float bpm = Single.Parse(par[2]); // BPM here is store as float
                                                track.Write(BitConverter.GetBytes(bpm), 0, 4);
                                                track.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
                                                break;
                                            }
                                        default:
                                            {
                                                int cmdCode;
                                                if(int.TryParse(par[1], out cmdCode))
                                                {
                                                    commandCounter++;
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


                        // Write all to file

                        // Write music table
                        ofs.Seek(0x8, SeekOrigin.Begin);
                        sounds.Seek(0x0, SeekOrigin.Begin);
                        sounds.CopyTo(ofs);

                        // Write tracks
                        track.Seek(0, SeekOrigin.Begin);
                        track.CopyTo(ofs);

                        // Write Info Offset
                        long infoOffset = ofs.Position;
                        // Write Info
                        info.Seek(0, SeekOrigin.End);
                        while (info.Length < 0x1A) {
                            info.WriteByte(0x0);
                        }
                        info.Seek(0, SeekOrigin.Begin);
                        info.CopyTo(ofs);

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

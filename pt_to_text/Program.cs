using System;
using System.IO;
using System.Linq;
using UnpackMe.SDK.Core;
using UnpackMe.SDK.Core.Models;

namespace pt_to_text
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("DMTQ Tools - pt to text");
            Console.WriteLine("Convert .pt file to text");
            Console.WriteLine("WARNING: Some of the commad will be ignored.");

            foreach (string arg in args)
            {
                FileInfo file = new FileInfo(arg);
                using (FileStream ifs = new FileStream(file.FullName, FileMode.Open))
                using (BinaryReader reader = new BinaryReader(ifs))
                using (MemoryStream ms = new MemoryStream(100))
                using (BinaryReader msReader = new BinaryReader(ms, System.Text.Encoding.ASCII))
                using (FileStream ofs = new FileStream(Path.Combine(file.DirectoryName, Path.GetFileNameWithoutExtension(file.Name) + ".txt"), FileMode.Create))
                using (StreamWriter sw = new StreamWriter(ofs))
                {
                    Writer writer = new Writer(sw);
                    string header = new string(reader.ReadChars(0x4));
                    if(header != "PTFF")
                    {
                        Console.WriteLine("Invalid format");
                        break;
                    }
                    
                    ifs.Seek(0x18, SeekOrigin.Begin);
                    int firstId = reader.ReadInt16(); // For some pt file has 2 bytes header
                    if(firstId != 1)
                    {
                        // Do decryption
                        Console.WriteLine("Decrypting file");
                        ifs.Seek(0, SeekOrigin.Begin);
                        byte[] result = Decrypt(ifs);
                        ms.Write(result, 0, result.Length);
                    }
                    else
                    {
                        ifs.Seek(0, SeekOrigin.Begin);
                        ifs.CopyTo(ms);
                    }

                    ms.Seek(0x18, SeekOrigin.Begin);
                    if (ms.ReadByte() != 0x1)
                    {
                        Console.WriteLine("Warning: First sound table index is not 1");
                    }

                    // Check if this .pt file has padding
                    bool isPadded = false;
                    if(ms.ReadByte() == 0 && ms.ReadByte() == 0 & ms.ReadByte() == 0)
                    {
                        isPadded = true;
                    }

                    ms.Seek(0x4, SeekOrigin.Begin);
                    int unknownFlag = msReader.ReadInt16();
                    // Read sound info
                    int positionsPerMeasure = msReader.ReadInt16();
                    float initialBpm = msReader.ReadSingle();
                    int trackCount = msReader.ReadInt16();
                    int endPostion = msReader.ReadInt32();
                    int tagB = msReader.ReadInt32();
                    int soundCount = msReader.ReadInt16();
                    
                    
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
                        ms.Seek(currentOffset, SeekOrigin.Begin);
                        int id;
                        if(isPadded)
                        {
                            id = msReader.ReadInt32();
                        } else
                        {
                            id = msReader.ReadInt16();
                        }
                        string fileName = new string(msReader.ReadChars(0x40));
                        fileName = fileName.Substring(0, fileName.IndexOf("\0"));
                        writer.WriteLine("#WAV" + id.ToString("X4") + " " + fileName);

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
                    while (currentOffset < ms.Length)
                    {
                        // Read by 0x10
                        int trackHeader = msReader.ReadInt32();

                        if(trackHeader == 1381259845) // Check EZTR header
                        {
                            // Read Header
                            msReader.ReadInt16();
                            string trackName = new string(msReader.ReadChars(0x40));
                            int ticks = msReader.ReadInt32();
                            int commandCount = msReader.ReadInt32();
                            if(isPadded) msReader.ReadInt16();
                            writer.WriteLine("#0 " + "TRACK_START " + currentTrackCount + " '' ");
                            currentTrackCount++;
                        } else
                        {
                            ms.Seek(-4, SeekOrigin.Current);
                            //int position = msReader.ReadInt16();
                            //int cmd = msReader.ReadByte();
                            //if (cmd > 0x1 && cmd <= 0x4)
                            //{

                            //} else
                            //{
                            //    ms.Seek(-3, SeekOrigin.Current);
                            //    position = msReader.ReadInt32();
                            //    cmd = msReader.ReadByte();
                            //}
                            int position = msReader.ReadInt32();
                            int cmd = msReader.ReadByte();
                            switch (cmd)
                            {
                                case 0x1: // Note
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
                                        int pan = msReader.ReadByte();
                                        int type = msReader.ReadByte();
                                        int length = msReader.ReadByte();
                                        int unknown;
                                        if (isPadded) { 
                                            unknown = msReader.ReadInt16(); 
                                        } else
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
                                case 0x2: // Volume
                                    {
                                        if (isPadded) ms.Seek(0x3, SeekOrigin.Current);
                                        int volume = msReader.ReadByte();
                                        int unknown1 = msReader.ReadByte();
                                        int unknown2 = msReader.ReadByte();
                                        int unknown3 = msReader.ReadByte();
                                        int unknown4;
                                        if(isPadded)
                                        {
                                            unknown4 = msReader.ReadInt32();
                                        }else
                                        {
                                            unknown4 = msReader.ReadInt16();
                                        }
                                        writer.WriteLine("#" + position + " " + "VOLUME" + " " + volume + " " + unknown1 + " " + unknown2 + " " + unknown3 + " " + unknown4);
                                        break;
                                    }
                                case 0x3: // BPM Change
                                    {
                                        if (isPadded) ms.Seek(0x3, SeekOrigin.Current);
                                        int bpm = msReader.ReadInt32();
                                        if (isPadded)
                                        {
                                            ms.Seek(0x4, SeekOrigin.Current);
                                        } else
                                        {
                                            ms.Seek(0x2, SeekOrigin.Current);
                                        }
                                        
                                        writer.WriteLine("#" + position + " " + "BPM_CHANGE" + " " + bpm);
                                        break;
                                    }
                                case 0x4: // Beat 
                                    {
                                        if (isPadded) ms.Seek(0x3, SeekOrigin.Current);
                                        int beat = msReader.ReadInt16();
                                        if(isPadded)
                                        {
                                            ms.Seek(0x6, SeekOrigin.Current);
                                        }
                                        else
                                        {
                                            ms.Seek(0x4, SeekOrigin.Current);
                                        }
                                        
                                        break;
                                    }
                                default: // Other
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
            Console.WriteLine("Done");
            Console.ReadLine();
        }


        private static byte[] Decrypt(Stream data)
        {
            using (UnpackMeClient client = new UnpackMeClient(LoginInfo.URL))
            {
                client.Authenticate(LoginInfo.Username, LoginInfo.Password);
                var commands = client.GetAvailableCommands();
                var commandName = "DJMax *.pt decrypt";
                var decryptCommand = commands.SingleOrDefault(x => x.CommandTitle == commandName);

                var taskId = client.CreateTaskFromCommandId(decryptCommand.CommandId, data);

                TaskModel task;
                string taskStatus;
                do
                {
                    task = client.GetTaskById(taskId);
                    taskStatus = task.TaskStatus;
                    Console.WriteLine("Decrypt task status: " + taskStatus);
                    System.Threading.Thread.Sleep(500);

                } while (taskStatus != "completed");

                return client.DownloadToByteArray(taskId);

            }
        }
    }

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
            Console.WriteLine(val);
        }
    }
}

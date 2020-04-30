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
                using (BinaryReader msReader = new BinaryReader(ms))
                using (FileStream ofs = new FileStream(Path.Combine(file.DirectoryName, Path.GetFileNameWithoutExtension(file.Name) + ".txt"), FileMode.Create))
                using (StreamWriter writer = new StreamWriter(ofs))
                {
                    string header = new string(reader.ReadChars(0x4));
                    if(header != "PTFF")
                    {
                        Console.WriteLine("Invalid format");
                        break;
                    }
                    
                    ifs.Seek(0x18, SeekOrigin.Begin);
                    int firstId = reader.ReadInt32();
                    if(firstId > 1024)
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
                        int id = msReader.ReadInt32();
                        char[] fileNameChars = msReader.ReadChars(0x40);
                        string fileName = new string(fileNameChars).Replace("\0", string.Empty).Trim();
                        writer.WriteLine("#WAV" + id.ToString("X4") + " " + fileName);
                        Console.WriteLine("#WAV" + id.ToString("X4") + " " + fileName);

                        currentOffset += 0x44;
                    }

                    writer.WriteLine("POSITION COMMAND PARAMETER");
                    int currentTrackCount = 0;
                    while (currentOffset < ms.Length)
                    {
                        // Read by 0x10

                        int trackHeader = msReader.ReadInt32();
                        if(trackHeader == 1381259845) // Check EZTR header
                        {
                            // Skip header
                            ms.Seek(0x3C, SeekOrigin.Current);

                        } else
                        {
                            int position = trackHeader;
                            int cmd = msReader.ReadByte();
                            switch (cmd)
                            {
                                case 0x0: // Track Start
                                    {
                                        char[] temp = msReader.ReadChars(0xB);
                                        writer.WriteLine("#" + position + " " + "TRACK_START " + currentTrackCount + " '' ");
                                        break;
                                    }
                                case 0x1: // Note
                                    {
                                        ms.Seek(0x3, SeekOrigin.Current);
                                        int soundIndex = msReader.ReadInt16();
                                        int volume = msReader.ReadByte();
                                        int pan = msReader.ReadByte();
                                        int type = msReader.ReadByte();
                                        int length = msReader.ReadByte();
                                        int unknown = msReader.ReadInt16();
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
                                        ms.Seek(0x3, SeekOrigin.Current);
                                        int volume = msReader.ReadByte();
                                        int unknown1 = msReader.ReadByte();
                                        int unknown2 = msReader.ReadByte();
                                        int unknown3 = msReader.ReadByte();
                                        int unknown4 = msReader.ReadInt32();
                                        writer.WriteLine("#" + position + " " + "VOLUME" + " " + volume + " " + unknown1 + " " + unknown2 + " " + unknown3 + " " + unknown4);
                                        break;
                                    }
                                case 0x3: // BPM Change
                                    {
                                        ms.Seek(0x3, SeekOrigin.Current);
                                        int bpm = msReader.ReadInt32();
                                        ms.Seek(0x4, SeekOrigin.Current);
                                        writer.WriteLine("#" + position + " " + "BPM_CHANGE" + " " + bpm);
                                        break;
                                    }
                                default: // Other
                                    {
                                        ms.Seek(0x3, SeekOrigin.Current);
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
}

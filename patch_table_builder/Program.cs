using FileHelpers;
using patch_table_builder.model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using LZ4;
using System.Security.Cryptography;

namespace patch_table_builder
{
    class Program
    {
        static JavaScriptSerializer json = new JavaScriptSerializer();
        const string CATEGORY_HEADER = "category_id,product_id,display_order,update";
        const string ITEM_DESC_HEADER = "item_id,name,description,summary";
        const string PRODUCT_ITEM_HEADER = "item_id,item_name,img_url_1,img_url_2,description,repeat_count,item_type,limit_minute,status,buy_level,buy_limit_count,buy_limit_type,summary,update";
        const string PRODUCT_PRODUCT_HEADER = "product_id,item_id,platform_product_id,store_product_id,product_type,cost_game_point,cost_game_cash,status,sale_start_date,sale_end_date,update";
        const string SONG_DESC_HEADER = "song_id,fullname,genre,artist,composed_by,singer,feat_by,arranged_by,visualized_by";
        const string SONG_HEADER = "song_id,item_id,name,full_name,genre,artist_name,original_bga_yn,loop_bga_yn,composed_by,singer,feat_by,arranged_by,visualized_by,cost_game_point,cost_game_cash,flag,status,free_yn,hidden_yn,open_yn,track_id,mod_date,update";
        const string PATTERN_HEADER = "pattern_id,song_id,signature,line,difficulty,point_type,point_value,flg,update";

        const string PATCH_HEADER = "file_name,file_size,checksum,compressed_file_size,compressed_checksum,acquire_on_demand,compressed,platform,tag,";
        
        const string CATEGORY_PRODUCT_PATH = @"table\<LANG>\category_categoryproduct.csv";
        const string ITEM_DESC_PATH = @"table\<LANG>\item_desc_<LANG>.csv";
        const string PRODUCT_ITEM_PATH = @"table\<LANG>\product_item.csv";
        const string PRODUCT_PRODUCT_PATH = @"table\<LANG>\product_product.csv";
        const string SONG_DESC_PATH = @"table\<LANG>\song_desc_<LANG>.csv";
        const string SONG_PATH = @"table\<LANG>\song_song.csv";
        const string PATTERN_PATH = @"table\<LANG>\song_songPattern.csv";

        static string[] ALL_PATH = new string[] { CATEGORY_PRODUCT_PATH, ITEM_DESC_PATH, PRODUCT_ITEM_PATH, PRODUCT_ITEM_PATH, PRODUCT_PRODUCT_PATH, SONG_DESC_PATH, SONG_PATH, PATTERN_PATH };

        static void Main(string[] args)
        {
            Console.WriteLine("DMTQ Tools - patch table builder");
            if (!File.Exists("song.json"))
            {
                Console.WriteLine("New folder detected, press enter to create song info");
                Console.ReadLine();

                Read();
            } else
            {
                Console.WriteLine("Ready to build patch from json, press enter to continue");
                Console.ReadLine();

                Write();
                Copy();
                UpdatePatch(true);
            }


            Console.WriteLine("Done");
            Console.ReadLine();
        }

        static string getMD5(string path)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(path))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        static PatchInfo GetFileInfo(string path)
        {
            var P = path.Replace(@"<LANG>", "us");
            return new PatchInfo(new FileInfo(P).Length, getMD5(P), new FileInfo(P + ".lz4").Length, getMD5(P + ".lz4"));
        }

        static void UpdateField(Patch patch, PatchInfo info)
        {
            patch.checksum = info.checksum;
            patch.file_size = info.file_size;
            patch.compressed_checksum = info.compressed_checksum;
            patch.compressed_file_size = info.compressed_file_size;
        }
        static void UpdatePatch(bool updatePreview = false)
        {
            Console.WriteLine("Updating Patch csv...");
            if (!File.Exists(@"patch_new.csv"))
            {
                Decompress(@"patch_new.csv.lz4");
            }
            var patches = ReadFile<Patch>(@"patch_new.csv");

            var songInfo = GetFileInfo(SONG_PATH);
            var songDescInfo = GetFileInfo(SONG_DESC_PATH);
            var ptInfo = GetFileInfo(PATTERN_PATH);
            var piInfo = GetFileInfo(PRODUCT_ITEM_PATH);
            var ppInfo = GetFileInfo(PRODUCT_PRODUCT_PATH);
            var caInfo = GetFileInfo(CATEGORY_PRODUCT_PATH);
            var idInfo = GetFileInfo(ITEM_DESC_PATH);

            foreach (Patch patch in patches)
            {
                if (patch.file_name.Contains("song_desc_"))
                {
                    UpdateField(patch, songDescInfo);
                }
                if (patch.file_name.Contains("song_song.csv"))
                {
                    UpdateField(patch, songInfo);
                }
                if (patch.file_name.Contains("song_songPattern.csv"))
                {
                    UpdateField(patch, ptInfo);
                }
                if (patch.file_name.Contains("product_item.csv"))
                {
                    UpdateField(patch, piInfo);
                }
                if (patch.file_name.Contains("product_product.csv"))
                {
                    UpdateField(patch, ppInfo);
                }
                if (patch.file_name.Contains("category_categoryproduct.csv"))
                {
                    UpdateField(patch, caInfo);
                }
                if (patch.file_name.Contains("item_desc_"))
                {
                    UpdateField(patch, idInfo);
                }
            }

            if (updatePreview)
            {
                foreach (string file in Directory.EnumerateFiles("preview", "*.opus"))
                {
                    Console.WriteLine("Updating preview info ... {0}", file);
                    Compress(file);
                    var info = GetFileInfo(file);
                    var fileName = file.Replace("\\", "/");
                    var result = patches.Where(x => x.file_name.Contains(fileName));
                    Patch patch;
                    if (result.Any())
                    {
                        patch = result.First();
                    } else
                    {
                        patch = new Patch();
                        patch.file_name = fileName;
                        patch.acquire_on_demand = 0;
                        patch.compressed = 1;
                        patch.platform = "";
                        patch.tag = "";
                        patch.unused = "";
                        patches.AddLast(patch);
                    }
                    patch.checksum = info.checksum;
                    patch.file_size = info.file_size;
                    patch.compressed_checksum = info.compressed_checksum;
                    patch.compressed_file_size = info.compressed_file_size;
                }
            }

            WriteFile(patches.ToList(), PATCH_HEADER, @"patch_new.csv");
            Compress(@"patch_new.csv");
        }

        static LinkedList<T> ReadFile<T>(string path) where T : class
        {
            Console.WriteLine("Reading: {0}", path);
            UTF8Encoding utf8 = new UTF8Encoding();
            var reader = new FileHelperAsyncEngine<T>(utf8);

            var result = new LinkedList<T>();

            using (reader.BeginReadFile(path))
            {
                foreach (T item in reader)
                {
                    result.AddLast(item);
                }
            }
            reader.Close();
            return result;
        }

        static void DecompressIfNeed(string path)
        {
            if (!File.Exists(path.Replace(@"<LANG>", "us"))) Decompress(path.Replace(@"<LANG>", "us"));
        }

        static void Read()
        {
            // Decompress
            foreach (string path in ALL_PATH) DecompressIfNeed(path);

            // Read csv
            var songs = ReadFile<Song>(SONG_PATH.Replace(@"<LANG>", "us"));
            var patterns = ReadFile<Pattern>(PATTERN_PATH.Replace(@"<LANG>", "us"));
            var category = ReadFile<Category>(CATEGORY_PRODUCT_PATH.Replace(@"<LANG>", "us"));
            var itemDesc = ReadFile<ItemDesc>(ITEM_DESC_PATH.Replace(@"<LANG>", "us"));
            var productItem = ReadFile<ProductItem>(PRODUCT_ITEM_PATH.Replace(@"<LANG>", "us"));
            var productProduct = ReadFile<ProductProduct>(PRODUCT_PRODUCT_PATH.Replace(@"<LANG>", "us"));
            var songDesc = ReadFile<SongDesc>(SONG_DESC_PATH.Replace(@"<LANG>", "us"));

            // Write json
            File.WriteAllText(@"song.json", json.Serialize(songs));
            File.WriteAllText(@"pattern.json", json.Serialize(patterns));
            File.WriteAllText(@"category.json", json.Serialize(category));
            File.WriteAllText(@"itemDesc.json", json.Serialize(itemDesc));
            File.WriteAllText(@"productItem.json", json.Serialize(productItem));
            File.WriteAllText(@"productProduct.json", json.Serialize(productProduct));
            File.WriteAllText(@"songDesc.json", json.Serialize(songDesc));
        }

        static void WriteFile<T>(List<T> data, string header, string path) where T : class
        {
            Console.WriteLine("Writing: {0}", path);
            UTF8Encoding utf8 = new UTF8Encoding();
            var writer = new FileHelperAsyncEngine<T>(utf8);

            writer.HeaderText = header;
            using (writer.BeginWriteFile(path))
            {
                foreach (T item in data)
                {
                    writer.WriteNext(item);
                }
            }
            writer.Close();
        }

        static void Write()
        {
            // Read json
            var songs = json.Deserialize<List<Song>>(File.ReadAllText(@"song.json"));
            var patterns = json.Deserialize<List<Pattern>>(File.ReadAllText(@"pattern.json"));
            var category = json.Deserialize<List<Category>>(File.ReadAllText(@"category.json"));
            var itemDesc = json.Deserialize<List<ItemDesc>>(File.ReadAllText(@"itemDesc.json"));
            var productItem = json.Deserialize<List<ProductItem>>(File.ReadAllText(@"productItem.json"));
            var productProduct = json.Deserialize<List<ProductProduct>>(File.ReadAllText(@"productProduct.json"));
            var songDesc = json.Deserialize<List<SongDesc>>(File.ReadAllText(@"songDesc.json"));

            // Write csv
            WriteFile(songs, SONG_HEADER, SONG_PATH.Replace(@"<LANG>", "us"));
            WriteFile(patterns, PATTERN_HEADER, PATTERN_PATH.Replace(@"<LANG>", "us"));
            WriteFile(category, CATEGORY_HEADER, CATEGORY_PRODUCT_PATH.Replace(@"<LANG>", "us"));
            WriteFile(itemDesc, ITEM_DESC_HEADER, ITEM_DESC_PATH.Replace(@"<LANG>", "us"));
            WriteFile(productItem, PRODUCT_ITEM_HEADER, PRODUCT_ITEM_PATH.Replace(@"<LANG>", "us"));
            WriteFile(productProduct, PRODUCT_PRODUCT_HEADER, PRODUCT_PRODUCT_PATH.Replace(@"<LANG>", "us"));
            WriteFile(songDesc, SONG_DESC_HEADER, SONG_DESC_PATH.Replace(@"<LANG>", "us"));

            // Compress
            foreach (string path in ALL_PATH) Compress(path.Replace(@"<LANG>", "us"));
        }

        static void Copy()
        {
            Console.WriteLine("Copying file to all language folder...");


            var langs = new string[] { "cn", "jp", "kr", "tw" };
            foreach (string lang in langs)
            {
                foreach(string path in ALL_PATH) CopyAll(path, lang);
            }
        }

        static void CopyAll(string path, string lang)
        {

            File.Copy(path.Replace(@"<LANG>", "us"), path.Replace(@"<LANG>", lang), true);
            File.Copy(path.Replace(@"<LANG>", "us") + ".lz4", path.Replace(@"<LANG>", lang) + ".lz4", true);
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
    class PatchInfo
    {
        public long file_size { get; set; }
        public string checksum { get; set; }
        public long compressed_file_size { get; set; }
        public string compressed_checksum { get; set; }

        public PatchInfo(long file_size, string checksum, long compressed_file_size, string compressed_checksum)
        {
            this.file_size = file_size;
            this.checksum = checksum;
            this.compressed_file_size = compressed_file_size;
            this.compressed_checksum = compressed_checksum;
        }
    }
}

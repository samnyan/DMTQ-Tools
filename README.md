# DMTQ Tools

Useful tools for DJMAX Technika Q

I will add the finished tool here.

## Binary Download

[release](https://github.com/samnyan/DMTQ-Tools/releases)

## Currently available

### fpk tool

For unpack and repack dmtq's .fpk file. 

Just drag the .fpk file or the folder you want to repack to the exe.

### bytes to text

Convert .bytes format to a readable text format, and convert it back to bytes format.

This tool can help you better understand the bytes format. For more detail please see the [bytes-format.md](bytes-format.md)

### pt to text

Convert .pt format to a readable text format. This tool can decrypt .pt file now, but if you want to build it from source, please provide the decrypt api server info in the LoginInfo.cs.

Combine with bytes to text tool, it should be able to port song from Arcade version.

For format explain please see the [pt-format.md](pt-format.md)

### lz4 tool

For compress and decompress .lz4 file use in update pack.

Just drag the onto the exe, it will detect the extention and compress or decompress the file.

Or use parameter 
* -d <folder> to decompress all file inside that folder.
* -c <folder> to compress all file inside that folder.


## Useful information

### Port song from Arcade version

What you need is a unpacked .pak with all .ogg inside, and a .pt file.

(If you are planing to port custom, you should have the unpacked .pak)

Drag the folder that containing all the .ogg files to fpk_tool to generate a .fpk pack.

Convert video to .webm format if you want to have video.

Use pt_to_text to convert .pt to text format.

Open the converted .txt file, check if the end position (End Ticks) is set to the last note position or later than last note position.

Use bytes_to_text to convert the .txt back to DMTQ's .bytes file.

Now you can replace the .fpk and patterns to the server side, and re-download the song to your device.

If every thing is good, you can create a new patch by changing the patch file on server. Use lz4_tool to decompress and compress those file.

### Modifying game table

For adding song and patterns to the game select list.

Open the patch folder at server side. `patch\phone_new\1.003.005\[android|ios]`.

**Patch List Table**

Decompress the `patch_new.csv.lz4` using lz4_tool, and you can use Excel or text editor to edit this file.

    patch_new.csv

    `file_name,file_size,checksum,compressed_file_size,compressed_checksum,acquire_on_demand,compressed,platform,tag`

* file_name
    * the relative path of the file.

* file_size
    * You can use PowerShell command `(Get-Item ./<filepath>).Length` to get it.

* checksum
    * You can use PowerShell command `(Get-FileHash ./<filepath> -Algorithm MD5).Hash.ToLower()` calculate the checksum.

* compressed_file_size,compressed_checksum
    * You can leave it empty and provide a uncompressed file (Without .lz4 extension)

* acquire_on_demand
    * leave it 0

* compressed
    * Set to 0 then you can use a uncompressed file

**Song List Table**

Go to `table\<language>`. The only difference between those language version is the description file with the _<lang> suffix. Other files can just copy to between languages.

Decompress the file you want to edit. For adding a song, the following file is required to modify.

* song_song.csv: Music table
    * `name` is important in this table, it act as an ID to load images and audio file, so make it unique.

* song_songPattern.csv: Patterns table. 
    * `signature` is difficulty, 0 is easy then 3 is EX. 
    * `line` is actual line + 1 (eg: 3 means 2Line here). 
    * `difficulty` is star.

* product_item.csv: Item table
    * The music_id you added to the Music table is required to add here.

* product_product.csv

* category_categoryproduct.csv: Category of that item

* item_desc_<lang>.csv: Item description

* song_desc_<lang>.csv: Song description
    * The song info is load from here

Edit those file by following the exist data. If you miss some of them, the game may crash after loading.


You don't have to compress it back to .lz4 after finished the edit. Set `compressed` to 0 in `patch_new.csv` save so much time for you.

Now copy the finished file to other language table folder. (You can skip this step if you only use in one language)

**Song Preview**

Simply open the `preview` folder, convert the preview file to .opus format, and rename it to <songname>.p.opus. Song name it what you use in the `song_song.csv`.


Go back to `patch_new.csv`. Modify the `file_size` and `checksum` for every file you have modified.

Use lz4_tool to compress back to `patch_new.csv.lz4`

Now you can upload this files to your server to check if that works.
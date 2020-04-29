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

Convert decrypted .pt format to a readable text format. You need to decrypted it first to use this tool.

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

What you need is a unpacked .pak with all .ogg inside, and a decrypted .pt file.

(If you are planing to port custom, you should have the unpacked .pak, but still need to decrypt the .pt)

Drag the folder that containing all the .ogg files to fpk_tool to generate a .fpk pack.

Convert video to .webm format if you want to have video.

Use pt_to_text to convert decrypted .pt to text format.

Open the converted .txt file, check if the end position (End Ticks) is set to the last note position or later than last note position.

Use bytes_to_text to convert the .txt back to DMTQ's .bytes file.

Now you can replace the .fpk and patterns to the server side, and re-download the song to your device.

If every thing is good, you can create a new patch by changing the patch file on server. Use lz4_tool to decompress and compress those file.
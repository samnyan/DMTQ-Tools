DJMax Technika PT Format
========================

header
----------------------------
* 4 bytes of 50 54 46 46(PTFF)
* 2 bytes unknown (always 01 00 ?)
* 2 bytes number of positions per measure
* 4 bytes initial BPM (float)
* 2 bytes number of tracks
* 4 bytes end position
* 4 bytes unknown (Tag B)
* 2 bytes number of sounds

Sounds Table
----------------------------
* Starts at offset 8
* An array of entries that are 0x43 bytes in size each:
    * 4 bytes index
    * 0x40 bytes filename


Command
----------------------------
* An array of commands (0x10 bytes each):
    * 4 bytes position
    * 1 byte command type ?
    * 3 bytes unknown
    * 8 bytes of params

See [bytes-format.md](bytes-format.md)

Tracks
------

* Track header:
	* 45 5A 54 52 (EZTR?) (total 0x40 bytes, rest of them should be the track name)


Track Start Command:

```
  Position    |CMD|  UNKNOWN  |NOTE |
00 00 00 00 00 00  80 31 00 00 F0 08 00 00 00 00 (First one is very large, maybe that is the total count)
00 00 00 00 00 00  28 2F 00 00 10 08 00 00 00 00
00 00 00 00 00 00  D4 31 00 00 30 08 00 00 00 00
00 00 00 00 00 00  E0 31 00 00 F0 08 00 00 00 00
```



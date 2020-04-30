DJMax Technika PT Song Format
========================

header
----------------------------
* 4 bytes of 50 54 46 46(PTFF)
* 2 bytes Unknown (always 01 00 ?)
* 2 bytes Ticks per measure
* 4 bytes Master BPM (float)
* 2 bytes Number of tracks
* 4 bytes Total Ticks (Ticks of the very last note of the pattern)
* 4 bytes Time (in seconds) (Convert as Tag B in current version)
* 2 bytes Number of sounds

Sounds Table
----------------------------
* Starts at offset 0x18
* An array of entries that are 0x43 (68) bytes in size each:
    * 4 bytes index
    * 0x40 (64) bytes filename



Tracks
------
* Track header: Total 80 bytes
    * 4 bytes Track initializer 45 5A 54 52 ("EZTR")
    * 2 bytes blank
    * 64 bytes Name of Track (string)
    * 4 bytes Tick (means the position of the last note of the track, int)
    * 4 bytes Size of data (excluding header, means Num of objects * Size of individual notes)
    * 2 bytes blank


Notes
----------------------------
* An array of commands (0x10 bytes each):
    * 4 bytes position
    * 1 byte command type
    * 3 bytes blank
    * 8 bytes of params

Command types (Also see [bytes-format.md](bytes-format.md))
-------------

Params
----------------------------
From offset 0x08 of each note

* General Note (Command = 1)
    * 2 bytes Sound index in sounds table (0 ~ 1023)
    * 1 byte Volume (0 ~ 127)
    * 1 byte Pan (0 ~ 127)
    * 1 byte Attribute (0 ~ 255)
    * 2 bytes Duration (6 if not long note)
    * 1 byte blank

* Volume Note (Command = 2)
    * 1 byte Volume (0 ~ 127)
    * 7 bytes blank

* BPM Change Note (Command = 3)
    * 4 bytes Tempo (float)
    * 4 bytes blank

* Beat Note (Command = 4)
    * 2 bytes beat (0 ~ 65535)
    * 6 bytes blank
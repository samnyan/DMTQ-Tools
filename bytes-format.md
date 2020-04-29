DJMax Technika Q Song Format
============================

Original from: https://github.com/oliverchang/technika_q/blob/master/format.md

The track format has changed from that, this is the latest format



Header
------
* 4 bytes unknown (always zeros?)
* 4 bytes offset to song information

Song information (usually at the very end of the file)
------------------------------------------------------
* 2 bytes number of sounds
* 2 bytes number of tracks?
* 2 bytes number of positions per measure?
* 4 bytes initial BPM (float) (can be 0 if contains change bpm cmd)
* 4 bytes end position
* 4 bytes unknown (another float?) (Tag B)
* 4 bytes end position
* 4 bytes total command count?(can be 0) (not include start of track, but include change bpm )

Sounds Table
----------------------------
* Starts at offset 8
* An array of entries that are 0x43 bytes in size each:
    * 2 bytes index
    * 1 byte unknown
    * 0x40 bytes filename


Tracks
------
* Track header:
    * 2 bytes of 0x0
    * 0x3B bytes of track name (The track name doesn't relate to it's function. Actually all the track name is empty string in the later patterns)

Follow by
* An array of commands (0xD bytes each):
    * 4 bytes position
    * 1 byte command type
    * 8 bytes of params

### Notes
* The position refers to a position in the song
    * Its meaning is determined by the number of
      positions per measure (in song info)
    * Assumes 4/4 time signature?
* There are usually 64 tracks
* The playable tracks for 3 lines are (0 indexed in order of appearance in file):
    * 0: top line
    * 1: middle line
    * 2: bottom line

Command types
-------------
### 0:  Start of a track
#### Params
* 4 bytes note count shifted?
* 4 bytes note count

### 1: Note
#### Params
* 2 bytes sound index
* 1 byte volume
* 1 byte pan
* 1 byte attribute
* 1 byte? length (for long notes, unit is a single position)
* 2 bytes unknown

#### Attributes
* 0: normal note (drag note if length != 0x6)
* A: repeat note
* B: last repeat note
* C: hold note
* 5: chain note start (all notes in between have type 0)
* 6: chain note end
* 2: unknown
* 100: VideoStart
* 150: ?

### 2: Volume
#### Params
* 1 bytes volume
* 7 bytes unknown (1 + 1 + 1 + 4 bytes?) (It is ok to fill with 0)
An example from the same song (A new pattern)
```
00 00 00 00 02 73 00 00 00 88 3A 34 02
00 00 00 00 02 73 00 00 00 88 3A 34 02
00 00 00 00 02 7D 00 00 00 88 3A 34 02
00 00 00 00 02 69 00 00 00 88 3A 34 02
```

An example from another song (A older pattern)
```
00 00 00 00 02 73 C7 F3 01 28 FE C7 02
00 00 00 00 02 73 C7 F3 01 28 FE C7 02
00 00 00 00 02 7D 00 00 00 C0 3C 50 01
00 00 00 00 02 69 00 00 00 40 7A F3 01
```


### 3: BPM change
#### Params
* 4 bytes float BPM
* 4 bytes unknown

### 4: Unknown
DJMAX TECHNIKA PT Song Format
============================

A PT file contains a header, a sound table, and a sequence of tracks.
Each track contains a track header followed by its events.

All numeric fields are little-endian, except the version field.
`float32` means a 4-byte IEEE 754 floating-point value.
Strings use fixed-width byte buffers and end at the first NUL byte.

Header
------

Total size: **0x18 (24) bytes**, identical in versions 0.6 and 1.0.

* `0x00`: 4 bytes — signature, `50 54 46 46` (`PTFF`).
* `0x04`: 2 bytes — version: `00 06` for **0.6**, `01 00` for **1.0**.
* `0x06`: `uint16` — ticks per measure.
* `0x08`: `float32` — initial BPM.
* `0x0C`: `uint16` — number of tracks.
* `0x0E`: `uint32` — total ticks / end position.
* `0x12`: `float32` — play time in seconds.
* `0x16`: `uint16` — number of sounds.

The header is not encrypted. If the remaining data is encrypted, decrypt it
before reading the sound table and tracks. The layouts below describe decrypted data.

Sound Table
-----------

Starts at offset `0x18`. Contains the number of entries specified in the header.

### Version 0.6

Entry size: **0x42 (66) bytes**.

* `0x00`: `uint8` — sound index.
* `0x01`: `uint8` — flag.
* `0x02`: 64 bytes — sound filename.

### Version 1.0

Entry size: **0x44 (68) bytes**.

* `0x00`: `uint16` — sound index.
* `0x02`: `uint8` — flag.
* `0x03`: 1 byte — reserved.
* `0x04`: 64 bytes — sound filename.

Sound indices identify the entries referenced by note events; they are not row numbers.
Version 0.6 indices fit in one byte; version 1.0 indices range from 0 to 1023.

Track Header
------------

Size: **0x4E (78) bytes in 0.6**, **0x50 (80) bytes in 1.0**.

* `0x00`: 4 bytes — signature, `45 5A 54 52` (`EZTR`).
* `0x04`: 2 bytes — reserved.
* `0x06`: 64 bytes — track name.
* `0x46`: `uint32` — track end position, in ticks.
* `0x4A`: `uint32` — event data size in bytes, excluding the track header.
* `0x4E`: 2 bytes — reserved, **1.0 only**; absent in 0.6.

The events immediately follow the track header.
Event count = event data size / event size.

Events
------

All events have a fixed size within each version, including unknown commands.

### Version 0.6

Event size: **0x0B (11) bytes**.

* `0x00`: `uint32` — position, in ticks.
* `0x04`: `uint8` — command type.
* `0x05`: 6 bytes — parameters.

### Version 1.0

Event size: **0x10 (16) bytes**.

* `0x00`: `uint32` — position, in ticks.
* `0x04`: `uint8` — command type.
* `0x05`: 3 bytes — reserved.
* `0x08`: 8 bytes — parameters.

Command Parameters
------------------

Offsets in this section are relative to the start of the parameters,
not the start of the event.

### Command 1 — Note

**Version 0.6** — 6 bytes:

* `0x00`: `uint8` — sound index.
* `0x01`: `uint8` — volume, 0–127.
* `0x02`: `uint8` — pan, 0–127.
* `0x03`: `uint8` — attribute.
* `0x04`: `uint16` — duration, in ticks; normally 6 for a short note.

**Version 1.0** — 8 bytes:

* `0x00`: `uint16` — sound index.
* `0x02`: `uint8` — volume, 0–127.
* `0x03`: `uint8` — pan, 0–127.
* `0x04`: `uint8` — attribute.
* `0x05`: `uint16` — duration, in ticks; normally 6 for a short note.
* `0x07`: 1 byte — reserved.

### Command 2 — Volume

* `0x00`: `uint8` — volume, 0–127.
* `0x01`: reserved — **5 bytes in 0.6**, **7 bytes in 1.0**.

### Command 3 — BPM Change

* `0x00`: `float32` — new BPM.
* `0x04`: reserved — **2 bytes in 0.6**, **4 bytes in 1.0**.

### Command 4 — Beat

* `0x00`: `uint8` — beat value.
* `0x01`: reserved — **5 bytes in 0.6**, **7 bytes in 1.0**.

### Other Commands

Parameter meanings are unspecified. Keep the full parameter buffer:
**6 bytes in 0.6**, **8 bytes in 1.0**.

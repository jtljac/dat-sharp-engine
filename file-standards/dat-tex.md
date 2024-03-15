```
Header {
    u8[7]   signature   (Expected Value: B1 44 41 54 54 45 58, ±DATTEX)
    u8      version     (Expected Value: 0x01, 1)
    u32     width
    u32     height
    Format  format
}
```

```
Format: enum (u16) {
    R8              value = 0
    R8G8            value = 1
    R8G8B8          value = 2
    R8G8B8A8        value = 3
    R16             value = 4
    R16G16          value = 5
    R16G16B16       value = 6
    R16G16B16A16    value = 7
}
```

```
File {
    Header      head
    u8[]        pixels      Size = width * height * format.size
}
```

# Description
The File is split into 2 parts:

## The Header
The header contains:
* signature: A 7 byte long magic value to identify the file
* version: The version of the file standard
* width: The width of the image
* height: The height of the image
* format: The format of the pixels in the image

## The pixel array:
The pixel array is a continuous stream of pixels exactly the length `width * height * format.size`, of which are
defined in the header.

Each actual pixel can be formed out of multiple bytes, as determined by the `format`.

A pixel can have multiple channels, of which can be multiple bytes in size. The number of channels and their sizes is
determined by the `format`

# Extra Information
## Format:
The format is an enum that defines the layout of the pixels in memory.

Each format is described by the number of channels, and the size of each channel. The format name can be read as a
sequence of values of which consist of a channel label followed by its size in bits.

For example:
* `R8G8` - 2 channels, `r` and `g`, both of which are 1 byte long
* `R16G16B16A16` - 4 channels, `r`,`g`,`b`,`a`, each of which are 2 bytes long
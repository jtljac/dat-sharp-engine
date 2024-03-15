```
Header {
    u8[8]       signature       (Expected Value: B1 44 41 54 4D 45 53 48, ±DATMESH)
    u8          version         (Expected Value: 0x01, 1)
    u8          VertexSize
    u32         vertexCount
    u32         indexCount
    u8          TypeHintSize    (0 for disabled)
}
```

```
TypeHint : enum (u8) {
    R8_UINT = 0 (0b00000000)
    R8_SINT = 1 (0b00000001)
    R8_UNORM = 3 (0b00000011)
    R8_SNORM = 4 (0b00000100)
    R8_USCALED = 5 (0b00000101)
    R8_SSCALED = 6 (0b00000110)
    R8G8_UINT = 16 (0b00010000)
    R8G8_SINT = 17 (0b00010001)
    R8G8_UNORM = 19 (0b00010011)
    R8G8_SNORM = 20 (0b00010100)
    R8G8_USCALED = 21 (0b00010101)
    R8G8_SSCALED = 22 (0b00010110)
    R8G8B8_UINT = 32 (0b00100000)
    R8G8B8_SINT = 33 (0b00100001)
    R8G8B8_UNORM = 35 (0b00100011)
    R8G8B8_SNORM = 36 (0b00100100)
    R8G8B8_USCALED = 37 (0b00100101)
    R8G8B8_SSCALED = 38 (0b00100110)
    R8G8B8A8_UINT = 48 (0b00110000)
    R8G8B8A8_SINT = 49 (0b00110001)
    R8G8B8A8_UNORM = 51 (0b00110011)
    R8G8B8A8_SNORM = 52 (0b00110100)
    R8G8B8A8_USCALED = 53 (0b00110101)
    R8G8B8A8_SSCALED = 54 (0b00110110)
    R16_UINT = 64 (0b01000000)
    R16_SINT = 65 (0b01000001)
    R16_SFLOAT = 66 (0b01000010)
    R16_UNORM = 67 (0b01000011)
    R16_SNORM = 68 (0b01000100)
    R16_USCALED = 69 (0b01000101)
    R16_SSCALED = 70 (0b01000110)
    R16G16_UINT = 80 (0b01010000)
    R16G16_SINT = 81 (0b01010001)
    R16G16_SFLOAT = 82 (0b01010010)
    R16G16_UNORM = 83 (0b01010011)
    R16G16_SNORM = 84 (0b01010100)
    R16G16_USCALED = 85 (0b01010101)
    R16G16_SSCALED = 86 (0b01010110)
    R16G16B16_UINT = 96 (0b01100000)
    R16G16B16_SINT = 97 (0b01100001)
    R16G16B16_SFLOAT = 98 (0b01100010)
    R16G16B16_UNORM = 99 (0b01100011)
    R16G16B16_SNORM = 100 (0b01100100)
    R16G16B16_USCALED = 101 (0b01100101)
    R16G16B16_SSCALED = 102 (0b01100110)
    R16G16B16A16_UINT = 112 (0b01110000)
    R16G16B16A16_SINT = 113 (0b01110001)
    R16G16B16A16_SFLOAT = 114 (0b01110010)
    R16G16B16A16_UNORM = 115 (0b01110011)
    R16G16B16A16_SNORM = 116 (0b01110100)
    R16G16B16A16_USCALED = 117 (0b01110101)
    R16G16B16A16_SSCALED = 118 (0b01110110)
    R32_UINT = 128 (0b10000000)
    R32_SINT = 129 (0b10000001)
    R32_SFLOAT = 130 (0b10000010)
    R32G32_UINT = 144 (0b10010000)
    R32G32_SINT = 145 (0b10010001)
    R32G32_SFLOAT = 146 (0b10010010)
    R32G32B32_UINT = 160 (0b10100000)
    R32G32B32_SINT = 161 (0b10100001)
    R32G32B32_SFLOAT = 162 (0b10100010)
    R32G32B32A32_UINT = 176 (0b10110000)
    R32G32B32A32_SINT = 177 (0b10110001)
    R32G32B32A32_SFLOAT = 178 (0b10110010)
    R64_UINT = 192 (0b11000000)
    R64_SINT = 193 (0b11000001)
    R64_SFLOAT = 194 (0b11000010)
    R64G64_UINT = 208 (0b11010000)
    R64G64_SINT = 209 (0b11010001)
    R64G64_SFLOAT = 210 (0b11010010)
    R64G64B64_UINT = 224 (0b11100000)
    R64G64B64_SINT = 225 (0b11100001)
    R64G64B64_SFLOAT = 226 (0b11100010)
    R64G64B64A64_UINT = 240 (0b11110000)
    R64G64B64A64_SINT = 241 (0b11110001)
    R64G64B64A64_SFLOAT = 242 (0b11110010)
}
```

```
File {
    Header      head
    TypeHint[]  TypeHints   Size = TypeHintSize
    u8[]        vertices    Size = vertexCount * vertexSize
    u32[]       indices     Size = indexCount
}
```

# Description
The File is split into 3 parts:

## The header
The header contains:
* signature: A 8 byte long magic value to identify the file
* version: The version of the file standard
* vertexSize: The size of each vertex in bytes (including padding)
* vertexCount: The amount of vertices that the file contains
* indexCount: The amount of indices the file contains
* TypeHintSize: The amount of type hints the file contains

## Type hints
The TypeHints array is a continuous stream of unsigned 8 bit integers, of which represent an entry in the TypeHint Enum,
exactly the length `TypeHintSize` as defined in the header.

The Type Hint array is purely for convenience, as a way to indicate the layout of the vertices. As of such, it may be 
left blank by setting `TypeHintSize` to 0.

Type hints support the basic primitive types:
* UINT (8, 16, 32, 64)
* SINT (8, 16, 32, 64)
* SFLOAT (16, 32, 64)
* UNORM (8, 16)
* SNORM (8, 16)
* USCALED (8, 16)
* SSCALED (8, 16)

These can each be expressed as either scalars or vectors up to 4 components, and be represented using 8, 16, 32, or 64 
bits (floats do not support 8 bits, and norms & scaleds do not support 32 or 64 bits).

A TypeHint can be decoded with the following method:
* The leading nibble packs both the number of bytes for the primitives, and the number of components
  * The leading 2 bits of the nibble represent the number of bytes, where it can be interpreted as the power of two (I.e
    `00` represents single byte (8 bit) components, and `11` represents 8 byte (64 bit) components)
  * The trailing bytes of the nibble represent the number of components - 1 (I.E `00` represents a scalar with a single
    component, and `11` represents a vector with 4 components)
* The trailing nibble maps to each of the supported primitives:
  * `0000`: UINT
  * `0001`: SING
  * `0010`: SFLOAT
  * `0011`: UNORM
  * `0100`: SNORM
  * `0101`: USCALED
  * `0110`: SSCALED

For example, the Type Hint `0b11010010` would be R64G64_SFLOAT:
* The leading nibble: 1101
  * The leading 2 bits `11` = 2^3: 8 bytes (64 bits)
  * The trailing 2 bits `01` = 1 + 1: 2 components
* The trailing nibble: `0010` is the primitive SFloat

## The vertex array:
The vertex array is a continuous stream of bytes exactly the length `vertexCount` multiplied by `vertexSize`, both of
which are defined in the header.

Each vertex is arbitrary data that is expected to be interpreted by the GPU and given form by the Vertex structure
definition in the shaders.

The vertex data can be described by the TypeHint array, however that is not required.

## The index array:
The index array is a continuous stream of indices exactly the length `indexCount`, as defined in the header.
Each index is a 32-bit unsigned integer that corresponds to an index of the `vertexArray`.
```
Header {
    u8[8]   signature   (Expected Value: C2 B1 44 41 54 54 45 58, ±DATMESH)
    u8      version     (Expected Value: 0x01, 1)
    u32     vertexCount
    u32     indexCount
}
```

```
Vec {
    float32 x
    float32 y
    float32 z
}
```

```
Vec2 {
    float32 x
    float32 y
}
```

```
Vertex {
    Vec     position
    float   uv_x
    Vec     normal
    float   uv_y
    Vec     tangent
    Vec     colour
}
```

```
File {
    Header      head
    Vertex[]    vertices    Size = vertexCount
    u32[]       indices     Size = indexCount
}
```

# Description
The File is split into 3 parts:

## The Header
The header contains:
* signature: A 8 byte long magic value to identify the file
* version: The version of the file standard
* vertexCount: The amount of vertices that the file contains
* indexCount: The amount of indices the file contains

## The vertex array:
The vertex array is a continuous stream of Vertices exactly the length `vertexCount` defined in the header

Each vertex is composed of 3 vecs which defined the position, normal, and colour of the vertex, and a vec2d that represents the UV coordinates.
Vecs are defined as three 32-bit floats, each representing a dimensional coordinate.

## The index array:
The index array is a continuous stream of indices exactly the length `indexCount` defined in the header.
Each index is a 32-bit unsigned integer that corresponds to an index of the `vertexArray`.
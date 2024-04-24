namespace dat_asset_handlers.DatMesh {
    public enum DatMeshTypeHint : byte {
        R8UInt = 0, //0b00000000
        R8SInt = 1, //0b00000001
        R8UNorm = 3, //0b00000011
        R8SNorm = 4, //0b00000100
        R8UScaled = 5, //0b00000101
        R8SScaled = 6, //0b00000110
        R8G8UInt = 16, //0b00010000
        R8G8SInt = 17, //0b00010001
        R8G8UNorm = 19, //0b00010011
        R8G8SNorm = 20, //0b00010100
        R8G8UScaled = 21, //0b00010101
        R8G8SScaled = 22, //0b00010110
        R8G8B8UInt = 32, //0b00100000
        R8G8B8SInt = 33, //0b00100001
        R8G8B8UNorm = 35, //0b00100011
        R8G8B8SNorm = 36, //0b00100100
        R8G8B8UScaled = 37, //0b00100101
        R8G8B8SScaled = 38, //0b00100110
        R8G8B8A8UInt = 48, //0b00110000
        R8G8B8A8SInt = 49, //0b00110001
        R8G8B8A8UNorm = 51, //0b00110011
        R8G8B8A8SNorm = 52, //0b00110100
        R8G8B8A8UScaled = 53, //0b00110101
        R8G8B8A8SScaled = 54, //0b00110110
        R16UInt = 64, //0b01000000
        R16SInt = 65, //0b01000001
        R16SFloat = 66, //0b01000010
        R16UNorm = 67, //0b01000011
        R16SNorm = 68, //0b01000100
        R16UScaled = 69, //0b01000101
        R16SScaled = 70, //0b01000110
        R16G16UInt = 80, //0b01010000
        R16G16SInt = 81, //0b01010001
        R16G16SFloat = 82, //0b01010010
        R16G16UNorm = 83, //0b01010011
        R16G16SNorm = 84, //0b01010100
        R16G16UScaled = 85, //0b01010101
        R16G16SScaled = 86, //0b01010110
        R16G16B16UInt = 96, //0b01100000
        R16G16B16SInt = 97, //0b01100001
        R16G16B16SFloat = 98, //0b01100010
        R16G16B16UNorm = 99, //0b01100011
        R16G16B16SNorm = 100, //0b01100100
        R16G16B16UScaled = 101, //0b01100101
        R16G16B16SScaled = 102, //0b01100110
        R16G16B16A16UInt = 112, //0b01110000
        R16G16B16A16SInt = 113, //0b01110001
        R16G16B16A16SFloat = 114, //0b01110010
        R16G16B16A16UNorm = 115, //0b01110011
        R16G16B16A16SNorm = 116, //0b01110100
        R16G16B16A16UScaled = 117, //0b01110101
        R16G16B16A16SScaled = 118, //0b01110110
        R32UInt = 128, //0b10000000
        R32SInt = 129, //0b10000001
        R32SFloat = 130, //0b10000010
        R32G32UInt = 144, //0b10010000
        R32G32SInt = 145, //0b10010001
        R32G32SFloat = 146, //0b10010010
        R32G32B32UInt = 160, //0b10100000
        R32G32B32SInt = 161, //0b10100001
        R32G32B32SFloat = 162, //0b10100010
        R32G32B32A32UInt = 176, //0b10110000
        R32G32B32A32SInt = 177, //0b10110001
        R32G32B32A32SFloat = 178, //0b10110010
        R64UInt = 192, //0b11000000
        R64SInt = 193, //0b11000001
        R64SFloat = 194, //0b11000010
        R64G64UInt = 208, //0b11010000
        R64G64SInt = 209, //0b11010001
        R64G64SFloat = 210, //0b11010010
        R64G64B64UInt = 224, //0b11100000
        R64G64B64SInt = 225, //0b11100001
        R64G64B64SFloat = 226, //0b11100010
        R64G64B64A64UInt = 240, //0b11110000
        R64G64B64A64SInt = 241, //0b11110001
        R64G64B64A64SFloat = 242 //0b11110010
    }
}

namespace EnumExtensions {
    using dat_asset_handlers.DatMesh;

    public static class TypeHintExtensions {
        public static DatMeshTypeHint GetPrimitiveTypeHint(this DatMeshTypeHint datMeshTypeHint) {
            return (DatMeshTypeHint) ((byte) datMeshTypeHint & 0b00001111);
        }

        public static int GetPrimitiveSize(this DatMeshTypeHint datMeshTypeHint) {
            return 1 << ((byte) datMeshTypeHint & 0b11000000 >> 6);
        }

        public static int GetComponentCount(this DatMeshTypeHint datMeshTypeHint) {
            return ((byte) datMeshTypeHint & 0b00110000 >> 4) + 1;
        }
    }
}
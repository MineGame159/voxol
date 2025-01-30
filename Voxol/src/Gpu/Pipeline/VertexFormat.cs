using Silk.NET.Vulkan;

namespace Voxol.Gpu;

public enum VertexAttributeType {
    UByte,
    SByte,
    UInt,
    SInt,
    Float
}

public static class VertexAttributeTypeMethods {
    public static uint GetSize(this VertexAttributeType type) {
        return type switch {
            VertexAttributeType.UByte => 1,
            VertexAttributeType.SByte => 1,
            VertexAttributeType.UInt => 4,
            VertexAttributeType.SInt => 4,
            VertexAttributeType.Float => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}

public readonly record struct VertexAttribute(VertexAttributeType Type, uint Count, bool Normalized) {
    public uint Size => Type.GetSize() * Count;

    public Format Format {
        get {
            if (Type != VertexAttributeType.UByte && Type != VertexAttributeType.SByte && Normalized)
                throw new Exception("Only UByte and SByte vertex attribute types can be normalized");
            
            return Type switch {
                VertexAttributeType.UByte => Count switch {
                    1 => Normalized ? Format.R8Unorm : Format.R8Uint,
                    2 => Normalized ? Format.R8G8Unorm : Format.R8G8Uint,
                    3 => Normalized ? Format.R8G8B8Unorm : Format.R8G8B8Uint,
                    4 => Normalized ? Format.R8G8B8A8Unorm : Format.R8G8B8A8Uint,
                    _ => throw new Exception("Invalid vertex attribute count")
                },
                VertexAttributeType.SByte => Count switch {
                    1 => Normalized ? Format.R8SNorm : Format.R8Sint,
                    2 => Normalized ? Format.R8G8SNorm : Format.R8G8Sint,
                    3 => Normalized ? Format.R8G8B8SNorm : Format.R8G8B8Sint,
                    4 => Normalized ? Format.R8G8B8A8SNorm : Format.R8G8B8A8Sint,
                    _ => throw new Exception("Invalid vertex attribute count")
                },
                VertexAttributeType.UInt => Count switch {
                    1 => Format.R32Uint,
                    2 => Format.R32G32Uint,
                    3 => Format.R32G32B32Uint,
                    4 => Format.R32G32B32A32Uint,
                    _ => throw new Exception("Invalid vertex attribute count")
                },
                VertexAttributeType.SInt => Count switch {
                    1 => Format.R32Sint,
                    2 => Format.R32G32Sint,
                    3 => Format.R32G32B32Sint,
                    4 => Format.R32G32B32A32Sint,
                    _ => throw new Exception("Invalid vertex attribute count")
                },
                VertexAttributeType.Float => Count switch {
                    1 => Format.R32Sfloat,
                    2 => Format.R32G32Sfloat,
                    3 => Format.R32G32B32Sfloat,
                    4 => Format.R32G32B32A32Sfloat,
                    _ => throw new Exception("Invalid vertex attribute count")
                },
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}

public readonly record struct VertexFormat(VertexAttribute[] Attributes) {
    public uint Stride => Attributes.Aggregate(0u, (stride, attribute) => stride + attribute.Size);
}
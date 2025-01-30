using Silk.NET.Vulkan;

namespace Voxol.Gpu.Spirv;

public enum SpirvExecutionModel {
    Vertex = 0,
    TessellationControl = 1,
    TessellationEvaluation = 2,
    Geometry = 3,
    Fragment = 4,
    GlCompute = 5,
    Kernel = 6,
    RayGenerationKhr = 5313,
    IntersectionKhr = 5314,
    AnyHitKhr = 5315,
    ClosestHitKhr = 5316,
    MissKhr = 5317,
    CallableKhr = 5318,
    TaskExt = 5364,
    MeshExt = 5365
}

public static class SpirvExecutionModelExt {
    public static ShaderStageFlags Vk(this SpirvExecutionModel model) {
        return model switch {
            SpirvExecutionModel.Vertex => ShaderStageFlags.VertexBit,
            SpirvExecutionModel.TessellationControl => ShaderStageFlags.TessellationControlBit,
            SpirvExecutionModel.TessellationEvaluation => ShaderStageFlags.TessellationEvaluationBit,
            SpirvExecutionModel.Geometry => ShaderStageFlags.GeometryBit,
            SpirvExecutionModel.Fragment => ShaderStageFlags.FragmentBit,
            SpirvExecutionModel.GlCompute => ShaderStageFlags.ComputeBit,
            SpirvExecutionModel.Kernel => throw new Exception("Invalid SPIRV execution model Kernel"),
            SpirvExecutionModel.RayGenerationKhr => ShaderStageFlags.RaygenBitKhr,
            SpirvExecutionModel.IntersectionKhr => ShaderStageFlags.IntersectionBitKhr,
            SpirvExecutionModel.AnyHitKhr => ShaderStageFlags.AnyHitBitKhr,
            SpirvExecutionModel.ClosestHitKhr => ShaderStageFlags.ClosestHitBitKhr,
            SpirvExecutionModel.MissKhr => ShaderStageFlags.MissBitKhr,
            SpirvExecutionModel.CallableKhr => ShaderStageFlags.CallableBitKhr,
            SpirvExecutionModel.TaskExt => ShaderStageFlags.TaskBitExt,
            SpirvExecutionModel.MeshExt => ShaderStageFlags.MeshBitExt,
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, null)
        };
    }
}

public enum SpirvDecoration {
    RelaxedPrecision = 0,
    SpicId = 1,
    Block = 2,
    BufferBlock = 3,
    RowMajor = 4,
    ColMajor = 5,
    ArrayStride = 6,
    MatrixStride = 7,
    GlslShared = 8,
    GlslPacked = 9,
    CPacked = 10,
    BuiltIn = 11,
    NoPerspective = 13,
    Flat = 14,
    Patch = 15,
    Centroid = 16,
    Sample = 17,
    Invariant = 18,
    Restrict = 19,
    Aliased = 20,
    Volatile = 21,
    Constant = 22,
    Coherent = 23,
    NonWritable = 24,
    NonReadable = 25,
    Uniform = 26,
    SaturatedConversion = 28,
    Stream = 29,
    Location = 30,
    Component = 31,
    Index = 32,
    Binding = 33,
    DescriptorSet = 34,
    Offset = 35
}

public enum SpirvStorageClass {
    UniformConstant = 0,
    Input = 1,
    Uniform = 2,
    Output = 3,
    Workgroup = 4,
    CrossWorkgroup = 5,
    Private = 6,
    Function = 7,
    Generic = 8,
    PushConstant = 9,
    AtomicCounter = 10,
    Image = 11,
    StorageBuffer = 12
}

public enum SpirvDim {
    D1D = 0,
    D2D = 1,
    D3D = 2,
    Cube = 3,
    Rect = 4,
    Buffer = 5,
    SubpassData = 6
}

public enum SpirvImageFormat {
    Unknown = 0,
    Rgba32f = 1,
    Rgba16f = 2,
    R32f = 3,
    Rgba8 = 4,
    Rgba8Snorm = 5,
    Rg32f = 6,
    Rg16f = 7,
    R11fG11fB10f = 8,
    R16f = 9,
    Rgba16 = 10,
    Rgb10A2 = 11,
    Rg16 = 12,
    Rg8 = 13,
    R16 = 14,
    R8 = 15,
    Rgba16Snorm = 16,
    Rg16Snorm = 17,
    Rg8Snorm = 18,
    R16Snorm = 19,
    R8Snorm = 20,
    Rgba32i = 21,
    Rgba16i = 22,
    Rgba8i = 23,
    R32i = 24,
    Rg32i = 25,
    Rg16i = 26,
    Rg8i = 27,
    R16i = 28,
    R8i = 29,
    Rgba32ui = 30,
    Rgba16ui = 31,
    Rgba8ui = 32,
    R32ui = 33,
    Rgb10a2ui = 34,
    Rg32ui = 35,
    Rg16ui = 36,
    Rg8ui = 37,
    R16ui = 38,
    R8ui = 39
}
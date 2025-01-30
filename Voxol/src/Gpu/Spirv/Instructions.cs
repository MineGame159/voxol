using System.Text;

namespace Voxol.Gpu.Spirv;

internal readonly unsafe struct OpUnknown {
    public readonly uint* Data;
    
    public OpUnknown(uint* data) {
        Data = data;
    }

    public uint Type => Data[0] & 0xFFFF;
    public uint WordCount => Data[0] >> 16;
}

internal readonly unsafe struct OpEntryPoint {
    public const uint Id = 15;
    
    public readonly OpUnknown Base;
    
    public OpEntryPoint(OpUnknown @base) {
        Base = @base;
    }

    public SpirvExecutionModel ExecutionModel => (SpirvExecutionModel) Base.Data[1];
    public uint EntryPoint => Base.Data[2];

    public string Name {
        get {
            var bytes = (byte*) &Base.Data[3];

            var length = 0;
            while (bytes[length] != '\0') length++;

            return Encoding.UTF8.GetString(bytes, length);
        }
    }
}

internal readonly unsafe struct OpVariable {
    public const uint Id = 59;
    
    public readonly OpUnknown Base;
    
    public OpVariable(OpUnknown @base) {
        Base = @base;
    }

    public uint ResultType => Base.Data[1];
    public uint Result => Base.Data[2];
    public SpirvStorageClass StorageClass => (SpirvStorageClass) Base.Data[3];
}

// Types

internal readonly unsafe struct OpTypeImage {
    public const uint Id = 25;
    
    public readonly OpUnknown Base;
    
    public OpTypeImage(OpUnknown @base) {
        Base = @base;
    }
    
    public uint Result => Base.Data[1];
    public uint SampledType => Base.Data[2];
    public SpirvDim Dim => (SpirvDim) Base.Data[3];
    public uint Depth => Base.Data[4];
    public uint Arrayed => Base.Data[5];
    public uint Ms => Base.Data[6];
    public uint Sampled => Base.Data[7];
    public SpirvImageFormat Format => (SpirvImageFormat) Base.Data[8];
}

internal readonly unsafe struct OpTypeSampledImage {
    public const uint Id = 27;

    public readonly OpUnknown Base;

    public OpTypeSampledImage(OpUnknown @base) {
        Base = @base;
    }
    
    public uint Result => Base.Data[1];
    public uint Type => Base.Data[2];
}

internal readonly unsafe struct OpTypeRuntimeArray {
    public const uint Id = 29;
    
    public readonly OpUnknown Base;
    
    public OpTypeRuntimeArray(OpUnknown @base) {
        Base = @base;
    }
    
    public uint Result => Base.Data[1];
    public uint Type => Base.Data[2];
}

internal readonly unsafe struct OpTypePointer {
    public const uint Id = 32;
    
    public readonly OpUnknown Base;
    
    public OpTypePointer(OpUnknown @base) {
        Base = @base;
    }
    
    public uint Result => Base.Data[1];
    public SpirvStorageClass StorageClass => (SpirvStorageClass) Base.Data[2];
    public uint Type => Base.Data[3];
}

internal readonly unsafe struct OpDecorate {
    public const uint Id = 71;
    
    public readonly OpUnknown Base;
    
    public OpDecorate(OpUnknown @base) {
        Base = @base;
    }
    
    public uint Target => Base.Data[1];
    public SpirvDecoration Decoration => (SpirvDecoration) Base.Data[2];
    public uint Value => Base.Data[3];
}

internal readonly unsafe struct OpTypeAccelerationStructure {
    public const uint Id = 5341;

    public readonly OpUnknown Base;
    
    public OpTypeAccelerationStructure(OpUnknown @base) {
        Base = @base;
    }
    
    public uint Result => Base.Data[1];
}

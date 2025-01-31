using System.Diagnostics.CodeAnalysis;
using Silk.NET.Vulkan;
using VMASharp;

namespace Voxol.Gpu;

public enum SbtShaderType {
    RayGen,
    Miss,
    Hit,
    Callable
}

public class SbtBuilder {
    private readonly GpuContext ctx;
    private readonly GpuRayTracePipeline pipeline;

    private readonly PhysicalDeviceRayTracingPipelinePropertiesKHR properties;

    private readonly List<int>[] typeRecords;

    public SbtBuilder(GpuContext ctx, GpuRayTracePipeline pipeline) {
        this.ctx = ctx;
        this.pipeline = pipeline;

        unsafe {
            properties.SType = StructureType.PhysicalDeviceRayTracingPipelinePropertiesKhr;

            fixed (PhysicalDeviceRayTracingPipelinePropertiesKHR* properties = &this.properties) {
                var deviceProperties = new PhysicalDeviceProperties2(pNext: properties);
                ctx.Vk.GetPhysicalDeviceProperties2(ctx.PhysicalDevice, &deviceProperties);
            }
        }

        typeRecords = new List<int>[Enum.GetValues<SbtShaderType>().Length];

        for (var i = 0; i < typeRecords.Length; i++) {
            typeRecords[i] = [];
        }
    }

    public void AddRecord(SbtShaderType type, int groupIndex) {
        typeRecords[(int) type].Add(groupIndex);
    }

    public Sbt Build() {
        var offset = 0u;

        var (rayGenOffset, rayGenSize) = CalculateShaderTypeOffset(ref offset, SbtShaderType.RayGen);
        var (missOffset, missSize) = CalculateShaderTypeOffset(ref offset, SbtShaderType.Miss);
        var (hitOffset, hitSize) = CalculateShaderTypeOffset(ref offset, SbtShaderType.Hit);
        var (callableOffset, callableSize) = CalculateShaderTypeOffset(ref offset, SbtShaderType.Callable);

        Span<byte> data = stackalloc byte[(int) offset];

        var groupCount = pipeline.Options.ShaderGroups.Length;

        Span<byte> handles = stackalloc byte[(int) properties.ShaderGroupHandleSize * groupCount];
        ctx.RayTracingApi.GetRayTracingShaderGroupHandles(ctx.Device, pipeline, 0, (uint) groupCount, handles);

        WriteShaderTypeHandles(data, rayGenOffset, handles, SbtShaderType.RayGen);
        WriteShaderTypeHandles(data, missOffset, handles, SbtShaderType.Miss);
        WriteShaderTypeHandles(data, hitOffset, handles, SbtShaderType.Hit);
        WriteShaderTypeHandles(data, callableOffset, handles, SbtShaderType.Callable);

        var (buffer, bufferAddress) = CreateBuffer(data);
        
        var handleAlignedSize = Utils.Align(properties.ShaderGroupHandleSize, properties.ShaderGroupHandleAlignment);

        return new Sbt(
            buffer,
            new StridedDeviceAddressRegionKHR(bufferAddress + rayGenOffset, handleAlignedSize, rayGenSize), 
            new StridedDeviceAddressRegionKHR(bufferAddress + missOffset, handleAlignedSize, missSize), 
            new StridedDeviceAddressRegionKHR(bufferAddress + hitOffset, handleAlignedSize, hitSize), 
            new StridedDeviceAddressRegionKHR(bufferAddress + callableOffset, handleAlignedSize, callableSize) 
        );
    }

    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    private (GpuBuffer, ulong) CreateBuffer(ReadOnlySpan<byte> data) {
        var buffer = ctx.CreateBuffer(
            (ulong) data.Length + properties.ShaderGroupBaseAlignment,
            BufferUsageFlags.ShaderBindingTableBitKhr | BufferUsageFlags.ShaderDeviceAddressBit | BufferUsageFlags.TransferDstBit,
            MemoryUsage.GPU_Only
        );

        var address = Utils.Align(buffer.DeviceAddress, properties.ShaderGroupBaseAlignment);
        var offset = address - buffer.DeviceAddress;
        
        GpuSyncUploads.UploadToBuffer(data, buffer.Sub(offset, (ulong) data.Length));

        return (buffer, address);
    }

    private (uint, uint) CalculateShaderTypeOffset(ref uint offset, SbtShaderType shaderType) {
        var recordCount = (uint) typeRecords[(int) shaderType].Count;
        if (recordCount == 0) return (0, 0);

        var handleAlignedSize = Utils.Align(properties.ShaderGroupHandleSize, properties.ShaderGroupHandleAlignment);

        offset = Utils.Align(offset, properties.ShaderGroupBaseAlignment);
        var typeOffset = offset;
        var typeSize = recordCount * handleAlignedSize;
        offset += typeSize;

        return (typeOffset, typeSize);
    }

    private void WriteShaderTypeHandles(Span<byte> data, uint offset, Span<byte> handles, SbtShaderType shaderType) {
        var records = typeRecords[(int) shaderType];

        foreach (var record in records) {
            offset = Utils.Align(offset, properties.ShaderGroupHandleAlignment);

            var srcStart = record * (int) properties.ShaderGroupHandleSize;
            var src = handles[srcStart .. (srcStart + (int) properties.ShaderGroupHandleSize)];

            var dst = data[(int) offset .. (int) (offset + properties.ShaderGroupHandleSize)];
            src.CopyTo(dst);

            offset += properties.ShaderGroupHandleSize;
        }
    }
}

public readonly record struct Sbt(
    GpuBuffer Buffer,
    StridedDeviceAddressRegionKHR RayGen,
    StridedDeviceAddressRegionKHR Miss,
    StridedDeviceAddressRegionKHR Hit,
    StridedDeviceAddressRegionKHR Callable
);
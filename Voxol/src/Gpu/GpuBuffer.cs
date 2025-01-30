using Silk.NET.Vulkan;
using VMASharp;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Voxol.Gpu;

public class GpuBuffer : IDescriptor, IDisposable {
    public readonly GpuContext Ctx;

    public readonly Buffer Handle;
    public readonly ulong Size;
    public readonly BufferUsageFlags Usage;

    private readonly Allocation allocation;

    public GpuBuffer(GpuContext ctx, Buffer handle, ulong size, BufferUsageFlags usage, Allocation allocation) {
        Ctx = ctx;

        Handle = handle;
        Size = size;
        Usage = usage;
        
        this.allocation = allocation;
    }

    public DescriptorType DescriptorType {
        get {
            if (Usage.HasFlag(BufferUsageFlags.UniformBufferBit)) return DescriptorType.UniformBuffer;
            if (Usage.HasFlag(BufferUsageFlags.StorageBufferBit)) return DescriptorType.StorageBuffer;

            throw new Exception($"Buffer with {Usage} usage cannot be a descriptor");
        }
    }

    public unsafe ulong DeviceAddress => Ctx.Vk.GetBufferDeviceAddress(Ctx.Device, new BufferDeviceAddressInfo(buffer: Handle));

    public GpuSubBuffer Sub(ulong offset, ulong size) => new(this, offset, size);

    public unsafe Span<T> Map<T>() where T : unmanaged {
        return new Span<T>((void*) allocation.Map(), (int) (Size / (ulong) sizeof(T)));
    }

    public void Unmap() {
        allocation.Unmap();
    }

    public void Write<T>(ReadOnlySpan<T> data) where T : unmanaged {
        data.CopyTo(Map<T>());
        Unmap();
    }

    public void Write<T>(ref T data) where T : unmanaged {
        new ReadOnlySpan<T>(ref data).CopyTo(Map<T>());
        Unmap();
    }

    public unsafe void Dispose() {
        Ctx.Vk.DestroyBuffer(Ctx.Device, Handle, null);
        Ctx.Allocator.FreeMemory(allocation);
    }
    
    public static implicit operator Buffer(GpuBuffer buffer) => buffer.Handle;
    
    public static implicit operator GpuSubBuffer(GpuBuffer buffer) => new(buffer, 0, buffer.Size);
}

public readonly record struct GpuSubBuffer(GpuBuffer Buffer, ulong Offset, ulong Size);
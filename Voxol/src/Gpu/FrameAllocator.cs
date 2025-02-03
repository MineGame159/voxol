using Silk.NET.Vulkan;
using VMASharp;

namespace Voxol.Gpu;

public class FrameAllocator {
    private readonly GpuContext ctx;

    private readonly Dictionary<BufferUsageFlags, BumpAllocator> allocators = [];
    
    public FrameAllocator(GpuContext ctx) {
        this.ctx = ctx;
    }
    
    public void NewFrame() {
        foreach (var allocator in allocators.Values) {
            allocator.Free();
        }
    }
    
    public GpuSubBuffer Allocate(BufferUsageFlags usage, ulong size) {
        if (!allocators.TryGetValue(usage, out var allocator)) {
            allocator = new BumpAllocator(ctx, usage);
            allocators[usage] = allocator;
        }
        
        return allocator.Allocate(size);
    }

    public GpuSubBuffer Allocate<T>(BufferUsageFlags usage, ReadOnlySpan<T> data) where T : unmanaged {
        var allocation = Allocate(usage, (ulong) data.Length * Utils.SizeOf<T>());
        allocation.Write(data);
        
        return allocation;
    }

    private class BumpAllocator {
        private readonly GpuContext ctx;
        private readonly BufferUsageFlags usage;

        private readonly List<BumpBuffer> buffers = [];

        public BumpAllocator(GpuContext ctx, BufferUsageFlags usage) {
            this.ctx = ctx;
            this.usage = usage;
        }

        public void Free() {
            for (var i = 0; i < buffers.Count; i++) {
                buffers.Ref(i).Free();
            }
        }

        public GpuSubBuffer Allocate(ulong size) {
            var bestI = -1;
            var bestRemainingSize = ulong.MaxValue;
            
            for (var i = 0; i < buffers.Count; i++) {
                var remainingSize = buffers[i].GetRemainingSizeAfterAllocation(size);

                if (remainingSize != ulong.MaxValue && remainingSize < bestRemainingSize) {
                    bestI = i;
                    bestRemainingSize = remainingSize;
                }
            }

            if (bestI == -1) {
                bestI = buffers.Count;
                
                buffers.Add(new BumpBuffer(ctx.CreateBuffer(
                    Utils.Align(size, 4u * 1024u * 1024u),
                    usage,
                    MemoryUsage.CPU_To_GPU
                )));
            }

            return buffers.Ref(bestI).Allocate(size);
        }
    }

    private struct BumpBuffer {
        private readonly GpuBuffer buffer;

        private ulong size;

        public BumpBuffer(GpuBuffer buffer) {
            this.buffer = buffer;
        }

        public void Free() {
            size = 0;
        }

        public ulong GetRemainingSizeAfterAllocation(ulong size) {
            if (this.size + size > buffer.Size) {
                return ulong.MaxValue;
            }

            return buffer.Size - (this.size + size);
        }

        public GpuSubBuffer Allocate(ulong size) {
            var allocation = buffer.Sub(this.size, size);
            this.size += size;

            return allocation;
        }
    }
}
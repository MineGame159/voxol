using Silk.NET.Vulkan;

namespace Voxol.Gpu;

public class GpuCommandPool {
    private readonly GpuContext ctx;
    private readonly CommandPool pool;

    public unsafe GpuCommandPool(GpuContext ctx) {
        this.ctx = ctx;
        
        Utils.Wrap(
            ctx.Vk.CreateCommandPool(ctx.Device, new CommandPoolCreateInfo(
                queueFamilyIndex: Utils.GetQueueIndices(ctx.Vk, ctx.PhysicalDevice).Graphics!.Value
            ), null, out pool),
            "Failed to create Command Pool"
        );
    }

    public void Reset() {
        Utils.Wrap(
            ctx.Vk.ResetCommandPool(ctx.Device, pool, CommandPoolResetFlags.None),
            "Failed to reset Command Pool"
        );
    }

    public unsafe GpuCommandBuffer Get() {
        Utils.Wrap(
            ctx.Vk.AllocateCommandBuffers(ctx.Device, new CommandBufferAllocateInfo(
                commandPool: pool,
                level: CommandBufferLevel.Primary,
                commandBufferCount: 1
            ), out var handle),
            "Failed to allocate Command Buffer"
        );

        return new GpuCommandBuffer(ctx, handle);
    }
}
using Silk.NET.Vulkan;

namespace Voxol.Gpu;

public abstract class GpuPipeline : IDisposable {
    public readonly GpuContext Ctx;

    public readonly PipelineLayout Layout;
    public readonly Pipeline Handle;

    protected GpuPipeline(GpuContext ctx, PipelineLayout layout, Pipeline handle) {
        Ctx = ctx;
        Layout = layout;
        Handle = handle;
    }

    public abstract PipelineBindPoint BindPoint { get; }

    public void Dispose() {
        unsafe {
            Ctx.Vk.DestroyPipeline(Ctx.Device, Handle, null);
        }

        GC.SuppressFinalize(this);
        
    }

    public static implicit operator Pipeline(GpuPipeline pipeline) => pipeline.Handle;
}
namespace Voxol.Gpu;

public abstract class GpuResource : IDisposable {
    public readonly GpuContext Ctx;

    protected GpuResource(GpuContext ctx) {
        Ctx = ctx;
    }

    public abstract void Dispose();
}
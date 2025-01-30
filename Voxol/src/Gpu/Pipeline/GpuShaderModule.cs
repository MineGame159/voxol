using System.Reflection;

namespace Voxol.Gpu;

public readonly record struct GpuShaderModule {
    private readonly Assembly assembly;
    private readonly string path;

    public GpuShaderModule(Assembly assembly, string path) {
        this.assembly = assembly;
        this.path = path;
    }

    public static GpuShaderModule FromResource(string path) {
        return new GpuShaderModule(Assembly.GetCallingAssembly(), path);
    }

    public byte[] Read() {
        using var stream = assembly.GetManifestResourceStream(path)!;
        
        using var mb = new MemoryStream();
        stream.CopyTo(mb);

        return mb.ToArray();
    }
}
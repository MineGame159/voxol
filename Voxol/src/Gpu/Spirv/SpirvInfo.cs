using Silk.NET.Vulkan;

namespace Voxol.Gpu.Spirv;

public class SpirvInfo {
    public readonly EntryPoint[] EntryPoints;
    public readonly Binding[] Bindings;
    
    public SpirvInfo(EntryPoint[] entryPoints, Binding[] bindings) {
        EntryPoints = entryPoints;
        Bindings = bindings;
    }
    
    public readonly record struct EntryPoint(ShaderStageFlags Stage, string Name);

    public readonly record struct Binding(uint Set, uint Index, DescriptorType Type);
}
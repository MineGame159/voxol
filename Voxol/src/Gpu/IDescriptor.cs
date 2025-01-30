using Silk.NET.Vulkan;

namespace Voxol.Gpu;

public interface IDescriptor {
    public DescriptorType DescriptorType { get; }
}
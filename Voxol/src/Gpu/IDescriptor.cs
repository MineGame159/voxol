using Silk.NET.Vulkan;

namespace Voxol.Gpu;

public interface IDescriptor : IEquatable<IDescriptor?> {
    public DescriptorType DescriptorType { get; }
}
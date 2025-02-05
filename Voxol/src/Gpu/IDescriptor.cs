using Silk.NET.Vulkan;

namespace Voxol.Gpu;

public interface IDescriptor {
    public DescriptorType DescriptorType { get; }

    public bool DescriptorEquals(IDescriptor other);

    public int DescriptorHashCode();
}

public class DescriptorEqualityComparer : EqualityComparer<IDescriptor?> {
    public static readonly DescriptorEqualityComparer Instance = new();

    private DescriptorEqualityComparer() { }

    public override bool Equals(IDescriptor? x, IDescriptor? y) {
        if (x == null && y == null)
            return true;

        if (x == null || y == null)
            return false;

        return x.DescriptorEquals(y);
    }

    public override int GetHashCode(IDescriptor obj) {
        return obj.DescriptorHashCode();
    }
}
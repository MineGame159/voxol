using Silk.NET.Maths;
using Silk.NET.Vulkan;
using VMASharp;

namespace Voxol.Gpu;

public class GpuImage : GpuResource, IDescriptor {
    public readonly Image Handle;
    public readonly Vector2D<uint> Size;
    public readonly ImageUsageFlags Usage;
    public readonly Format Format;

    public readonly ImageView View;

    private readonly Allocation? allocation;

    public ImageLayout Layout;
    
    public GpuImage(GpuContext ctx, Image handle, Vector2D<uint> size, ImageUsageFlags usage, Format format, Allocation? allocation) : base(ctx) {
        Handle = handle;
        Size = size;
        Usage = usage;
        Format = format;

        unsafe {
            Utils.Wrap(Ctx.Vk.CreateImageView(Ctx.Device, new ImageViewCreateInfo(
                image: handle,
                viewType: ImageViewType.Type2D,
                format: format,
                components: new ComponentMapping(),
                subresourceRange: new ImageSubresourceRange(
                    aspectMask: ImageAspectFlags.ColorBit,
                    levelCount: 1,
                    layerCount: 1
                )
            ), null, out View), "Failed to create an Image View");
        }
        
        this.allocation = allocation;

        Layout = ImageLayout.Undefined;
    }

    public DescriptorType DescriptorType => DescriptorType.StorageImage;

    public bool Equals(IDescriptor? other) {
        return ReferenceEquals(this, other);
    }

    public override unsafe void Dispose() {
        Ctx.OnDestroyResource(this);
        
        Ctx.Vk.DestroyImageView(Ctx.Device, View, null);
        
        if (allocation != null) {
            Ctx.Vk.DestroyImage(Ctx.Device, Handle, null);
            Ctx.Allocator.FreeMemory(allocation);
        }
        
        GC.SuppressFinalize(this);
    }

    public static implicit operator Image(GpuImage image) => image.Handle;
    public static implicit operator ImageView(GpuImage image) => image.View;
}

public readonly record struct GpuSamplerImage(GpuImage Image, Sampler Sampler) : IDescriptor {
    public DescriptorType DescriptorType => DescriptorType.CombinedImageSampler;

    public bool Equals(IDescriptor? other) {
        return Equals(this, other);
    }
}
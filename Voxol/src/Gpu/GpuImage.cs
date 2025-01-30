using Silk.NET.Vulkan;
using VMASharp;

namespace Voxol.Gpu;

public class GpuImage : IDescriptor, IDisposable {
    public readonly GpuContext Ctx;

    public readonly Image Handle;
    public readonly uint Width;
    public readonly uint Height;
    public readonly ImageUsageFlags Usage;
    public readonly Format Format;

    public readonly ImageView View;

    private readonly Allocation? allocation;

    public ImageLayout Layout;
    
    public GpuImage(GpuContext ctx, Image handle, uint width, uint height, ImageUsageFlags usage, Format format, Allocation? allocation) {
        Ctx = ctx;

        Handle = handle;
        Width = width;
        Height = height;
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

    public unsafe void Dispose() {
        Ctx.Vk.DestroyImageView(Ctx.Device, View, null);
        
        if (allocation != null) {
            Ctx.Vk.DestroyImage(Ctx.Device, Handle, null);
            Ctx.Allocator.FreeMemory(allocation);
        }
    }

    public static implicit operator Image(GpuImage image) => image.Handle;
    public static implicit operator ImageView(GpuImage image) => image.View;
}

public readonly record struct GpuSamplerImage(GpuImage Image, Sampler Sampler) : IDescriptor {
    public DescriptorType DescriptorType => DescriptorType.CombinedImageSampler;
}
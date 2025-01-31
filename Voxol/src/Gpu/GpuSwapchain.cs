using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Voxol.Gpu;

public class GpuSwapchain {
    private readonly GpuContext ctx;
    private readonly IWindow window;

    public readonly KhrSwapchain Api;

    public SwapchainKHR Handle { get; private set; }
    public GpuImage[] Images { get; private set; } = null!;

    public Action<GpuSwapchain>? Resize;

    public Vector2D<uint> WindowSize { get; private set; }
    public Vector2D<uint> FramebufferSize => Images[0].Size;

    public GpuSwapchain(GpuContext ctx, IWindow window) {
        this.ctx = ctx;
        this.window = window;

        if (!ctx.Vk.TryGetDeviceExtension(ctx.Instance, ctx.Device, out Api))
            throw new Exception("Failed to get Swapchain API");

        Recreate(false);
    }

    public GpuImage? GetNextImage(Semaphore semaphore) {
        var index = 0u;
        var result = Api.AcquireNextImage(ctx.Device, Handle, ulong.MaxValue, semaphore, new Fence(), ref index);

        if (result == Result.ErrorOutOfDateKhr) {
            Recreate(true);
            return null;
        }

        if (result != Result.Success && result != Result.SuboptimalKhr) {
            throw new Exception("Failed to acquire next swapchain image");
        }
        
        return Images[index];
    }

    public unsafe void Present(GpuImage image, Semaphore waitSemaphore) {
        var index = Array.IndexOf(Images, image);

        if (index == -1)
            throw new Exception("Image is not from this Swapchain");

        var handle = Handle;
        
        Api.QueuePresent(ctx.Queue, new PresentInfoKHR(
            waitSemaphoreCount: 1,
            pWaitSemaphores: &waitSemaphore,
            swapchainCount: 1,
            pSwapchains: &handle,
            pImageIndices: (uint*) &index
        ));
    }

    private unsafe void Recreate(bool resize) {
        Utils.Wrap(ctx.Vk.DeviceWaitIdle(ctx.Device), "Failed to wait for device idle");

        if (resize) {
            foreach (var image in Images) {
                image.Dispose();
            }

            Api.DestroySwapchain(ctx.Device, Handle, null);
        }

        var caps = ctx.Surface.Capabilities;

        var surfaceFormat = ChooseSwapSurfaceFormat();
        var extent = ChooseSwapExtent(caps);
        var presentMode = ChoosePresentMode();

        var imageCount = caps.MinImageCount + 1;
        if (caps.MaxImageCount > 0 && imageCount > caps.MaxImageCount)
            imageCount = caps.MaxImageCount;

        var queueIndex = Utils.GetQueueIndices(ctx.Vk, ctx.PhysicalDevice).Graphics!.Value;

        Utils.Wrap(
            Api.CreateSwapchain(ctx.Device, new SwapchainCreateInfoKHR(
                surface: ctx.Surface,
                minImageCount: imageCount,
                imageFormat: surfaceFormat.Format,
                imageColorSpace: surfaceFormat.ColorSpace,
                imageExtent: extent,
                imageArrayLayers: 1,
                imageUsage: ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferDstBit,
                imageSharingMode: SharingMode.Exclusive,
                queueFamilyIndexCount: 1,
                pQueueFamilyIndices: &queueIndex,
                preTransform: caps.CurrentTransform,
                compositeAlpha: CompositeAlphaFlagsKHR.OpaqueBitKhr,
                presentMode: presentMode,
                clipped: true
            ), null, out var handle),
            "Failed to create Swapchain"
        );

        Handle = handle;
        WindowSize = window.Size.As<uint>();

        var count = 0u;
        Api.GetSwapchainImages(ctx.Device, Handle, ref count, null);

        Span<Image> swapchainImages = stackalloc Image[(int) count];
        Api.GetSwapchainImages(ctx.Device, Handle, ref count, Utils.AsPtr(swapchainImages));

        Images = new GpuImage[count];

        for (var i = 0; i < count; i++) {
            Images[i] = new GpuImage(
                ctx,
                swapchainImages[i],
                new Vector2D<uint>(extent.Width, extent.Height), 
                ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferDstBit,
                surfaceFormat.Format,
                null
            );
        }

        if (resize) {
            Resize?.Invoke(this);
        }
    }

    private SurfaceFormatKHR ChooseSwapSurfaceFormat() {
        foreach (var availableFormat in ctx.Surface.Formats) {
            if (availableFormat is { Format: Format.B8G8R8A8Unorm, ColorSpace: ColorSpaceKHR.SpaceSrgbNonlinearKhr })
                return availableFormat;
        }

        return ctx.Surface.Formats[0];
    }

    private PresentModeKHR ChoosePresentMode() {
        foreach (var availablePresentMode in ctx.Surface.PresentModes) {
            if (availablePresentMode == PresentModeKHR.MailboxKhr)
                return availablePresentMode;
        }

        return PresentModeKHR.FifoKhr;
    }

    private Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR caps) {
        if (caps.CurrentExtent.Width != uint.MaxValue)
            return caps.CurrentExtent;

        var framebufferSize = window.FramebufferSize;

        Extent2D actualExtent = new() {
            Width = (uint) framebufferSize.X,
            Height = (uint) framebufferSize.Y
        };

        actualExtent.Width = Math.Clamp(actualExtent.Width, caps.MinImageExtent.Width, caps.MaxImageExtent.Width);
        actualExtent.Height = Math.Clamp(actualExtent.Height, caps.MinImageExtent.Height, caps.MaxImageExtent.Height);

        return actualExtent;
    }

    public static implicit operator SwapchainKHR(GpuSwapchain swapchain) => swapchain.Handle;
}
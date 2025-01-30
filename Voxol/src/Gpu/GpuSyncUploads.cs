using Silk.NET.Vulkan;
using VMASharp;

namespace Voxol.Gpu;

public static class GpuSyncUploads {
    public static void UploadToImage(ReadOnlySpan<byte> pixels, GpuImage image) {
        var buffer = image.Ctx.CreateBuffer(
            (uint) pixels.Length,
            BufferUsageFlags.TransferSrcBit,
            MemoryUsage.CPU_Only
        );
        
        pixels.CopyTo(buffer.Map<byte>());
        buffer.Unmap();

        image.Ctx.Run(commandBuffer => {
            commandBuffer.TransitionImage(
                image,
                ImageLayout.TransferDstOptimal,
                PipelineStageFlags.TopOfPipeBit, AccessFlags.None,
                PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit
            );

            // ReSharper disable once AccessToDisposedClosure
            commandBuffer.CopyBuffer(buffer, image);
            
            commandBuffer.TransitionImage(
                image,
                ImageLayout.ShaderReadOnlyOptimal,
                PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit,
                PipelineStageFlags.FragmentShaderBit, AccessFlags.ShaderReadBit
            );
        });

        buffer.Dispose();
    }
}
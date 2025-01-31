using Silk.NET.Vulkan;
using VMASharp;

namespace Voxol.Gpu;

public static class GpuSyncUploads {
    public static void UploadToBuffer<T>(ReadOnlySpan<T> data, GpuSubBuffer buffer) where T : unmanaged {
        var upload = buffer.Buffer.Ctx.CreateBuffer(
            (ulong) data.Length * Utils.SizeOf<T>(),
            BufferUsageFlags.TransferSrcBit,
            MemoryUsage.CPU_Only
        );
        
        upload.Write(data);
        
        // ReSharper disable once AccessToDisposedClosure
        upload.Ctx.Run(commandBuffer => commandBuffer.CopyBuffer(upload, buffer));
        
        upload.Dispose();
    }
    
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
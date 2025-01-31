using System.Numerics;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace Voxol.Gpu;

public class GpuCommandBuffer {
    public readonly GpuContext Ctx;

    public readonly CommandBuffer Handle;

    private GpuPipeline? boundPipeline;

    public GpuCommandBuffer(GpuContext ctx, CommandBuffer handle) {
        Ctx = ctx;
        Handle = handle;
    }

    public unsafe void Begin() {
        Utils.Wrap(Ctx.Vk.BeginCommandBuffer(Handle, new CommandBufferBeginInfo(
            flags: CommandBufferUsageFlags.OneTimeSubmitBit
        )), "Failed to begin a Command Buffer");
    }

    public void End() {
        Utils.Wrap(Ctx.Vk.EndCommandBuffer(Handle), "Failed to end a Command Buffer");
    }

    public void BindPipeline(GpuPipeline pipeline) {
        Ctx.Vk.CmdBindPipeline(Handle, pipeline.BindPoint, pipeline);
        boundPipeline = pipeline;
    }

    public void BindDescriptorSet(uint index, DescriptorSet set) {
        Ctx.Vk.CmdBindDescriptorSets(Handle, boundPipeline!.BindPoint, boundPipeline!.Layout, index, 1, set, 0, 0);
    }

    public void BindDescriptorSet(uint index, ReadOnlySpan<IDescriptor?> descriptors) {
        var set = Ctx.Descriptors.GetSet(descriptors);
        BindDescriptorSet(index, set);
    }

    public void TraceRays(Sbt sbt, uint width, uint height, uint depth) {
        Ctx.RayTracingApi.CmdTraceRays(Handle, sbt.RayGen, sbt.Miss, sbt.Hit, sbt.Callable, width, height, depth);
    }

    public void CopyBuffer(GpuSubBuffer src, GpuSubBuffer dst) {
        if (src.Size != dst.Size)
            throw new Exception("CopyBuffer - buffers don't have matching size");

        Ctx.Vk.CmdCopyBuffer(Handle, src.Buffer, dst.Buffer, 1, new BufferCopy(
            srcOffset: src.Offset,
            dstOffset: dst.Offset,
            size: src.Size
        ));
    }

    public void CopyBuffer(GpuSubBuffer src, GpuImage dst) {
        Ctx.Vk.CmdCopyBufferToImage(Handle, src.Buffer, dst, ImageLayout.TransferDstOptimal, 1, new BufferImageCopy(
            bufferOffset: src.Offset,
            bufferRowLength: 0,
            bufferImageHeight: 0,
            imageSubresource: new ImageSubresourceLayers(
                ImageAspectFlags.ColorBit,
                layerCount: 1
            ),
            imageOffset: new Offset3D(0, 0, 0),
            imageExtent: new Extent3D(dst.Size.X, dst.Size.Y, 1)
        ));
    }

    public void BlitImage(GpuImage src, GpuImage dst, Filter filter) {
        Ctx.Vk.CmdBlitImage(Handle, src, ImageLayout.General, dst, ImageLayout.General, 1,
            new ImageBlit {
                SrcSubresource = new ImageSubresourceLayers(
                    ImageAspectFlags.ColorBit,
                    layerCount: 1
                ),
                SrcOffsets = new ImageBlit.SrcOffsetsBuffer {
                    Element0 = new Offset3D(),
                    Element1 = new Offset3D((int) src.Size.X, (int) src.Size.Y, 1)
                },
                DstSubresource = new ImageSubresourceLayers(
                    ImageAspectFlags.ColorBit,
                    layerCount: 1
                ),
                DstOffsets = new ImageBlit.DstOffsetsBuffer {
                    Element0 = new Offset3D(),
                    Element1 = new Offset3D((int) dst.Size.X, (int) dst.Size.Y, 1)
                }
            }, filter);
    }

    public unsafe void TransitionImage(
        GpuImage image,
        ImageLayout layout,
        PipelineStageFlags srcStage, AccessFlags srcMask,
        PipelineStageFlags dstStage, AccessFlags dstMask
        ) {
        Ctx.Vk.CmdPipelineBarrier(
            Handle,
            srcStage,
            dstStage,
            DependencyFlags.None,
            0, null,
            0, null,
            1, new ImageMemoryBarrier(
                srcAccessMask: srcMask,
                dstAccessMask: dstMask,
                oldLayout: image.Layout,
                newLayout: layout,
                image: image,
                subresourceRange: new ImageSubresourceRange(
                    aspectMask: ImageAspectFlags.ColorBit,
                    levelCount: 1,
                    layerCount: 1
                )
            )
        );

        image.Layout = layout;
    }

    public unsafe void BeginRenderPass(params ReadOnlySpan<Attachment> colorAttachments) {
        Span<RenderingAttachmentInfo> colorAttachmentsRaw = stackalloc RenderingAttachmentInfo[colorAttachments.Length];

        for (var i = 0; i < colorAttachmentsRaw.Length; i++) {
            var attachment = colorAttachments[i];
            
            colorAttachmentsRaw[i] = new RenderingAttachmentInfo(
                imageView: attachment.Image.View,
                imageLayout: ImageLayout.ColorAttachmentOptimal,
                loadOp: attachment.LoadOp,
                storeOp: attachment.StoreOp
            );

            if (attachment.ClearValue != null) {
                colorAttachmentsRaw[i].ClearValue = new ClearValue(new ClearColorValue(
                    attachment.ClearValue.Value.X,
                    attachment.ClearValue.Value.Y,
                    attachment.ClearValue.Value.Z,
                    attachment.ClearValue.Value.W
                ));
            }
        }
        
        Ctx.Vk.CmdBeginRendering(Handle, new RenderingInfo(
            renderArea: new Rect2D(
                new Offset2D(0, 0),
                new Extent2D(colorAttachments[0].Image.Size.X, colorAttachments[0].Image.Size.Y)
            ),
            layerCount: 1,
            colorAttachmentCount: (uint) colorAttachmentsRaw.Length,
            pColorAttachments: Utils.AsPtr(colorAttachmentsRaw)
        ));
        
        Ctx.Vk.CmdSetViewport(Handle, 0, 1, new Viewport(
            x: 0,
            y: 0,
            width: colorAttachments[0].Image.Size.X,
            height: colorAttachments[0].Image.Size.Y
        ));
    }

    public void EndRenderPass() {
        Ctx.Vk.CmdEndRendering(Handle);
    }

    public void SetScissor(Vector2D<int> min, Vector2D<int> max) {
        Ctx.Vk.CmdSetScissor(Handle, 0, 1, new Rect2D(
            offset: new Offset2D(min.X, min.Y),
            extent: new Extent2D((uint) (max.X - min.X), (uint) (max.Y - min.Y))
        ));
    }

    public void BindVertexBuffer(GpuSubBuffer buffer) {
        Ctx.Vk.CmdBindVertexBuffers(Handle, 0, 1, buffer.Buffer, buffer.Offset);
    }

    public void BindIndexBuffer(GpuSubBuffer buffer, IndexType type) {
        Ctx.Vk.CmdBindIndexBuffer(Handle, buffer.Buffer, buffer.Offset, type);
    }

    public void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int vertexOffset = 0, uint firstInstance = 0) {
        Ctx.Vk.CmdDrawIndexed(Handle, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    public unsafe void BeginGroup(string name) {
        var ptr = SilkMarshal.StringToPtr(name);
        
        Ctx.DebugUtilsApi.CmdBeginDebugUtilsLabel(Handle, new DebugUtilsLabelEXT(
            pLabelName: (byte*) ptr
        ));
        
        SilkMarshal.FreeString(ptr);
    }

    public void EndGroup() {
        Ctx.DebugUtilsApi.CmdEndDebugUtilsLabel(Handle);
    }

    public GpuQuery BeginQuery(PipelineStageFlags stage) {
        var query = Ctx.Queries.GetNext();
        query.Begin(this, stage);

        return query;
    }

    public void EndQuery(GpuQuery query, PipelineStageFlags stage) {
        query.End(this, stage);
    }

    public static implicit operator CommandBuffer(GpuCommandBuffer commandBuffer) => commandBuffer.Handle;
}

public readonly record struct Attachment(GpuImage Image, AttachmentLoadOp LoadOp, AttachmentStoreOp StoreOp, Vector4? ClearValue);
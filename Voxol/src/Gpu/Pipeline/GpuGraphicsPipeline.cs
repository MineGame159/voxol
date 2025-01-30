using Silk.NET.Vulkan;

namespace Voxol.Gpu;

public class GpuGraphicsPipeline : GpuPipeline {
    public readonly GpuGraphicsPipelineOptions Options;

    public GpuGraphicsPipeline(GpuContext ctx, PipelineLayout layout, Pipeline handle, GpuGraphicsPipelineOptions options)
        : base(ctx, layout, handle) {
        Options = options;
    }

    public override PipelineBindPoint BindPoint => PipelineBindPoint.Graphics;
}

public readonly record struct GpuGraphicsPipelineOptions(
    GpuShaderModule VertexShader,
    GpuShaderModule FragmentShader,
    VertexFormat Format,
    ColorAttachment[] ColorAttachments
);

public readonly record struct ColorAttachment(Format Format, bool Blend);
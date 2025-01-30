using Silk.NET.Vulkan;

namespace Voxol.Gpu;

public class GpuRayTracePipeline : GpuPipeline {
    public readonly GpuRayTracePipelineOptions Options;

    public GpuRayTracePipeline(GpuContext ctx, PipelineLayout layout, Pipeline handle, GpuRayTracePipelineOptions options)
        : base(ctx, layout, handle) {
        Options = options;
    }

    public override PipelineBindPoint BindPoint => PipelineBindPoint.RayTracingKhr;
}

public readonly record struct GpuRayTracePipelineOptions(
    GpuShaderModule[] ShaderModules,
    GpuRayTraceGroup[] ShaderGroups,
    uint MaxRecursionDepth
);

public readonly record struct GpuRayTraceGroup(
    RayTracingShaderGroupTypeKHR Type,
    uint? GeneralI = null,
    uint? IntersectionI = null,
    uint? AnyHitI = null,
    uint? ClosestHitI = null
);
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using Silk.NET.Vulkan;
using Voxol.Gpu;

namespace Voxol;

[StructLayout(LayoutKind.Sequential)]
public struct Uniforms {
    public Matrix4x4 Transform;
    public CameraData Camera;
    public bool Shadows;
}

[StructLayout(LayoutKind.Sequential)]
public struct ChunkOutlinesUniforms {
    public Matrix4x4 Transform;
    public Vector4 Color;
}

internal class Program : Application {
    private World world = null!;

    private string[] models = null!;
    private int modelI;
    private int modelResolution = 256;
    private bool voxelize;

    private GpuRayTracePipeline pipeline = null!;
    private Sbt sbt;

    private GpuImage? colorImage;
    private GpuImage? depthStorageImage;

    private GpuGraphicsPipeline depthCopyPipeline = null!;
    private Sampler depthCopySampler;
    private GpuImage? depthImage;

    private GpuGraphicsPipeline chunkOutlinesPipeline = null!;
    private bool chunkOutlines;

    private Camera camera = null!;
    private Uniforms uniforms;

    private readonly Stat<float> fps = new(60);
    private readonly Stat<double> cpuTime = new(60);
    private readonly Stat<double> gpuTime = new(60);

    private GpuQuery? frameQuery;

    protected override void Init() {
        Resize(Ctx.Swapchain);

        world = new World(Ctx);

        models = Directory.EnumerateFiles("scenes")
            .Select(Path.GetFileName)
            .ToArray()!;

        for (var i = 0; i < models.Length; i++) {
            if (models[i].Contains("fantasy_game_inn")) {
                modelI = i;
                break;
            }
        }

        voxelize = true;

        pipeline = Ctx.Pipelines.Create(new GpuRayTracePipelineOptions(
            [
                GpuShaderModule.FromResource("Voxol.shaders.rayGen.spv"),
                GpuShaderModule.FromResource("Voxol.shaders.miss.spv"),
                GpuShaderModule.FromResource("Voxol.shaders.intersection.spv"),
                GpuShaderModule.FromResource("Voxol.shaders.closestHit.spv")
            ],
            [
                new GpuRayTraceGroup(
                    RayTracingShaderGroupTypeKHR.GeneralKhr,
                    GeneralI: 0
                ),
                new GpuRayTraceGroup(
                    RayTracingShaderGroupTypeKHR.GeneralKhr,
                    GeneralI: 1
                ),
                new GpuRayTraceGroup(
                    RayTracingShaderGroupTypeKHR.ProceduralHitGroupKhr,
                    IntersectionI: 2,
                    ClosestHitI: 3
                )
            ],
            1
        ));

        var sbtBuilder = new SbtBuilder(Ctx, pipeline);

        sbtBuilder.AddRecord(SbtShaderType.RayGen, 0);
        sbtBuilder.AddRecord(SbtShaderType.Miss, 1);
        sbtBuilder.AddRecord(SbtShaderType.Hit, 2);

        sbt = sbtBuilder.Build();

        depthCopyPipeline = Ctx.Pipelines.Create(new GpuGraphicsPipelineOptions(
            PrimitiveTopology.TriangleList,
            GpuShaderModule.FromResource("Voxol.shaders.depth_copy.spv"),
            GpuShaderModule.FromResource("Voxol.shaders.depth_copy.spv"),
            new VertexFormat([]),
            [],
            new DepthAttachment(depthImage!.Format, CompareOp.Always, true)
        ));

        unsafe {
            Ctx.Vk.CreateSampler(Ctx.Device, new SamplerCreateInfo(
                magFilter: Filter.Nearest,
                minFilter: Filter.Nearest
            ), null, out depthCopySampler);
        }

        chunkOutlinesPipeline = Ctx.Pipelines.Create(new GpuGraphicsPipelineOptions(
            PrimitiveTopology.LineList,
            GpuShaderModule.FromResource("Voxol.shaders.chunk_outlines.spv"),
            GpuShaderModule.FromResource("Voxol.shaders.chunk_outlines.spv"),
            new VertexFormat([
                new VertexAttribute(VertexAttributeType.Float, 3, false)
            ]),
            [
                new ColorAttachment(colorImage!.Format, true)
            ],
            new DepthAttachment(depthImage!.Format, CompareOp.LessOrEqual, true)
        ));

        camera = new Camera(new Vector3(), 0, 0);
        camera.Pos.X = -256;

        uniforms.Shadows = true;

        Ctx.Swapchain.Resize += Resize;

        // ImGui

        ImGuiImpl.Init(Ctx);

        var style = ImGui.GetStyle();

        style.WindowRounding = 3;
        style.FrameRounding = 3;

        style.Alpha = 0.85f;
    }

    private void Resize(GpuSwapchain swapchain) {
        colorImage?.Dispose();

        colorImage = Ctx.CreateImage(
            swapchain.FramebufferSize,
            ImageUsageFlags.StorageBit | ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit,
            Format.R8G8B8A8Unorm
        );

        depthStorageImage?.Dispose();

        depthStorageImage = Ctx.CreateImage(
            swapchain.FramebufferSize,
            ImageUsageFlags.StorageBit | ImageUsageFlags.SampledBit,
            Format.R32Sfloat
        );

        depthImage?.Dispose();

        depthImage = Ctx.CreateImage(
            swapchain.FramebufferSize,
            ImageUsageFlags.DepthStencilAttachmentBit,
            Format.D32Sfloat
        );
    }

    protected override void Render(float delta, GpuCommandBuffer commandBuffer, GpuImage output) {
        if (voxelize) {
            if (modelResolution < 16)
                modelResolution = 16;

            world.Load("scenes/" + models[modelI], (uint) modelResolution);

            voxelize = false;
        }

        // Stats

        if (frameQuery is { Time.Days: < 1 })
            gpuTime.Add(frameQuery.Time.TotalMilliseconds);

        var sw = Stopwatch.StartNew();
        frameQuery = commandBuffer.BeginQuery(PipelineStageFlags.TopOfPipeBit);

        // Camera

        camera.Move(delta);

        // Scene

        commandBuffer.TransitionImage(
            colorImage!,
            ImageLayout.General,
            PipelineStageFlags.TopOfPipeBit, AccessFlags.None,
            PipelineStageFlags.RayTracingShaderBitKhr, AccessFlags.ShaderWriteBit
        );

        commandBuffer.TransitionImage(
            depthStorageImage!,
            ImageLayout.General,
            PipelineStageFlags.TopOfPipeBit, AccessFlags.None,
            PipelineStageFlags.RayTracingShaderBitKhr, AccessFlags.ShaderWriteBit
        );

        RenderScene(commandBuffer);

        commandBuffer.TransitionImage(
            colorImage!,
            ImageLayout.ColorAttachmentOptimal,
            PipelineStageFlags.RayTracingShaderBitKhr, AccessFlags.ShaderWriteBit,
            PipelineStageFlags.ColorAttachmentOutputBit, AccessFlags.ColorAttachmentReadBit | AccessFlags.ColorAttachmentWriteBit
        );

        if (chunkOutlines) {
            commandBuffer.TransitionImage(
                depthStorageImage!,
                ImageLayout.ShaderReadOnlyOptimal,
                PipelineStageFlags.RayTracingShaderBitKhr, AccessFlags.ShaderWriteBit,
                PipelineStageFlags.FragmentShaderBit, AccessFlags.ShaderReadBit
            );

            commandBuffer.TransitionImage(
                depthImage!,
                ImageLayout.DepthAttachmentOptimal,
                PipelineStageFlags.TopOfPipeBit, AccessFlags.None,
                PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit,
                AccessFlags.DepthStencilAttachmentReadBit | AccessFlags.DepthStencilAttachmentWriteBit
            );

            commandBuffer.BeginGroup("Depth Copy");
            commandBuffer.BeginRenderPass(new Attachment(depthImage!, AttachmentLoadOp.DontCare, AttachmentStoreOp.Store, null));
            commandBuffer.BindPipeline(depthCopyPipeline);
            commandBuffer.BindDescriptorSet(0, [new GpuSamplerImage(depthStorageImage!, depthCopySampler)]);
            commandBuffer.Draw(3);
            commandBuffer.EndRenderPass();
            commandBuffer.EndGroup();

            commandBuffer.TransitionImage(
                depthImage!,
                ImageLayout.DepthAttachmentOptimal,
                PipelineStageFlags.LateFragmentTestsBit, AccessFlags.DepthStencilAttachmentWriteBit,
                PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit,
                AccessFlags.DepthStencilAttachmentReadBit | AccessFlags.DepthStencilAttachmentWriteBit
            );

            RenderChunkOutlines(commandBuffer);
        }

        // GUI

        RenderGui(commandBuffer, delta);

        // Present

        commandBuffer.TransitionImage(
            colorImage!,
            ImageLayout.General,
            PipelineStageFlags.ColorAttachmentOutputBit, AccessFlags.ColorAttachmentWriteBit,
            PipelineStageFlags.TransferBit, AccessFlags.TransferReadBit
        );

        commandBuffer.TransitionImage(
            output,
            ImageLayout.General,
            PipelineStageFlags.BottomOfPipeBit, AccessFlags.None,
            PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit
        );

        commandBuffer.BlitImage(colorImage!, output, Filter.Nearest);

        commandBuffer.TransitionImage(
            output,
            ImageLayout.PresentSrcKhr,
            PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit,
            PipelineStageFlags.BottomOfPipeBit, AccessFlags.None
        );

        // Stats

        commandBuffer.EndQuery(frameQuery, PipelineStageFlags.BottomOfPipeBit);
        cpuTime.Add(sw.Elapsed.TotalMilliseconds);
    }

    private void RenderScene(GpuCommandBuffer commandBuffer) {
        uniforms.Transform = camera.GetView() * Utils.CreatePerspective(70, colorImage!.Size, 0.01f, 8192);
        uniforms.Camera = camera.GetData(Ctx.Swapchain.FramebufferSize.X, Ctx.Swapchain.FramebufferSize.Y);

        var uniformBuffer = Ctx.FrameAllocator.Allocate(BufferUsageFlags.UniformBufferBit, uniforms);

        commandBuffer.BeginGroup("Scene");

        commandBuffer.BindPipeline(pipeline);
        commandBuffer.BindDescriptorSet(0, [
            uniformBuffer,
            world.TopAccelStruct,
            world.ChunkBuffer,
            world.BrickBuffer,
            world.VoxelBuffer,
            colorImage,
            depthStorageImage
        ]);
        commandBuffer.TraceRays(sbt, colorImage!.Size.X, colorImage.Size.Y, 1);

        commandBuffer.EndGroup();
    }

    private void RenderChunkOutlines(GpuCommandBuffer commandBuffer) {
        // Geometry

        var vertexBuffer = Ctx.FrameAllocator.Allocate(
            BufferUsageFlags.VertexBufferBit,
            (ulong) world.ChunkBoxes.Count * 8 * Utils.SizeOf<Vector3>()
        );

        var indexBuffer = Ctx.FrameAllocator.Allocate(
            BufferUsageFlags.IndexBufferBit,
            (ulong) world.ChunkBoxes.Count * 12 * 2 * Utils.SizeOf<uint>()
        );

        var vertices = vertexBuffer.Map<Vector3>();
        var indices = indexBuffer.Map<uint>();

        var vertexI = 0u;
        var indexI = 0;

        foreach (var box in world.ChunkBoxes) {
            vertices[(int) vertexI + 0] = new Vector3(box.Min.X, box.Min.Y, box.Min.Z);
            vertices[(int) vertexI + 1] = new Vector3(box.Min.X, box.Min.Y, box.Max.Z);
            vertices[(int) vertexI + 2] = new Vector3(box.Max.X, box.Min.Y, box.Max.Z);
            vertices[(int) vertexI + 3] = new Vector3(box.Max.X, box.Min.Y, box.Min.Z);

            vertices[(int) vertexI + 4] = new Vector3(box.Min.X, box.Max.Y, box.Min.Z);
            vertices[(int) vertexI + 5] = new Vector3(box.Min.X, box.Max.Y, box.Max.Z);
            vertices[(int) vertexI + 6] = new Vector3(box.Max.X, box.Max.Y, box.Max.Z);
            vertices[(int) vertexI + 7] = new Vector3(box.Max.X, box.Max.Y, box.Min.Z);

            Line(indices, 0, 1);
            Line(indices, 1, 2);
            Line(indices, 2, 3);
            Line(indices, 3, 0);

            Line(indices, 4, 5);
            Line(indices, 5, 6);
            Line(indices, 6, 7);
            Line(indices, 7, 4);

            Line(indices, 0, 4);
            Line(indices, 1, 5);
            Line(indices, 2, 6);
            Line(indices, 3, 7);

            vertexI += 8;
        }

        vertexBuffer.Unmap();
        indexBuffer.Unmap();

        // Uniforms

        var uniformBuffer = Ctx.FrameAllocator.Allocate(BufferUsageFlags.UniformBufferBit, new ChunkOutlinesUniforms {
            Transform = camera.GetView() * Utils.CreatePerspective(70, colorImage!.Size, 0.01f, 8192),
            Color = new Vector4(1)
        });

        // Commands

        commandBuffer.BeginGroup("Chunk Outlines");
        commandBuffer.BeginRenderPass(
            new Attachment(colorImage!, AttachmentLoadOp.Load, AttachmentStoreOp.Store, null),
            new Attachment(depthImage!, AttachmentLoadOp.Load, AttachmentStoreOp.Store, null)
        );

        commandBuffer.BindPipeline(chunkOutlinesPipeline);
        commandBuffer.BindVertexBuffer(vertexBuffer);
        commandBuffer.BindIndexBuffer(indexBuffer, IndexType.Uint32);
        commandBuffer.BindDescriptorSet(0, [uniformBuffer]);
        commandBuffer.DrawIndexed((uint) (indexBuffer.Size / Utils.SizeOf<uint>()));

        commandBuffer.EndRenderPass();
        commandBuffer.EndGroup();

        return;

        void Line(Span<uint> indices, uint v0, uint v1) {
            indices[indexI++] = vertexI + v0;
            indices[indexI++] = vertexI + v1;
        }
    }

    private void RenderGui(GpuCommandBuffer commandBuffer, float delta) {
        commandBuffer.BeginGroup("Gui");
        ImGuiImpl.BeginFrame(delta);

        fps.Add(1 / delta);

        if (ImGui.Begin("Voxol", ImGuiWindowFlags.AlwaysAutoResize)) {
            DisplayStat("FPS", "", fps, f => f);
            DisplayStat("CPU Time", "ms", cpuTime, d => (float) d);
            DisplayStat("GPU Time", "ms", gpuTime, d => (float) d);

            ImGui.Separator();

            ImGui.Text($"Chunks: {world.ChunkBuffer.Size / Utils.SizeOf<Chunk>()}");
            ImGui.Text($"   Accel Structs: {Utils.FormatBytes(world.AccelStructBytes)}");
            ImGui.Text($"   Chunks: {Utils.FormatBytes(world.ChunkBuffer.Size)}");
            ImGui.Text($"   Bricks: {Utils.FormatBytes(world.BrickBuffer.Size)}");
            ImGui.Text($"   Voxels: {Utils.FormatBytes(world.VoxelBuffer.Size)}");

            ImGui.Separator();

            ImGui.Checkbox("Shadows", ref uniforms.Shadows);
            ImGui.Checkbox("Chunk Outlines", ref chunkOutlines);

            ImGui.Separator();

            ImGui.Combo("Model", ref modelI, models, models.Length);
            ImGui.InputInt("Resolution", ref modelResolution, 256);

            if (ImGui.Button("Voxelize", new Vector2(ImGui.CalcItemWidth(), 0)))
                voxelize = true;
        }

        ImGui.End();

        ImGuiImpl.EndFrame(commandBuffer, colorImage!);
        commandBuffer.EndGroup();
    }

    private static void DisplayStat<T>(string name, string unit, Stat<T> stat, Func<T, float> converter)
        where T : INumber<T>, IMinMaxValue<T> {
        ImGui.Text($"{name}: {stat.Avg:F2} ({stat.Min:F2}, {stat.Max:F2}) {unit}");

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.DelayNormal)) {
            Span<float> floats = stackalloc float[stat.Size];
            stat.GetHistoricData(floats, converter);

            ImGui.BeginTooltip();
            ImGui.PlotHistogram(
                "##" + name,
                ref floats.GetPinnableReference(),
                floats.Length,
                0,
                "",
                0,
                Math.Max(float.CreateSaturating(stat.Max), 1f / 60f * TimeSpan.MillisecondsPerSecond),
                new Vector2(stat.Size * 4, stat.Size)
            );
            ImGui.EndTooltip();
        }
    }

    public static void Main() {
        var app = new Program();
        app.Run();
    }
}
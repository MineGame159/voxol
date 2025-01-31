using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using Obj2Voxel;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using VMASharp;
using Voxol.Gpu;

namespace Voxol;

[StructLayout(LayoutKind.Sequential)]
public struct Voxel {
    public byte X, Y, Z;
    private byte _0;

    public byte R, G, B;
    private byte _1;
}

[StructLayout(LayoutKind.Sequential)]
public struct Uniforms {
    public CameraData Camera;
    public bool Shadows;
}

internal class Program : Application {
    private static GpuRayTracePipeline pipeline = null!;
    private static Sbt sbt;
    
    private static GpuBuffer uniformBuffer = null!;
    private static GpuAccelStruct topAccelStruct = null!;
    private static GpuBuffer chunkVoxelIndexBuffer = null!;
    private static GpuBuffer voxelBuffer = null!;
    private static GpuImage? image;

    private static Camera camera = null!;
    private static Uniforms uniforms;

    private static readonly Stat<float> fps = new(60);
    private static readonly Stat<double> cpuTime = new(60);
    private static readonly Stat<double> gpuTime = new(60);

    private static GpuQuery? frameQuery;

    private static uint chunkCount;
    private static ulong accelStructBytes;
    private static ulong voxelBytes;

    protected override void Init() {
        Resize(Ctx.Swapchain);

        var vox = new Voxelizer();
        vox.Resolution = 256;

        var loader = new GltfLoader();
        loader.Load("scenes/fantasy_game_inn.glb");
        vox.InputCallback = loader.GetNextTriangle;

        Dictionary<Vector3D<uint>, List<Voxel>> chunks = [];

        vox.OutputCallback = voxels => {
            foreach (var voxel in voxels) {
                var chunkPos = voxel.Pos / 256;

                if (!chunks.TryGetValue(chunkPos, out var chunk)) {
                    chunk = [];
                    chunks[chunkPos] = chunk;
                }

                chunk.Add(new Voxel {
                    X = (byte) (voxel.Pos.X % 256),
                    Y = (byte) (voxel.Pos.Y % 256),
                    Z = (byte) (voxel.Pos.Z % 256),
                    R = voxel.Color.X,
                    G = voxel.Color.Y,
                    B = voxel.Color.Z
                });
            }

            return true;
        };

        var sw = Stopwatch.StartNew();
        vox.Voxelize();
        Console.WriteLine("Voxelized in " + sw.Elapsed);

        loader.Dispose();
        vox.Dispose();

        var chunkInstances = new List<AccelerationStructureInstanceKHR>(chunks.Count);
        var chunkVoxelIndices = new List<uint>(chunks.Count);
        var voxelI = 0;

        var scratchBuffer = default(GpuBuffer?);
        var aabbs = new List<Box3D<float>>();

        chunkCount = (uint) chunks.Count;

        foreach (var (chunkPos, chunk) in chunks) {
            aabbs.EnsureCapacity(chunk.Count);

            foreach (var pos in chunk) {
                aabbs.Add(new Box3D<float>(pos.X, pos.Y, pos.Z, pos.X + 1, pos.Y + 1, pos.Z + 1));
            }

            var accelStruct = Ctx.CreateAccelStruct(CollectionsMarshal.AsSpan(aabbs), true, ref scratchBuffer);
            aabbs.Clear();

            accelStructBytes += accelStruct.Buffer.Size;

            var transform = new TransformMatrixKHR();
            transform.Load(Matrix4x4.CreateTranslation(chunkPos.As<float>().ToSystem() * 256));

            chunkInstances.Add(new AccelerationStructureInstanceKHR(
                transform: transform,
                mask: 0xFF,
                accelerationStructureReference: accelStruct.DeviceAddress,
                instanceCustomIndex: (uint) chunkInstances.Count
            ));

            chunkVoxelIndices.Add((uint) voxelI);

            voxelI += chunk.Count;
        }

        var voxels = new Voxel[chunks.Sum(pair => pair.Value.Count)];
        voxelI = 0;

        foreach (var chunk in chunks.Values) {
            foreach (var voxel in chunk) {
                voxels[voxelI++] = voxel;
            }
        }

        chunkVoxelIndexBuffer = Ctx.CreateStaticBuffer(CollectionsMarshal.AsSpan(chunkVoxelIndices), BufferUsageFlags.StorageBufferBit);
        voxelBuffer = Ctx.CreateStaticBuffer(voxels.AsSpan(), BufferUsageFlags.StorageBufferBit);

        voxelBytes = voxelBuffer.Size;

        topAccelStruct = Ctx.CreateAccelStruct(CollectionsMarshal.AsSpan(chunkInstances), false, ref scratchBuffer);
        scratchBuffer?.Dispose();

        accelStructBytes += topAccelStruct.Buffer.Size;

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

        uniformBuffer = Ctx.CreateBuffer(
            Utils.SizeOf<Uniforms>(),
            BufferUsageFlags.UniformBufferBit,
            MemoryUsage.CPU_To_GPU
        );

        camera = new Camera(new Vector3(), 0, 0);

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
        image?.Dispose();
        
        image = Ctx.CreateImage(
            swapchain.FramebufferSize,
            ImageUsageFlags.StorageBit | ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit,
            Format.R8G8B8A8Unorm
        );
    }

    protected override void Render(float delta, GpuCommandBuffer commandBuffer, GpuImage output) {
        // Stats

        if (frameQuery is { Time.Days: < 1 })
            gpuTime.Add(frameQuery.Time.TotalMilliseconds);

        var sw = Stopwatch.StartNew();
        frameQuery = commandBuffer.BeginQuery(PipelineStageFlags.TopOfPipeBit);

        // Uniforms

        camera.Move(delta);
        
        uniforms.Camera = camera.GetData(Ctx.Swapchain.FramebufferSize.X, Ctx.Swapchain.FramebufferSize.Y);
        uniformBuffer.Write(ref uniforms);

        // Scene

        commandBuffer.TransitionImage(
            image!,
            ImageLayout.General,
            PipelineStageFlags.TopOfPipeBit, AccessFlags.None,
            PipelineStageFlags.RayTracingShaderBitKhr, AccessFlags.ShaderWriteBit
        );

        commandBuffer.TransitionImage(
            output,
            ImageLayout.General,
            PipelineStageFlags.TopOfPipeBit, AccessFlags.None,
            PipelineStageFlags.TransferBit, AccessFlags.TransferWriteBit
        );

        RenderScene(commandBuffer);

        commandBuffer.TransitionImage(
            image!,
            ImageLayout.General,
            PipelineStageFlags.RayTracingShaderBitKhr, AccessFlags.ShaderWriteBit,
            PipelineStageFlags.ColorAttachmentOutputBit, AccessFlags.ColorAttachmentWriteBit
        );

        // GUI

        RenderGui(commandBuffer, delta);

        commandBuffer.TransitionImage(
            image!,
            ImageLayout.General,
            PipelineStageFlags.ColorAttachmentOutputBit, AccessFlags.ColorAttachmentWriteBit,
            PipelineStageFlags.TransferBit, AccessFlags.TransferReadBit
        );

        // Present

        commandBuffer.BlitImage(image!, output, Filter.Nearest);

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
        commandBuffer.BeginGroup("Scene");

        commandBuffer.BindPipeline(pipeline);
        commandBuffer.BindDescriptorSet(0, [
            uniformBuffer,
            topAccelStruct,
            chunkVoxelIndexBuffer,
            voxelBuffer,
            image
        ]);
        commandBuffer.TraceRays(sbt, image!.Size.X, image.Size.Y, 1);

        commandBuffer.EndGroup();
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

            ImGui.Text($"Chunks: {chunkCount}");
            ImGui.Text($"   Accel Structs: {Utils.FormatBytes(accelStructBytes)}");
            ImGui.Text($"   Voxels: {Utils.FormatBytes(voxelBytes)}");
            
            ImGui.Separator();

            ImGui.Checkbox("Shadows", ref uniforms.Shadows);
        }

        ImGui.End();

        ImGuiImpl.EndFrame(commandBuffer, image!);
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
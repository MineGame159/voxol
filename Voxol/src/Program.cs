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
public struct Chunk {
    public uint X, Y, Z;
    public uint BrickBase;
    public uint BrickCount;

    public Vector3D<uint> Pos {
        get => new(X, Y, Z);
        set {
            X = value.X;
            Y = value.Y;
            Z = value.Z;
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct Brick {
    public byte MinX, MinY, MinZ;
    private byte _0;

    private byte MaxX, MaxY, MaxZ;
    private byte _1;

    public uint VoxelBase;
    
    public unsafe fixed uint Mask[16];

    public Vector3D<byte> Min {
        get => new(MinX, MinY, MinZ);
        set {
            MinX = value.X;
            MinY = value.Y;
            MinZ = value.Z;
        }
    }

    public Vector3D<byte> Max {
        get => new(MaxX, MaxY, MaxZ);
        set {
            MaxX = value.X;
            MaxY = value.Y;
            MaxZ = value.Z;
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct Voxel {
    public Vector3D<byte> Color;
}

public struct ChunkVoxel {
    public Vector3D<byte> Pos;
    public Vector3D<byte> Color;
}

[StructLayout(LayoutKind.Sequential)]
public struct Uniforms {
    public CameraData Camera;
    public bool Shadows;
}

internal class Program : Application {
    private GpuRayTracePipeline pipeline = null!;
    private Sbt sbt;

    private GpuBuffer uniformBuffer = null!;
    private GpuAccelStruct topAccelStruct = null!;
    private GpuBuffer chunkBuffer = null!;
    private GpuBuffer brickBuffer = null!;
    private GpuBuffer voxelBuffer = null!;
    private GpuImage? image;

    private ulong accelStructBytes;

    private Camera camera = null!;
    private Uniforms uniforms;

    private readonly Stat<float> fps = new(60);
    private readonly Stat<double> cpuTime = new(60);
    private readonly Stat<double> gpuTime = new(60);

    private GpuQuery? frameQuery;

    protected override void Init() {
        Resize(Ctx.Swapchain);

        var chunkVoxels = LoadChunkVoxels("scenes/fantasy_game_inn.glb", 256);
        var (chunks, bricks, voxels) = ConvertChunkVoxels(chunkVoxels);

        CreateAccelStruct(chunks, bricks);

        chunkBuffer = Ctx.CreateStaticBuffer(
            CollectionsMarshal.AsSpan(chunks),
            BufferUsageFlags.StorageBufferBit
        );
        brickBuffer = Ctx.CreateStaticBuffer(
            CollectionsMarshal.AsSpan(bricks),
            BufferUsageFlags.StorageBufferBit
        );
        voxelBuffer = Ctx.CreateStaticBuffer(
            CollectionsMarshal.AsSpan(voxels),
            BufferUsageFlags.StorageBufferBit
        );

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

    private static Dictionary<Vector3D<uint>, List<ChunkVoxel>> LoadChunkVoxels(string path, uint resolution) {
        var vox = new Voxelizer();
        vox.Resolution = resolution;

        var loader = new GltfLoader();
        loader.Load(path);
        vox.InputCallback = loader.GetNextTriangle;

        var chunks = new Dictionary<Vector3D<uint>, List<ChunkVoxel>>();

        vox.OutputCallback = voxels => {
            foreach (var voxel in voxels) {
                var chunkPos = voxel.Pos / 256;

                if (!chunks.TryGetValue(chunkPos, out var chunk)) {
                    chunk = [];
                    chunks[chunkPos] = chunk;
                }

                chunk.Add(new ChunkVoxel {
                    Pos = voxel.Pos.Mod(256u).As<byte>(),
                    Color = voxel.Color.To3()
                });
            }

            return true;
        };

        var sw = Stopwatch.StartNew();
        vox.Voxelize();
        Console.WriteLine("Voxelized in " + Utils.FormatDuration(sw.Elapsed));

        loader.Dispose();
        vox.Dispose();

        return chunks;
    }

    private static (List<Chunk>, List<Brick>, List<Voxel>) ConvertChunkVoxels(Dictionary<Vector3D<uint>, List<ChunkVoxel>> chunkVoxels) {
        var sw = Stopwatch.StartNew();
        
        var chunks = new List<Chunk>(chunkVoxels.Count);
        var bricks = new List<Brick>();
        var voxels = new List<Voxel>();

        var chunkBricks = new int[32 * 32 * 32];

        foreach (var (chunkPos, chunkVoxels2) in chunkVoxels) {
            // Chunk

            chunks.Add(new Chunk {
                Pos = chunkPos,
                BrickBase = (uint) bricks.Count,
                BrickCount = 0
            });

            ref var chunk = ref CollectionsMarshal.AsSpan(chunks)[chunks.Count - 1];

            // Voxels

            Array.Fill(chunkBricks, -1);

            foreach (var chunkVoxel in chunkVoxels2) {
                // Brick

                var brickPos = chunkVoxel.Pos / 8;
                ref var brickI = ref chunkBricks[brickPos.X + 32 * (brickPos.Y + 32 * brickPos.Z)];

                if (brickI == -1) {
                    chunk.BrickCount++;

                    brickI = bricks.Count;

                    bricks.Add(new Brick {
                        Min = new Vector3D<byte>(byte.MaxValue),
                        Max = new Vector3D<byte>(byte.MinValue),
                        //Min = brickPos * 8,
                        //Max = brickPos * 8 + new Vector3D<byte>(7),
                        VoxelBase = (uint) voxels.Count
                    });

                    voxels.EnsureCapacity(voxels.Count + 8 * 8 * 8);
                    voxels.AddRange(Enumerable.Repeat(new Voxel(), 8 * 8 * 8));
                }

                ref var brick = ref CollectionsMarshal.AsSpan(bricks)[brickI];

                // Voxel
                
                SetVoxel(ref brick, chunkVoxel.Pos, chunkVoxel.Color);
            }
        }
        
        Console.WriteLine("Converted chunks in " + Utils.FormatDuration(sw.Elapsed));

        return (chunks, bricks, voxels);

        void SetVoxel(ref Brick brick, Vector3D<byte> pos, Vector3D<byte> color) {
            brick.Min = Vector3D.Min(brick.Min, pos);
            brick.Max = Vector3D.Max(brick.Max, pos);

            var voxelPos = pos.Mod((byte) 8);
            var voxelI = voxelPos.X + 8 * (voxelPos.Y + 8 * voxelPos.Z);

            unsafe {
                brick.Mask[voxelI / 32] |= 1u << (voxelI % 32);
            }

            CollectionsMarshal.AsSpan(voxels)[(int) brick.VoxelBase + voxelI].Color = color;
        }
    }

    private void CreateAccelStruct(List<Chunk> chunks, List<Brick> bricks) {
        var sw = Stopwatch.StartNew();
        
        var chunkInstances = new List<AccelerationStructureInstanceKHR>(chunks.Count);

        var scratchBuffer = default(GpuBuffer?);
        var aabbs = new List<Box3D<float>>();

        foreach (var chunk in chunks) {
            aabbs.EnsureCapacity((int) chunk.BrickCount);

            for (var i = 0u; i < chunk.BrickCount; i++) {
                var brick = bricks[(int) (chunk.BrickBase + i)];
                aabbs.Add(new Box3D<float>(brick.Min.As<float>(), brick.Max.As<float>() + Vector3D<float>.One));
            }

            var accelStruct = Ctx.CreateAccelStruct(CollectionsMarshal.AsSpan(aabbs), true, ref scratchBuffer);
            aabbs.Clear();

            accelStructBytes += accelStruct.Buffer.Size;

            var transform = new TransformMatrixKHR();
            
            // ReSharper disable once PossiblyImpureMethodCallOnReadonlyVariable
            transform.Load(Matrix4x4.CreateTranslation(chunk.Pos.As<float>().ToSystem() * 256));

            chunkInstances.Add(new AccelerationStructureInstanceKHR(
                transform: transform,
                mask: 0xFF,
                accelerationStructureReference: accelStruct.DeviceAddress,
                instanceCustomIndex: (uint) chunkInstances.Count
            ));
        }

        topAccelStruct = Ctx.CreateAccelStruct(CollectionsMarshal.AsSpan(chunkInstances), false, ref scratchBuffer);
        scratchBuffer?.Dispose();

        accelStructBytes += topAccelStruct.Buffer.Size;
        
        Console.WriteLine("Created acceleration structures in " + Utils.FormatDuration(sw.Elapsed));
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
            chunkBuffer,
            brickBuffer,
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

            ImGui.Text($"Chunks: {chunkBuffer.Size / Utils.SizeOf<Chunk>()}");
            ImGui.Text($"   Accel Structs: {Utils.FormatBytes(accelStructBytes)}");
            ImGui.Text($"   Chunks: {Utils.FormatBytes(chunkBuffer.Size)}");
            ImGui.Text($"   Bricks: {Utils.FormatBytes(brickBuffer.Size)}");
            ImGui.Text($"   Voxels: {Utils.FormatBytes(voxelBuffer.Size)}");

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
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Obj2Voxel;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
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

public class World {
    private readonly GpuContext ctx;

    public readonly List<Box3D<float>> ChunkBoxes = [];

    public GpuAccelStruct TopAccelStruct = null!;
    public GpuBuffer ChunkBuffer = null!;
    public GpuBuffer BrickBuffer = null!;
    public GpuBuffer VoxelBuffer = null!;

    public ulong AccelStructBytes;

    public World(GpuContext ctx) {
        this.ctx = ctx;
    }

    public void Load(string path, uint resolution) {
        if (ChunkBoxes.Count > 0) {
            TopAccelStruct.Dispose();
            ChunkBuffer.Dispose();
            BrickBuffer.Dispose();
            VoxelBuffer.Dispose();
        }
        
        var chunkVoxels = LoadChunkVoxels(path, resolution);
        var (chunks, bricks, voxels) = ConvertChunkVoxels(chunkVoxels);

        CreateChunkBoxes(chunks, bricks);
        CreateAccelStruct(chunks, bricks);

        ChunkBuffer = ctx.CreateStaticBuffer(
            CollectionsMarshal.AsSpan(chunks),
            BufferUsageFlags.StorageBufferBit
        );
        BrickBuffer = ctx.CreateStaticBuffer(
            CollectionsMarshal.AsSpan(bricks),
            BufferUsageFlags.StorageBufferBit
        );
        VoxelBuffer = ctx.CreateStaticBuffer(
            CollectionsMarshal.AsSpan(voxels),
            BufferUsageFlags.StorageBufferBit
        );
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

        var chunks = new List<Chunk>();
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

    private void CreateChunkBoxes(List<Chunk> chunks, List<Brick> bricks) {
        ChunkBoxes.Clear();

        foreach (var chunk in chunks) {
            var box = new Box3D<float>(
                new Vector3D<float>(float.MaxValue),
                new Vector3D<float>(float.MinValue)
            );

            for (var i = 0; i < chunk.BrickCount; i++) {
                ref var brick = ref bricks.Ref((int) chunk.BrickBase + i);

                box.Expand(new Box3D<float>(
                    brick.Min.As<float>(),
                    brick.Max.As<float>() + Vector3D<float>.One
                ));
            }

            box = box.GetTranslated(chunk.Pos.As<float>() * 256);

            ChunkBoxes.Add(box);
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

            var accelStruct = ctx.CreateAccelStruct(CollectionsMarshal.AsSpan(aabbs), true, ref scratchBuffer);
            aabbs.Clear();

            AccelStructBytes += accelStruct.Buffer.Size;

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

        TopAccelStruct = ctx.CreateAccelStruct(CollectionsMarshal.AsSpan(chunkInstances), false, ref scratchBuffer);
        scratchBuffer?.Dispose();

        AccelStructBytes += TopAccelStruct.Buffer.Size;

        Console.WriteLine("Created acceleration structures in " + Utils.FormatDuration(sw.Elapsed));
    }
}
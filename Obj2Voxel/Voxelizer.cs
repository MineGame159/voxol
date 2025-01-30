using System.Runtime.InteropServices;

namespace Obj2Voxel;

public delegate bool TriangleFn(VoxTriangle triangle);

public delegate bool VoxelFn(ReadOnlySpan<Voxel> voxels);

public class Voxelizer : IDisposable {
    private static TriangleFn triangleFn = null!;
    private static VoxelFn voxelFn = null!;
    
    private readonly IntPtr handle;

    public Voxelizer() {
        handle = Native.Alloc();
        Native.SetParallel(handle, 1);
    }

    public uint Resolution {
        set => Native.SetResolution(handle, value);
    }

    public TriangleFn InputCallback {
        set {
            unsafe {
                triangleFn = value;
                Native.SetInputCallback(handle, &TriangleFnWrapper, null);
            }
        }
    }

    public VoxelFn OutputCallback {
        set {
            unsafe {
                voxelFn = value;
                Native.SetOutputCallback(handle, &VoxelFnWrapper, null);
            }
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe byte TriangleFnWrapper(void* data, IntPtr triangle) {
        return triangleFn(new VoxTriangle(triangle)) ? (byte) 1 : (byte) 0;
    }

    [UnmanagedCallersOnly]
    private static unsafe byte VoxelFnWrapper(void* data, uint* voxels, nuint count) {
        if (count != 0)
            return voxelFn(new ReadOnlySpan<Voxel>(voxels, (int) count)) ? (byte) 1 : (byte) 0;

        return 1;
    }

    public Error Voxelize(int threadCount = -1) {
        if (threadCount == -1)
            threadCount = Math.Max(1, Environment.ProcessorCount - 2);
        
        for (var i = 0; i < threadCount; i++) {
            var worker = new Thread(() => Native.RunWorker(handle)) {
                Name = $"Obj2Voxel - Worker {i}"
            };

            worker.Start();
        }
        
        var result = (Error) Native.Voxelize(handle);
        
        Native.StopWorkers(handle);
        
        return result;
    }

    public void Dispose() {
        Native.Free(handle);
        GC.SuppressFinalize(this);
    }
}
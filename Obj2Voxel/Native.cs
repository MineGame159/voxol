using System.Runtime.InteropServices;

namespace Obj2Voxel;

internal static unsafe partial class Native {
    public const string LibName = "obj2voxel";

    // Instance

    [LibraryImport(LibName, EntryPoint = "obj2voxel_alloc")]
    public static partial IntPtr Alloc();

    [LibraryImport(LibName, EntryPoint = "obj2voxel_free")]
    public static partial void Free(IntPtr instance);

    [LibraryImport(LibName, EntryPoint = "obj2voxel_set_resolution")]
    public static partial void SetResolution(IntPtr instance, uint resolution);

    [LibraryImport(LibName, EntryPoint = "obj2voxel_set_input_callback")]
    public static partial void SetInputCallback(IntPtr instance, delegate* unmanaged<void*, IntPtr, byte> callback, void* data);

    [LibraryImport(LibName, EntryPoint = "obj2voxel_set_output_callback")]
    public static partial void SetOutputCallback(IntPtr instance, delegate* unmanaged<void*, uint*, nuint, byte> callback, void* data);

    [LibraryImport(LibName, EntryPoint = "obj2voxel_set_parallel")]
    public static partial void SetParallel(IntPtr instance, byte enabled);

    [LibraryImport(LibName, EntryPoint = "obj2voxel_voxelize")]
    public static partial byte Voxelize(IntPtr instance);

    // Texture

    [LibraryImport(LibName, EntryPoint = "obj2voxel_texture_alloc")]
    public static partial IntPtr TextureAlloc();

    [LibraryImport(LibName, EntryPoint = "obj2voxel_texture_free")]
    public static partial void TextureFree(IntPtr handle);

    [LibraryImport(LibName, EntryPoint = "obj2voxel_texture_load_from_memory")]
    public static partial byte TextureLoadFromMemory(IntPtr handle, byte* data, nuint size, byte* type);

    [LibraryImport(LibName, EntryPoint = "obj2voxel_texture_load_pixels")]
    public static partial byte TextureLoadPixels(IntPtr handle, byte* pixels, nuint width, nuint height, nuint channels);

    // Triangle

    [LibraryImport(LibName, EntryPoint = "obj2voxel_set_triangle_basic")]
    public static partial void SetTriangleBasic(IntPtr handle, float* vertices);

    [LibraryImport(LibName, EntryPoint = "obj2voxel_set_triangle_colored")]
    public static partial void SetTriangleColored(IntPtr handle, float* vertices, float* color);

    [LibraryImport(LibName, EntryPoint = "obj2voxel_set_triangle_textured")]
    public static partial void SetTriangleTextured(IntPtr handle, float* vertices, float* uvs, IntPtr texture);

    // Workers

    [LibraryImport(LibName, EntryPoint = "obj2voxel_run_worker")]
    public static partial void RunWorker(IntPtr handle);

    [LibraryImport(LibName, EntryPoint = "obj2voxel_stop_workers")]
    public static partial void StopWorkers(IntPtr handle);
}
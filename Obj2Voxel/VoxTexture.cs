using System.Runtime.InteropServices;

namespace Obj2Voxel;

public readonly struct VoxTexture : IDisposable {
    internal readonly IntPtr Handle;

    public VoxTexture() {
        Handle = Native.TextureAlloc();
    }

    public void Dispose() {
        Native.TextureFree(Handle);
    }

    public bool LoadFromMemory(ReadOnlySpan<byte> data, string? type = null) {
        unsafe {
            var typePtr = (byte*) Marshal.StringToHGlobalAuto(type);
            bool ok;
            
            fixed (byte* dataPtr = data) {
                ok = Native.TextureLoadFromMemory(
                    Handle,
                    dataPtr,
                    (nuint) data.Length,
                    typePtr
                ) != 0;
            }

            Marshal.FreeHGlobal((IntPtr) typePtr);
            return ok;
        }
    }

    public bool LoadPixels(ReadOnlySpan<byte> pixels, uint width, uint height, uint channels) {
        unsafe {
            fixed (byte* pixelsPtr = pixels) {
                return Native.TextureLoadPixels(Handle, pixelsPtr, width, height, channels) != 0;
            }
        }
    }
}
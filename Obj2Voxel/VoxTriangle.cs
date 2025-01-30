using System.Numerics;

namespace Obj2Voxel;

public readonly struct VoxTriangle {
    internal readonly IntPtr Handle;

    internal VoxTriangle(IntPtr handle) {
        Handle = handle;
    }

    public void Set(Vector3 v0, Vector3 v1, Vector3 v2) {
        unsafe {
            var vertices = stackalloc Vector3[3];
            vertices[0] = v0;
            vertices[1] = v1;
            vertices[2] = v2;
            
            Native.SetTriangleBasic(Handle, (float*) vertices);
        }
    }

    public void Set(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 color) {
        unsafe {
            var vertices = stackalloc Vector3[3];
            vertices[0] = v0;
            vertices[1] = v1;
            vertices[2] = v2;
            
            Native.SetTriangleColored(Handle, (float*) vertices, (float*) &color);
        }
    }

    public void Set(Vector3 v0, Vector3 v1, Vector3 v2, Vector2 uv0, Vector2 uv1, Vector2 uv2, VoxTexture texture) {
        unsafe {
            var vertices = stackalloc Vector3[3];
            vertices[0] = v0;
            vertices[1] = v1;
            vertices[2] = v2;

            var uvs = stackalloc Vector2[3];
            uvs[0] = uv0;
            uvs[1] = uv1;
            uvs[2] = uv2;
            
            Native.SetTriangleTextured(Handle, (float*) vertices, (float*) uvs, texture.Handle);
        }
    }
}
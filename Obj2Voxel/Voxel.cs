using System.Runtime.InteropServices;
using Silk.NET.Maths;

namespace Obj2Voxel;

[StructLayout(LayoutKind.Sequential)]
public readonly struct Voxel {
    public readonly Vector3D<uint> Pos;

    private readonly byte b, g, r, a;
    public Vector4D<byte> Color => new(r, g, b, a);
}
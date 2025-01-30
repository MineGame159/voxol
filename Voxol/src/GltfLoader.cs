using System.Numerics;
using System.Runtime.InteropServices;
using Obj2Voxel;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;
using Silk.NET.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace Voxol;

public class GltfLoader : IDisposable {
    public readonly record struct GltfTriangle(
        Vector3 V0,
        Vector3 V1,
        Vector3 V2,
        Vector2 Uv0,
        Vector2 Uv1,
        Vector2 Uv2,
        VoxTexture Texture
    );

    private readonly Dictionary<Texture, VoxTexture> textures = [];

    private IEnumerator<GltfTriangle> triangles = null!;

    public void Load(string path) {
        var root = ModelRoot.Load(path, new ReadSettings {
            Validation = ValidationMode.Skip
        });
        
        triangles = GetTriangles(root);
    }

    public void Dispose() {
        foreach (var texture in textures.Values) {
            texture.Dispose();
        }
        
        GC.SuppressFinalize(this);
    }

    private IEnumerator<GltfTriangle> GetTriangles(ModelRoot root) {
        List<Node> nodes = [];
        nodes.AddRange(root.DefaultScene.VisualChildren);

        while (nodes.Count > 0) {
            var node = nodes[^1];
            nodes.RemoveAt(nodes.Count - 1);

            if (node.Mesh != null) {
                var transform = node.WorldMatrix;

                foreach (var primitive in node.Mesh.Primitives) {
                    if (primitive.DrawPrimitiveType != PrimitiveType.TRIANGLES)
                        continue;

                    var channel = primitive.Material.FindChannel("BaseColor")!.Value;
                    var texture = GetTexture(channel);

                    var indices = primitive.IndexAccessor.AsIndicesArray();
                    var positions = primitive.GetVertexAccessor("POSITION").AsVector3Array();
                    var uvs = primitive.GetVertexAccessor($"TEXCOORD_{channel.TextureCoordinate}").AsVector2Array();

                    for (var i = 0; i < indices.Count; i += 3) {
                        var i0 = (int) indices[i + 0];
                        var i1 = (int) indices[i + 1];
                        var i2 = (int) indices[i + 2];

                        var v0 = Vector3.Transform(positions[i0], transform);
                        var v1 = Vector3.Transform(positions[i1], transform);
                        var v2 = Vector3.Transform(positions[i2], transform);

                        var uv0 = uvs[i0];
                        var uv1 = uvs[i1];
                        var uv2 = uvs[i2];

                        yield return new GltfTriangle(v0, v1, v2, uv0, uv1, uv2, texture);
                    }
                }
            }

            nodes.AddRange(node.VisualChildren);
        }
    }

    private VoxTexture GetTexture(MaterialChannel channel) {
        var texture = channel.Texture;

        if (texture == null) {
            var color = (channel.Color * 255).ToGeneric().As<byte>();
            Span<byte> pixels = [ color.X, color.Y, color.Z ];

            var voxTexture2 = new VoxTexture();
            voxTexture2.LoadPixels(pixels, 1, 1, 3);

            return voxTexture2;
        }

        if (!textures.TryGetValue(texture, out var voxTexture)) {
            var content = texture.PrimaryImage.Content;
            
            var conf = Configuration.Default.Clone();
            conf.PreferContiguousImageBuffers = true;

            var image = Image.Load<Rgb24>(new DecoderOptions {
                Configuration = conf
            }, content.Open());
            
            image.Mutate(ctx => ctx.Flip(FlipMode.Vertical));

            if (!image.DangerousTryGetSinglePixelMemory(out var memory))
                throw new Exception("Image is not contiguous");

            voxTexture = new VoxTexture();
            voxTexture.LoadPixels(MemoryMarshal.Cast<Rgb24, byte>(memory.Span), (uint) image.Width, (uint) image.Height, 3);

            textures[texture] = voxTexture;
        }
        
        return voxTexture;
    }

    public bool GetNextTriangle(VoxTriangle triangle) {
        if (!triangles.MoveNext())
            return false;

        var t = triangles.Current;
        triangle.Set(t.V0, t.V1, t.V2, t.Uv0, t.Uv1, t.Uv2, t.Texture);

        return true;
    }
}
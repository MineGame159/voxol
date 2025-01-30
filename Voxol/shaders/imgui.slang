struct VertexIn {
    float2 pos;
    float2 uv;
    float4 color;
}

struct VertexOut {
    float4 pos : SV_Position;
    float2 uv;
    float4 color;
}

struct Uniforms {
    float2 scale;
    float2 translate;
};

uniform Uniforms u;
uniform Sampler2D texture;

[shader("vertex")]
VertexOut vertex(VertexIn in) {
    VertexOut out;

    out.pos = float4(in.pos * u.scale + u.translate, 1.0);
    out.uv = in.uv;
    out.color = in.color;

    return out;
}

[shader("fragment")]
float4 fragment(VertexOut in) {
    return texture.Sample(in.uv) * in.color;
}

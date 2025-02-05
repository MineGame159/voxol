struct VertexOut {
    float4 pos : SV_Position;
    float2 uv;
}

struct FragmentOut {
    float depth : SV_Depth;
}

uniform Sampler2D<float> depth;

[shader("vertex")]
VertexOut vertex(uint index : SV_VertexID) {
    VertexOut out;

    out.uv = float2((index << 1) & 2, index & 2);
    out.pos = float4(out.uv * 2.0f + -1.0f, 0.0f, 1.0f);

    return out;
}

[shader("fragment")]
FragmentOut fragment(float2 uv) {
    FragmentOut out;

    out.depth = depth.Sample(uv);

    return out;
}

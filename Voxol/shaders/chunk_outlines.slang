struct VertexIn {
    float3 pos;
}

struct VertexOut {
    float4 pos : SV_Position;
}

struct Uniforms {
    float4x4 transform;
    float4 color;
}

uniform Uniforms u;

[shader("vertex")]
VertexOut vertex(VertexIn in) {
    VertexOut out;

    out.pos = mul(u.transform, float4(in.pos, 1.0));

    return out;
}

struct FragOut {
    float4 color : SV_Target;
}

[shader("fragment")]
FragOut fragment() {
    FragOut out;

    out.color = u.color;

    return out;
}

struct Uniforms {
    float4 origin;
    float4 lowerLeftCorner;
    float4 horizontal;
    float4 vertical;

    bool shadows;
}

struct Voxel {
    uint _pos;
    uint _color;

    property float3 min {
        get {
            return float3(_pos & 0xFF, (_pos >> 8) & 0xFF, (_pos >> 16) & 0xFF);
        }
    }

    property float3 max {
        get {
            return min + 1;
        }
    }

    property float3 color {
        get {
            return float3(_color & 0xFF, (_color >> 8) & 0xFF, (_color >> 16) & 0xFF) / 255.0;
        }
    }
}

uniform Uniforms u;

uniform RaytracingAccelerationStructure accelStruct;

uniform StructuredBuffer<uint> chunkVoxelIndices;

uniform StructuredBuffer<Voxel> voxels;

[format("rgba8")]
uniform RWTexture2D image;

struct RayPayload {
    float t;
    float4 color;
}

struct HitAttributes {
    float t;
    float3 color;
}

[shader("raygeneration")]
void RayGen() {
    let pos = DispatchRaysIndex().xy;
    let size = DispatchRaysDimensions().xy;

    let uv = float2(pos) / size;

    RayDesc ray;
    ray.Origin = u.origin.xyz;
    ray.Direction = u.lowerLeftCorner.xyz + uv.x * u.horizontal.xyz + uv.y * u.vertical.xyz - u.origin.xyz;
    ray.TMin = 0.0;
    ray.TMax = float.maxValue;

    RayPayload payload;

    TraceRay(accelStruct, RAY_FLAG_NONE, 0xFF, 0, 0, 0, ray, payload);
    var color = payload.color;

    if (u.shadows && payload.t >= 0) {
        ray.Origin = ray.Origin + ray.Direction * (payload.t - 0.001);
        ray.Direction = normalize(float3(-0.45, 1.0, -0.3));

        TraceRay(accelStruct, RAY_FLAG_NONE, 0xFF, 0, 0, 0, ray, payload);

        if (payload.t >= 0) {
            color *= 0.5;
        }
    }

    image[int2(pos.x, size.y - pos.y)] = color;
}

[shader("miss")]
void Miss(inout RayPayload payload) {
    payload.t = -1;
    payload.color = float4(GetBackground(WorldRayDirection()), 1.0);
}

[shader("intersection")]
void Intersection() {
    let chunkVoxelIndex = chunkVoxelIndices[InstanceID()];
    let voxel = voxels[chunkVoxelIndex + PrimitiveIndex()];

    let nearFar = Intersect(voxel);

    if (nearFar.x < nearFar.y) {
        HitAttributes attributes;
        attributes.t = nearFar.x;
        attributes.color = voxel.color;

        ReportHit(attributes.t, 0, attributes);
    }
}

float2 Intersect(Voxel voxel) {
    let rayOrigin = ObjectRayOrigin();
    let rayDir = ObjectRayDirection();

    let tMin = (voxel.min - rayOrigin) / rayDir;
    let tMax = (voxel.max - rayOrigin) / rayDir;

    let t1 = min(tMin, tMax);
    let t2 = max(tMin, tMax);

    let tNear = max(max(t1.x, t1.y), t1.z);
    let tFar = min(min(t2.x, t2.y), t2.z);

    return float2(tNear, tFar);
}

[shader("closesthit")]
void ClosestHit(inout RayPayload payload, in HitAttributes attributes) {
    payload.t = attributes.t;
    payload.color = float4(attributes.color, 1.0);
}

float3 GetBackground(float3 direction) {
    var t = 0.5 * (direction.y + 1);
    return (1 - t) * float3(1) + t * float3(0.5, 0.7, 1);
}

struct Uniforms {
    float4 origin;
    float4 lowerLeftCorner;
    float4 horizontal;
    float4 vertical;

    bool shadows;
}

struct Chunk {
    uint x, y, z;
    uint brickBase;
    uint brickCount;
}

struct Brick {
    uint8_t minX, minY, minZ, _0;
    uint8_t maxX, maxY, maxZ, _1;

    uint voxelBase;

    uint mask[16];

    property uint3 posU {
        get {
            return uint3(minX, minY, minZ) >> 3u;
        }
    }

    property uint3 minU {
        get {
            return uint3(minX, minY, minZ);
        }
    }

    property float3 minF {
        get {
            return float3(minX, minY, minZ);
        }
    }

    property uint3 maxU {
        get {
            return uint3(maxX, maxY, maxZ) + 1;
        }
    }

    property float3 maxF {
        get {
            return float3(maxX, maxY, maxZ) + 1;
        }
    }
}

struct Voxel {
    uint8_t r, g, b;

    property float3 color {
        get {
            return float3(r, g, b) / 255.0;
        }
    }
}

uniform Uniforms u;

uniform RaytracingAccelerationStructure accelStruct;

uniform StructuredBuffer<Chunk> chunks;

uniform StructuredBuffer<Brick> bricks;

uniform StructuredBuffer<Voxel> voxels;

[format("rgba8")]
uniform RWTexture2D image;

struct RayPayload {
    float t;
    float4 color;
}

struct HitAttributes {
    float t;
    uint voxelI;
}

static const let EPS = 0.001;

[shader("raygeneration")]
void RayGen() {
    let pos = DispatchRaysIndex().xy;
    let size = DispatchRaysDimensions().xy;

    let uv = float2(pos) / size;

    RayDesc ray;
    ray.Origin = u.origin.xyz;
    ray.Direction = fixDirection(u.lowerLeftCorner.xyz + uv.x * u.horizontal.xyz + uv.y * u.vertical.xyz - u.origin.xyz);
    ray.TMin = 0.0;
    ray.TMax = float.maxValue;

    RayPayload payload;

    TraceRay(accelStruct, RAY_FLAG_NONE, 0xFF, 0, 0, 0, ray, payload);
    var color = payload.color;

    if (u.shadows && payload.t >= 0) {
        ray.Origin = ray.Origin + ray.Direction * (payload.t - EPS);
        ray.Direction = normalize(float3(-0.45, 1.0, -0.3));

        TraceRay(accelStruct, RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH, 0xFF, 0, 0, 0, ray, payload);

        if (payload.t >= 0) {
            color *= 0.5;
        }
    }

    image[int2(pos.x, size.y - pos.y - 1)] = color;
}

float3 fixDirection(float3 v) {
    if (v.x == 0.0) v.x = EPS;
    if (v.y == 0.0) v.y = EPS;
    if (v.z == 0.0) v.z = EPS;

    return normalize(v);
}

[shader("miss")]
void Miss(inout RayPayload payload) {
    payload.t = -1;
    payload.color = float4(GetBackground(WorldRayDirection()), 1.0);
}

[shader("intersection")]
void Intersection() {
    // Get brick intersection

    let chunk = chunks[InstanceID()];
    let brick = bricks[chunk.brickBase + PrimitiveIndex()];

    let invDir = 1.0 / ObjectRayDirection();
    var brickT = brick.Intersect(invDir);

    if (brickT < 0)
        return;

    // Get voxel intersection

    let rayPos = ObjectRayOrigin() + ObjectRayDirection() * brickT - brick.posU * 8;
    var mapPos = clamp(floor(rayPos), 0, 7);
    let raySign = float3(sign(ObjectRayDirection()));
    var sideDist = ((mapPos - rayPos) + 0.5 + raySign * 0.5) * invDir;
    let mapMax = brick.maxF - brick.posU * 8 - 1;

    for (int i = 0; i < 32; i++) {
        let pos = uint3(mapPos);
        let voxelI = uint(pos.x + 8u * (pos.y + 8u * pos.z));

        if (brick.IsVoxelSet(voxelI)) {
            let mini = ((mapPos - rayPos) + 0.5 - 0.5 * raySign) * invDir;
            let voxelT = max(mini.x, max(mini.y, mini.z));

            HitAttributes attrs;
            attrs.t = brickT + voxelT;
            attrs.voxelI = brick.voxelBase + voxelI;

            ReportHit(attrs.t, 0, attrs);
            return;
        }

        let mask = float3(sideDist.xyz <= min(sideDist.yzx, sideDist.zxy));
        mapPos += mask * raySign;
        sideDist += mask * raySign * invDir;

        if (any(mapPos < 0) || any(mapPos > mapMax)) {
            return;
        }
    }
}

extension Brick {
    float Intersect(float3 invDir) {
        let rayOrigin = ObjectRayOrigin();

        let tMin = (minF - rayOrigin) * invDir;
        let tMax = (maxF - rayOrigin) * invDir;

        let t1 = min(tMin, tMax);
        let t2 = max(tMin, tMax);

        let tNear = max(max(t1.x, t1.y), t1.z);
        let tFar = min(min(t2.x, t2.y), t2.z);

        if (tNear > tFar)
            return -1;

        if (tNear < 0)
            return 0;

        return tNear;
    }

    bool IsVoxelSet(uint voxelI) {
        let voxelMask = 1u << (voxelI % 32u);
        let mask = this.mask[voxelI >> 5u];

        return (mask & voxelMask) != 0u;
    }
}

[shader("closesthit")]
void ClosestHit(inout RayPayload payload, in HitAttributes attrs) {
    payload.t = attrs.t;
    payload.color = float4(voxels[attrs.voxelI].color, 1.0);
}

float3 GetBackground(float3 direction) {
    var t = 0.5 * (direction.y + 1);
    return (1 - t) * float3(1) + t * float3(0.5, 0.7, 1);
}

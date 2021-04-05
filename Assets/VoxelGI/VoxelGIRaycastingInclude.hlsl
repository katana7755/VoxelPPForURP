#ifndef VOXEL_GI_RAYCASTING_INCLUDE
#define VOXEL_GI_RAYCASTING_INCLUDE
#define EPSILON 1.0e-4

#if SSBO_OCTREE_ONLY_CHECK_INSIDE_MODE // Only check inside
struct OctreeNode
{
    uint childNodeIndices[8];
};

StructuredBuffer<float4> COLOR_VOLUME_BUFFER;
StructuredBuffer<OctreeNode> OCTREE_BUFFER;
uint VOLUME_SIZE;
float VOXEL_SIZE;
float3 MAIN_CAMERA_WORLD_POS;

struct OctreeStackItem
{
    uint index;
    uint volumeSize;
    uint3 indexMin;
    uint3 parentIndexMin;
};

int GetVolumeLinearIndex(int3 index)
{
    return index.x * VOLUME_SIZE * VOLUME_SIZE + index.y * VOLUME_SIZE + index.z;
}

bool IsInVolume(float3 pos)
{
    float3 boundMin = MAIN_CAMERA_WORLD_POS - (VOLUME_SIZE >> 1) * VOXEL_SIZE;

    if (pos.x < boundMin.x || pos.y < boundMin.y || pos.z < boundMin.z)
    {
        return false;
    }

    float3 boundMax = boundMin + VOLUME_SIZE * VOXEL_SIZE;

    if (pos.x > boundMax.x || pos.y > boundMax.y || pos.z > boundMax.z)
    {
        return false;
    }

    return true;
}

void RaycastAndCheckHitOnLeaf(float3 RayOrigin, float3 RayDir, uint index, uint3 indexMin, inout float minDist, inout float4 OutColor, uint3 parentIndexMin)
{
    float4 volumeColor = COLOR_VOLUME_BUFFER[index];

    if (volumeColor.a < EPSILON)
    {
        return;
    }

    float3 boundMin = MAIN_CAMERA_WORLD_POS + indexMin * VOXEL_SIZE - (VOLUME_SIZE >> 1) * VOXEL_SIZE;
    float3 boundMax = boundMin + VOXEL_SIZE;
    float3 t0 = (boundMin - RayOrigin) / RayDir;
    float3 t1 = (boundMax - RayOrigin) / RayDir;
    float3 tMin = min(t0, t1);
    float3 tMax = max(t0, t1);
    float dstA = max(max(tMin.x, tMin.y), tMin.z);
    float dstB = min(min(tMax.x, tMax.y), tMax.z);

    if (dstA > EPSILON || dstB < EPSILON || dstA >= dstB)
    {
        return;
    }

    if (minDist > EPSILON && minDist < dstB)
    {
        return;
    }

    minDist = dstB;
    OutColor = volumeColor;
}

bool RaycastAndCheckHitOnNode(float3 RayOrigin, float3 RayDir, uint3 indexMin, uint volumeSize, inout float nodeMinDistance)
{
    float3 boundMin = MAIN_CAMERA_WORLD_POS + indexMin * VOXEL_SIZE - (VOLUME_SIZE >> 1) * VOXEL_SIZE;
    float3 boundMax = boundMin + volumeSize * VOXEL_SIZE;
    float3 t0 = (boundMin - RayOrigin) / RayDir;
    float3 t1 = (boundMax - RayOrigin) / RayDir;
    float3 tMin = min(t0, t1);
    float3 tMax = max(t0, t1);
    float dstA = max(max(tMin.x, tMin.y), tMin.z);
    float dstB = min(min(tMax.x, tMax.y), tMax.z);

    if (dstA > EPSILON || dstB < EPSILON || dstA >= dstB)
    {
        return false;
    }

    if (nodeMinDistance < EPSILON || nodeMinDistance > dstB)
    {
        nodeMinDistance = dstB;
    }

    return true;
}

void Raycast_float(float3 RayOrigin, float3 RayDir, float MaxDistance, out float4 OutColor, out float Distance)
{
    OutColor = float4(0.0, 0.0, 0.0, 0.0);
    Distance = -1.0;

    OctreeStackItem stack[128];
    uint stackCount = 0;

    OctreeStackItem item;
    item.index = 0;
    item.volumeSize = VOLUME_SIZE;
    item.indexMin = uint3(0, 0, 0);
    item.parentIndexMin = item.indexMin;
    stack[stackCount] = item;
    ++stackCount;

    uint debugStep = 0;
    float nodeMinDistance = -1.0;

    while (stackCount > 0)
    {
        ++debugStep;

        if (debugStep >= 1000)
        {
            OutColor = float4(0, 0, 1, 1);
            break;
        }

        item = stack[stackCount - 1];
        --stackCount;

        if (item.volumeSize == 1)
        {
            RaycastAndCheckHitOnLeaf(RayOrigin, RayDir, item.index, item.indexMin, Distance, OutColor, item.parentIndexMin);
            continue;
        }

        if (!RaycastAndCheckHitOnNode(RayOrigin, RayDir, item.indexMin, item.volumeSize, nodeMinDistance))
        {
            continue;
        }

        OctreeNode parentNode = OCTREE_BUFFER[item.index];
        uint volumeSize = item.volumeSize >> 1;

        [unroll]
        for (int i = 7; i >= 0; --i)
        {
            if ((volumeSize != 1 && parentNode.childNodeIndices[i] == 0) || (parentNode.childNodeIndices[i] == 0xFFFFFFFF))
            {
                continue;
            }

            OctreeStackItem newItem;
            newItem.index = parentNode.childNodeIndices[i];
            newItem.volumeSize = volumeSize;
            newItem.indexMin = item.indexMin + uint3((i & 0x4) >> 2, (i & 0x2) >> 1, (i & 0x1)) * volumeSize;
            newItem.parentIndexMin = item.indexMin;
            stack[stackCount] = newItem;
            ++stackCount;
        }
    }

    if (Distance < EPSILON)
    {
        Distance = nodeMinDistance;
    }
}
#elif SSBO_OCTREE_CHECK_ACTUAL_HIT // Check actual hit
struct OctreeNode
{
    uint childNodeIndices[8];
};

StructuredBuffer<float4> COLOR_VOLUME_BUFFER;
StructuredBuffer<OctreeNode> OCTREE_BUFFER;
uint VOLUME_SIZE;
float VOXEL_SIZE;
float3 MAIN_CAMERA_WORLD_POS;

struct OctreeStackItem
{
    uint index;
    uint volumeSize;
    uint3 indexMin;
    uint3 parentIndexMin;
};

int GetVolumeLinearIndex(int3 index)
{
    return index.x * VOLUME_SIZE * VOLUME_SIZE + index.y * VOLUME_SIZE + index.z;
}

bool IsInVolume(float3 pos)
{
    float3 boundMin = MAIN_CAMERA_WORLD_POS - (VOLUME_SIZE >> 1) * VOXEL_SIZE;

    if (pos.x < boundMin.x || pos.y < boundMin.y || pos.z < boundMin.z)
    {
        return false;
    }

    float3 boundMax = boundMin + VOLUME_SIZE * VOXEL_SIZE;

    if (pos.x > boundMax.x || pos.y > boundMax.y || pos.z > boundMax.z)
    {
        return false;
    }

    return true;
}

void RaycastAndCheckHitOnLeaf(float3 RayOrigin, float3 RayDir, uint index, uint3 indexMin, inout float minDist, inout float4 OutColor, uint3 parentIndexMin)
{
    float4 volumeColor = COLOR_VOLUME_BUFFER[index];
    
    if (volumeColor.a < EPSILON)
    {
        return;
    }
                        
    float3 boundMin = MAIN_CAMERA_WORLD_POS + indexMin * VOXEL_SIZE - (VOLUME_SIZE >> 1) * VOXEL_SIZE;
    float3 boundMax = boundMin + VOXEL_SIZE;
    float3 t0 = (boundMin - RayOrigin) / RayDir;
    float3 t1 = (boundMax - RayOrigin) / RayDir;
    float3 tMin = min(t0, t1);
    float3 tMax = max(t0, t1);
    float dstA = max(max(tMin.x, tMin.y), tMin.z);
    float dstB = min(min(tMax.x, tMax.y), tMax.z);
    
    if (dstA < EPSILON || dstA >= dstB)
    {
        return;
    }

    if (minDist > EPSILON && minDist < dstA)
    {
        return;
    }

    minDist = dstA;
    OutColor = volumeColor;
    //OutColor.a = 1.0;

    //uint volumeSizeSquare = (VOLUME_SIZE * VOLUME_SIZE);
    //uint halfVolumeSize = VOLUME_SIZE >> 1;
    //uint x = index / volumeSizeSquare;
    //uint y = (index - (x * volumeSizeSquare)) / VOLUME_SIZE;
    //uint z = (index - (x * volumeSizeSquare) - (y * VOLUME_SIZE));
    //float3 voxelBoundMin = MAIN_CAMERA_WORLD_POS + uint3(x, y, z) * VOXEL_SIZE - halfVolumeSize * VOXEL_SIZE;

    //if (length(voxelBoundMin - boundMin) > EPSILON)
    //{
    //    if (GetVolumeLinearIndex(indexMin) - index != 0)
    //    {
    //        float3 vec0 = float3(x, y, z);
    //        float3 vec1 = float3(indexMin.x, indexMin.y, indexMin.z);
    //        OutColor = float4(distance(vec0.xxx, vec1.xxx).xxx, 1);
    //        OutColor = float4(float3(parentIndexMin) / VOLUME_SIZE, 1);
    //        OutColor = float4(float3(vec0) / VOLUME_SIZE, 1);
    //        OutColor.y = 0.0;
    //        OutColor.z = 0.0;
    //    }
    //    else
    //    {
    //        OutColor = float4(0, 1, 0, 1);
    //    }
    //}
    //else
    //{
    //    OutColor = float4(0, 0, 1, 1);
    //}
}

bool RaycastAndCheckHitOnNode(float3 RayOrigin, float3 RayDir, uint3 indexMin, uint volumeSize)
{
    float3 boundMin = MAIN_CAMERA_WORLD_POS + indexMin * VOXEL_SIZE - (VOLUME_SIZE >> 1) * VOXEL_SIZE;
    float3 boundMax = boundMin + volumeSize * VOXEL_SIZE;
    float3 t0 = (boundMin - RayOrigin) / RayDir;
    float3 t1 = (boundMax - RayOrigin) / RayDir;
    float3 tMin = min(t0, t1);
    float3 tMax = max(t0, t1);
    float dstA = max(max(tMin.x, tMin.y), tMin.z);
    float dstB = min(min(tMax.x, tMax.y), tMax.z);

    if (dstB < 0 || dstA >= dstB)
    {
        return false;
    }

    return true;
}

void Raycast_float(float3 RayOrigin, float3 RayDir, float MaxDistance, out float4 OutColor, out float Distance)
{
    OutColor = float4(0.0, 0.0, 0.0, 0.0);
    Distance = -1.0;

    OctreeStackItem stack[128];
    uint stackCount = 0;
    
    OctreeStackItem item;
    item.index = 0;
    item.volumeSize = VOLUME_SIZE;
    item.indexMin = uint3(0, 0, 0);
    item.parentIndexMin = item.indexMin;
    stack[stackCount] = item;
    ++stackCount;

    uint debugStep = 0;

    while (stackCount > 0)
    {
        ++debugStep;

        if (debugStep >= 1000)
        {
            OutColor = float4(0, 0, 1, 1);
            break;
        }

        item = stack[stackCount - 1];
        --stackCount;

        if (item.volumeSize == 1)
        {
            RaycastAndCheckHitOnLeaf(RayOrigin, RayDir, item.index, item.indexMin, Distance, OutColor, item.parentIndexMin);
            continue;
        }

        if (!RaycastAndCheckHitOnNode(RayOrigin, RayDir, item.indexMin, item.volumeSize))
        {
            continue;
        }

        OctreeNode parentNode = OCTREE_BUFFER[item.index];
        uint volumeSize = item.volumeSize >> 1;

        [unroll]
        for (int i = 7; i >= 0; --i)
        {
            if ((volumeSize != 1 && parentNode.childNodeIndices[i] == 0) || (parentNode.childNodeIndices[i] == 0xFFFFFFFF))
            {
                continue;
            }

            OctreeStackItem newItem;
            newItem.index = parentNode.childNodeIndices[i];
            newItem.volumeSize = volumeSize;
            newItem.indexMin = item.indexMin + uint3((i & 0x4) >> 2, (i & 0x2) >> 1, (i & 0x1)) * volumeSize;
            newItem.parentIndexMin = item.indexMin;
            stack[stackCount] = newItem;
            ++stackCount;
        }
    }
}
#elif RENDERTEXTURE3D_HIZ_CASTING
Texture3D<float4> VOLUME_RENDER_TEXTURE_3D;
SamplerState sampler_VOLUME_RENDER_TEXTURE_3D;
uint VOLUME_MIP_COUNT;
uint VOLUME_SIZE;
float VOXEL_SIZE;
float3 MAIN_CAMERA_WORLD_POS;

int GetVolumeLinearIndex(int3 index)
{
    return index.x * VOLUME_SIZE * VOLUME_SIZE + index.y * VOLUME_SIZE + index.z;
}

bool IsInVolume(float3 volumeUV)
{
    if (volumeUV.x < 0.0 || volumeUV.y < 0.0 || volumeUV.x < 0.0)
    {
        return false;
    }

    if (volumeUV.x > 1.0 || volumeUV.y > 1.0 || volumeUV.x > 1.0)
    {
        return false;
    }

    return true;
}

void Raycast_float(float3 RayOrigin, float3 RayDir, float MaxDistance, out float4 OutColor, out float Distance)
{
    OutColor = float4(0.0, 0.0, 0.0, 0.0);
    Distance = 0.0;

    float volumeDivider = 1.0 / (float)VOLUME_SIZE;
    float voxelDivider = 1.0 / (float)VOXEL_SIZE;
    float volumeWorldSize = VOLUME_SIZE * VOXEL_SIZE;
    uint maxMipLevel = VOLUME_MIP_COUNT - 1;
    float maxMipLevelDivider = 1.0 / (float)(maxMipLevel + 1);
    uint currentMipLevel = 0;
    uint maxStep = 1000;
    float3 volumeUV = (RayOrigin - MAIN_CAMERA_WORLD_POS + (float)(VOLUME_SIZE >> 1) * VOXEL_SIZE) * voxelDivider * volumeDivider;
    float volumeStepFactor = 1.0; // INFO: control this factor if you want to get clearer result...the lower makes the clearer...

    while (maxStep > 0)
    {
        --maxStep;

        if (!IsInVolume(volumeUV))
        {
            break;
        }

        float4 volumeColor = VOLUME_RENDER_TEXTURE_3D.SampleLevel(sampler_VOLUME_RENDER_TEXTURE_3D, volumeUV, currentMipLevel);
        float factor = pow(2.0, currentMipLevel);
        float revertFactor = 1.0 / factor; //1.0; // 1.0 / factor;// (maxMipLevel - currentMipLevel)* maxMipLevelDivider;

        // Hit
        if (volumeColor.a > 0.0)
        {
            OutColor.xyz = OutColor.xyz * OutColor.a + volumeColor.xyz * revertFactor * (1.0 - OutColor.a);
            OutColor.a = saturate(OutColor.a + volumeColor.a * revertFactor * (1.0 - OutColor.a));
            factor = (currentMipLevel > 0) ? 0.0 : factor;
            currentMipLevel = (currentMipLevel > 0) ? currentMipLevel - 1 : 0;
        }
        // Miss
        else
        {
            currentMipLevel = (currentMipLevel + 2 < maxMipLevel) ? currentMipLevel + 2 : maxMipLevel;
        }

        if (OutColor.a > 0.95)
        {
            break;
        }

        Distance += volumeDivider * volumeStepFactor * factor * volumeWorldSize;
        volumeUV += RayDir * volumeDivider * volumeStepFactor * factor;

        if (Distance > MaxDistance)
        {
            Distance = MaxDistance;
            break;
        }
    }

    //if (maxStep <= 0)
    //{
    //    OutColor = float4(1, 0, 0, 1);
    //}

    //OutColor.a = 1.0;
}
#elif RENDERTEXTURE3D_HIGHESTMIP_CASTING
Texture3D<float4> VOLUME_RENDER_TEXTURE_3D;
SamplerState sampler_VOLUME_RENDER_TEXTURE_3D;
uint VOLUME_MIP_COUNT;
uint VOLUME_SIZE;
float VOXEL_SIZE;
float3 MAIN_CAMERA_WORLD_POS;

int GetVolumeLinearIndex(int3 index)
{
    return index.x * VOLUME_SIZE * VOLUME_SIZE + index.y * VOLUME_SIZE + index.z;
}

bool IsInVolume(float3 volumeUV)
{
    if (volumeUV.x < 0.0 || volumeUV.y < 0.0 || volumeUV.x < 0.0)
    {
        return false;
    }

    if (volumeUV.x > 1.0 || volumeUV.y > 1.0 || volumeUV.x > 1.0)
    {
        return false;
    }

    return true;
}

void Raycast_float(float3 RayOrigin, float3 RayDir, float MaxDistance, out float4 OutColor, out float Distance)
{
    OutColor = float4(0.0, 0.0, 0.0, 0.0);
    Distance = 0.0;

    float volumeDivider = 1.0 / (float)VOLUME_SIZE;
    float voxelDivider = 1.0 / (float)VOXEL_SIZE;
    float volumeWorldSize = VOLUME_SIZE * VOXEL_SIZE;
    uint maxMipLevel = VOLUME_MIP_COUNT - 1;
    uint currentMipLevel = 0;
    uint maxStep = 1000;
    float3 volumeUV = (RayOrigin - MAIN_CAMERA_WORLD_POS + (float)(VOLUME_SIZE >> 1) * VOXEL_SIZE) * voxelDivider * volumeDivider;
    float volumeStepFactor = 1.0; // INFO: control this factor if you want to get clearer result...the lower makes the clearer...

    while (maxStep > 0)
    {
        --maxStep;

        if (!IsInVolume(volumeUV))
        {
            break;
        }

        float4 volumeColor = VOLUME_RENDER_TEXTURE_3D.SampleLevel(sampler_VOLUME_RENDER_TEXTURE_3D, volumeUV, currentMipLevel);

        // Hit
        if (volumeColor.a > 0.0)
        {
            OutColor.xyz = OutColor.xyz * OutColor.a + volumeColor.xyz * (1.0 - OutColor.a);
            OutColor.a = saturate(OutColor.a + volumeColor.a * (1.0 - OutColor.a));
        }

        if (OutColor.a > 0.95)
        {
            break;
        }

        Distance += volumeDivider * volumeStepFactor * volumeWorldSize;
        volumeUV += RayDir * volumeDivider * volumeStepFactor;

        if (Distance > MaxDistance)
        {
            Distance = MaxDistance;
            break;
        }
    }

    //if (maxStep <= 0)
    //{
    //    OutColor = float4(1, 0, 0, 1);
    //}

    //OutColor.a = 1.0;
}
#elif RENDERTEXTURE3D_SIMPLE
Texture3D<float4> VOLUME_RENDER_TEXTURE_3D;
SamplerState sampler_VOLUME_RENDER_TEXTURE_3D;
uint VOLUME_SIZE;
float VOXEL_SIZE;
float3 MAIN_CAMERA_WORLD_POS;

int GetVolumeLinearIndex(int3 index)
{
    return index.x * VOLUME_SIZE * VOLUME_SIZE + index.y * VOLUME_SIZE + index.z;
}

bool IsInVolume(float3 pos)
{
    float3 boundMin = MAIN_CAMERA_WORLD_POS - (VOLUME_SIZE >> 1) * VOXEL_SIZE;

    if (pos.x < boundMin.x || pos.y < boundMin.y || pos.z < boundMin.z)
    {
        return false;
    }

    float3 boundMax = boundMin + VOLUME_SIZE * VOXEL_SIZE;

    if (pos.x > boundMax.x || pos.y > boundMax.y || pos.z > boundMax.z)
    {
        return false;
    }

    return true;
}

void Raycast_float(float3 RayOrigin, float3 RayDir, float MaxDistance, out float4 OutColor, out float Distance)
{
    OutColor = float4(0.0, 0.0, 0.0, 0.0);
    Distance = -1.0;

    float3 divider = 1.0 / (float)VOLUME_SIZE;
    int halfVolumeSize = VOLUME_SIZE >> 1;

    for (int x = 0; x < VOLUME_SIZE; ++x)
    {
        for (int y = 0; y < VOLUME_SIZE; ++y)
        {
            for (int z = 0; z < VOLUME_SIZE; ++z)
            {
                int3 volumeIndex = int3(x, y, z);
                float4 volumeColor = VOLUME_RENDER_TEXTURE_3D.SampleLevel(sampler_VOLUME_RENDER_TEXTURE_3D, float3(x, y, z) * divider, 0);

                if (volumeColor.a < EPSILON)
                {
                    continue;
                }

                float3 boundMin = MAIN_CAMERA_WORLD_POS + (volumeIndex - halfVolumeSize) * VOXEL_SIZE;
                float3 boundMax = boundMin + VOXEL_SIZE;

                float3 t0 = (boundMin - RayOrigin) / RayDir;
                float3 t1 = (boundMax - RayOrigin) / RayDir;
                float3 tMin = min(t0, t1);
                float3 tMax = max(t0, t1);
                float dstA = max(max(tMin.x, tMin.y), tMin.z);
                float dstB = min(min(tMax.x, tMax.y), tMax.z);

                if ((dstA > 0 && dstA < dstB) && (Distance < 0.0 || Distance > dstA))
                {
                    Distance = dstA;
                    OutColor = volumeColor;
                    OutColor.a = 1.0;
                }
            }
        }
    }
}
#else
StructuredBuffer<float4> COLOR_VOLUME_BUFFER;
uint VOLUME_SIZE;
float VOXEL_SIZE;
float3 MAIN_CAMERA_WORLD_POS;

int GetVolumeLinearIndex(int3 index)
{
    return index.x * VOLUME_SIZE * VOLUME_SIZE + index.y * VOLUME_SIZE + index.z;
}

bool IsInVolume(float3 pos)
{
    float3 boundMin = MAIN_CAMERA_WORLD_POS - (VOLUME_SIZE >> 1) * VOXEL_SIZE;

    if (pos.x < boundMin.x || pos.y < boundMin.y || pos.z < boundMin.z)
    {
        return false;
    }

    float3 boundMax = boundMin + VOLUME_SIZE * VOXEL_SIZE;

    if (pos.x > boundMax.x || pos.y > boundMax.y || pos.z > boundMax.z)
    {
        return false;
    }

    return true;
}

void Raycast_float(float3 RayOrigin, float3 RayDir, float MaxDistance, out float4 OutColor, out float Distance)
{
    OutColor = float4(0.0, 0.0, 0.0, 0.0);
    Distance = -1.0;

    int halfVolumeSize = VOLUME_SIZE >> 1;

    for (uint x = 0; x < VOLUME_SIZE; ++x)
    {
        for (uint y = 0; y < VOLUME_SIZE; ++y)
        {
            for (uint z = 0; z < VOLUME_SIZE; ++z)
            {
                int3 volumeIndex = int3(x, y, z);
                float4 volumeColor = COLOR_VOLUME_BUFFER[GetVolumeLinearIndex(volumeIndex)];

                if (volumeColor.a < EPSILON)
                {
                    continue;
                }
                    
                float3 boundMin = MAIN_CAMERA_WORLD_POS + (volumeIndex - halfVolumeSize) * VOXEL_SIZE;
                float3 boundMax = boundMin + VOXEL_SIZE;

                float3 t0 = (boundMin - RayOrigin) / RayDir;
                float3 t1 = (boundMax - RayOrigin) / RayDir;
                float3 tMin = min(t0, t1);
                float3 tMax = max(t0, t1);
                float dstA = max(max(tMin.x, tMin.y), tMin.z);
                float dstB = min(min(tMax.x, tMax.y), tMax.z);

                if ((dstA > 0 && dstA < dstB) && (Distance < 0.0 || Distance > dstA))
                {
                    Distance = dstA;
                    OutColor = volumeColor;
                    OutColor.a = 1.0;
                }
            }
        }
    }
}
#endif

#endif
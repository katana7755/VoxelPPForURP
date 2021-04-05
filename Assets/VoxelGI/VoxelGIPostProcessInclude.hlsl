#ifndef VOXEL_GI_POST_PROCESS_INCLUDE
#define VOXEL_GI_POST_PROCESS_INCLUDE

//#define SSBO_OCTREE_ONLY_CHECK_INSIDE_MODE 1
#define RENDERTEXTURE3D_HIZ_CASTING 1
//#define RENDERTEXTURE3D_HIGHESTMIP_CASTING 1
#include "VoxelGIRaycastingInclude.hlsl"

#define RAY_COUNT 9
#define MAX_RAY_DISTANCE 1.0
#define AO_MULTIPLY_FACTOR 1.0
#define AO_POWER_FACTOR 2.5

void MulticastForAmbientOcculusion_float(float3 Origin, float3 Normal, out float Occlusion, out float3 ShadowColor)
{
	const float2 RAY_ANGLES[RAY_COUNT] = {
		float2(0.0, 0.0),
		
		float2(PI * 0.166666, 0.0 * PI * 0.5), float2(PI * 0.166666, 1.0 * PI * 0.5), float2(PI * 0.166666, 2.0 * PI * 0.5), float2(PI * 0.166666, 3.0 * PI * 0.5),
		//float2(PI * 0.166666, 0.0 * PI * 0.25), float2(PI * 0.166666, 1.0 * PI * 0.25), float2(PI * 0.166666, 2.0 * PI * 0.25), float2(PI * 0.166666, 3.0 * PI * 0.25), float2(PI * 0.166666, 4.0 * PI * 0.25), float2(PI * 0.166666, 5.0 * PI * 0.25), float2(PI * 0.166666, 6.0 * PI * 0.25), float2(PI * 0.166666, 7.0 * PI * 0.25),
		
		float2(PI * 0.333333, 0.0 * PI * 0.25), float2(PI * 0.333333, 1.0 * PI * 0.75), float2(PI * 0.333333, 2.0 * PI * 1.25), float2(PI * 0.333333, 3.0 * PI * 1.75),
		//float2(PI * 0.333333, 0.0 * PI * 0.25), float2(PI * 0.333333, 1.0 * PI * 0.25), float2(PI * 0.333333, 2.0 * PI * 0.25), float2(PI * 0.333333, 3.0 * PI * 0.25), float2(PI * 0.333333, 4.0 * PI * 0.25), float2(PI * 0.333333, 5.0 * PI * 0.25), float2(PI * 0.333333, 6.0 * PI * 0.25), float2(PI * 0.333333, 7.0 * PI * 0.25)
		
		//float2(PI * 0.45, 0.0 * PI * 0.25), float2(PI * 0.45, 1.0 * PI * 0.75), float2(PI * 0.45, 2.0 * PI * 1.25), float2(PI * 0.45, 3.0 * PI * 1.75),
		//float2(PI * 0.45, 0.0 * PI * 0.25), float2(PI * 0.45, 1.0 * PI * 0.25), float2(PI * 0.45, 2.0 * PI * 0.25), float2(PI * 0.45, 3.0 * PI * 0.25), float2(PI * 0.45, 4.0 * PI * 0.25), float2(PI * 0.45, 5.0 * PI * 0.25), float2(PI * 0.45, 6.0 * PI * 0.25), float2(PI * 0.45, 7.0 * PI * 0.25)
	};

	Occlusion = 0.0;
	ShadowColor = 0.0;

#if SSBO_OCTREE_ONLY_CHECK_INSIDE_MODE
	if (IsInVolume(Origin) == false)
	{
		return;
	}
#endif

	float3 normal = normalize(Normal);
	float3 tangent = normalize(cross(float3(0.0, 1.0, 0.0), normal));
	float3 binormal = normalize(cross(Normal, tangent));
	float4 outColor;
	float outDistance;
	float deltaRay = 1.0 / RAY_COUNT;
	Origin += normal * VOXEL_SIZE;

	float count = 0.0;
	int selectedIndex = 16;

	for (int i = 0; i < RAY_COUNT; ++i)
	{
		float x = sin(RAY_ANGLES[i].x) * cos(RAY_ANGLES[i].y);
		float y = sin(RAY_ANGLES[i].x) * sin(RAY_ANGLES[i].y);
		float z = cos(RAY_ANGLES[i].x);
		float3 dir = normalize(tangent * x + binormal * y + normal * z);
		float nDotD = dot(dir, normal);

#if SSBO_OCTREE_ONLY_CHECK_INSIDE_MODE
		//if (abs(dot(dir, Normal)) < 0.1)
		////if (abs(RAY_ANGLES[i].x) < 0.00001)
		//{
		//	Occlusion = 1.0;
		//	return;
		//}

		int maxCount = 100;
		float currentDistance = VOXEL_SIZE;

		while (maxCount > 0)
		{
			Raycast_float(Origin + dir * currentDistance, dir, MAX_RAY_DISTANCE, outColor, outDistance);
			currentDistance += outDistance + VOXEL_SIZE * 0.01;

			if (outDistance < EPSILON)
			{
				break;
			}

			if (currentDistance > MAX_RAY_DISTANCE)
			{
				break;
			}
			
			if (outDistance < VOXEL_SIZE)
			{
				Occlusion += AO_MULTIPLY_FACTOR * pow(saturate(1.0 - currentDistance / MAX_RAY_DISTANCE), AO_POWER_FACTOR) * deltaRay * outColor.a * nDotD;

				if (Occlusion > 1.0)
				{
					break;
				}
			}
			
			--maxCount;
		}
#elif RENDERTEXTURE3D_HIZ_CASTING
		Raycast_float(Origin, dir, MAX_RAY_DISTANCE, outColor, outDistance);
		Occlusion += AO_MULTIPLY_FACTOR * pow(saturate(1.0 - outDistance / MAX_RAY_DISTANCE), AO_POWER_FACTOR) * deltaRay * outColor.a * nDotD;
#elif RENDERTEXTURE3D_HIGHESTMIP_CASTING
		Raycast_float(Origin, dir, MAX_RAY_DISTANCE, outColor, outDistance);
		Occlusion += AO_MULTIPLY_FACTOR * pow(saturate(1.0 - outDistance / MAX_RAY_DISTANCE), AO_POWER_FACTOR) * deltaRay * outColor.a * nDotD;
#endif
	}

	Occlusion = saturate(Occlusion);

	//if (Occlusion > 0.0)
	//{
	//	Occlusion = 1.0;
	//}
}
#endif
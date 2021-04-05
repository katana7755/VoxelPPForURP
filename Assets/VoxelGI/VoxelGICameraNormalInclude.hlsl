#ifdef SHADERGRAPH_PREVIEW
void SceneNormal_float(float2 UV, out float3 Normal)
{
	Normal = float3(0, 0, 1);
}
#else
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

void SceneNormal_float(float2 UV, out float3 Normal)
{
	Normal = SampleSceneNormals(UV);
}
#endif
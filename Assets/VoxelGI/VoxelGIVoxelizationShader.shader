Shader "Unlit/VoxelGIVoxelizationShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1.0, 1.0, 1.0, 1.0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            Tags{"LightMode" = "UniversalForward"}

            Cull Off
            ZTest Always
            ZWrite Off

            HLSLPROGRAM
            #pragma target 5.0

            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2g
            {
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            struct g2f
            {
                float4 clipPos : SV_POSITION;
                float2 uv : TEXCOORD0;
                uint viewAxis : TEXCOORD1; // 0: Front -> Back, 1: Left -> Right, 2: Top -> Bottom
                float3 worldPos : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            uniform RWStructuredBuffer<uint4> UINTCOLOR_VOLUME_BUFFER : register(u3);
            uniform RWStructuredBuffer<uint> COUNT_VOLUME_BUFFER : register(u4);
            uint VOLUME_SIZE;
            float VOXEL_SIZE;
            float3 MAIN_CAMERA_WORLD_POS;

            uint GetVolumeLinearIndex(uint3 index)
            {
                return index.x * VOLUME_SIZE * VOLUME_SIZE + index.y * VOLUME_SIZE + index.z;
            }

            v2g vert (appdata input)
            {
                v2g output;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.worldPos = mul(unity_ObjectToWorld, input.vertex);
                
                return output;
            }

            [maxvertexcount(3)]
            void geom(triangle v2g input[3], inout TriangleStream<g2f> triStream)
            {
                float3 edge1 = (input[1].worldPos - input[0].worldPos).xyz;
                float3 edge2 = (input[2].worldPos - input[0].worldPos).xyz;
                float3 area;
                area.x = 0.5 * length(cross(float3(0.0, edge1.y, edge1.z), float3(0.0, edge2.y, edge2.z)));
                area.y = 0.5 * length(cross(float3(edge1.x, 0.0, edge1.z), float3(edge2.x, 0.0, edge2.z)));
                area.z = 0.5 * length(cross(float3(edge1.x, edge1.y, 0.0), float3(edge2.x, edge2.y, 0.0)));
                
                float4x4 V = UNITY_MATRIX_V;
                float4x4 P = UNITY_MATRIX_P;
                float3 viewPos = float3(V._m03, V._m13, V._m23);
                float halfSize = VOLUME_SIZE * VOXEL_SIZE * 0.5f;
                V._m03 = 0.0;
                V._m13 = 0.0;
                V._m23 = 0.0;

                uint viewAxis = 0;
                float4 clipPosList[3];

                if (area.x > area.y && area.x > area.z)
                {
                    viewAxis = 1;

                    for (int i = 0; i < 3; ++i)
                    {
                        float3 pos = input[i].worldPos - _WorldSpaceCameraPos;
                        pos.x += halfSize;
                        pos.z -= halfSize;
                        clipPosList[i] = mul(P, mul(V, float4(-pos.z, pos.y, pos.x, 1.0)));
                    }
                }
                else if (area.y > area.x && area.y > area.z)
                {
                    viewAxis = 2;

                    for (int i = 0; i < 3; ++i)
                    {
                        float3 pos = input[i].worldPos - _WorldSpaceCameraPos;
                        pos.y += halfSize;
                        pos.z -= halfSize;
                        clipPosList[i] = mul(P, mul(V, float4(pos.x, pos.z, pos.y, 1.0)));
                    }
                }
                else
                {
                    for (int i = 0; i < 3; ++i)
                    {
                        float3 pos = input[i].worldPos - _WorldSpaceCameraPos;
                        clipPosList[i] = mul(P, mul(V, float4(pos.x, pos.y, pos.z, 1.0)));
                    }
                }

                for (int i = 0; i < 3; ++i)
                {
                    g2f output;
                    output.clipPos = clipPosList[i];
                    output.uv = input[i].uv;
                    output.viewAxis = viewAxis;
                    output.worldPos = input[i].worldPos;
                    triStream.Append(output);
                }
            }

            float4 frag (g2f input) : SV_Target
            {
                float4 col = tex2D(_MainTex, input.uv) * _Color;

                //if (input.viewAxis == 0)
                //{
                //    col = float4(0, 0, 1, 1);
                //    //col = tex2D(_MainTex, input.uv) * _Color;
                //}
                //else if (input.viewAxis == 1)
                //{
                //    col = float4(1, 0, 0, 1);
                //    //col = tex2D(_MainTex, input.uv) * _Color;
                //}
                //else
                //{
                //    col = float4(0, 1, 0, 1);
                //    //col = tex2D(_MainTex, input.uv) * _Color;
                //}

                // TODO: Currently, it is unlit shader, but one day we might need to consider lit and shadow also...
                uint3 volumeIndex = uint3((input.worldPos - MAIN_CAMERA_WORLD_POS + (VOLUME_SIZE >> 1) * VOXEL_SIZE) / VOXEL_SIZE);
                uint linearIndex = GetVolumeLinearIndex(volumeIndex);
                uint totalCount = VOLUME_SIZE * VOLUME_SIZE * VOLUME_SIZE;
                
                if (linearIndex < totalCount)
                {
                    InterlockedAdd(UINTCOLOR_VOLUME_BUFFER[linearIndex].r, uint(col.r * 255.0));
                    InterlockedAdd(UINTCOLOR_VOLUME_BUFFER[linearIndex].g, uint(col.g * 255.0));
                    InterlockedAdd(UINTCOLOR_VOLUME_BUFFER[linearIndex].b, uint(col.b * 255.0));
                    //InterlockedAdd(UINTCOLOR_VOLUME_BUFFER[linearIndex].a, uint(col.a * 255.0));
                    InterlockedAdd(UINTCOLOR_VOLUME_BUFFER[linearIndex].a, uint(1.0 * 255.0));
                    InterlockedAdd(COUNT_VOLUME_BUFFER[linearIndex], 1);
                }

                return col;
            }
            ENDHLSL
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VoxelGIForwardRenderer : ScriptableRenderer
{
    public VoxelGIForwardRenderer(VoxelGIForwardRendererData data) : base(data)
    {
        m_GraphicResources = data._GraphicResources;
        m_GraphicResources.Setup();
        m_DrawAllIntoVolumeRenderPass = new DrawAllIntoVolumeRenderPass(m_GraphicResources, data._SettingsForDrawAllIntoVolume);
    }

    protected override void Dispose(bool disposing)
    {
        m_DrawAllIntoVolumeRenderPass.Dispose();
        m_GraphicResources.Dispose();
    }

    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
#if UNITY_EDITOR
        if (renderingData.cameraData.isSceneViewCamera || renderingData.cameraData.isPreviewCamera)
        {
            return;
        }
#endif

        ConfigureCameraTarget(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.CameraTarget);
        EnqueuePass(m_DrawAllIntoVolumeRenderPass);
    }

    public override void SetupCullingParameters(ref ScriptableCullingParameters cullingParameters, ref CameraData cameraData)
    {
        base.SetupCullingParameters(ref cullingParameters, ref cameraData);
    }

    public VoxelGIGraphicResources GetGraphicResource()
    {
        return m_GraphicResources;
    }

    private VoxelGIGraphicResources m_GraphicResources = null;
    private DrawAllIntoVolumeRenderPass m_DrawAllIntoVolumeRenderPass = null;

    public class DrawAllIntoVolumeRenderPass : ScriptableRenderPass
    {
        public DrawAllIntoVolumeRenderPass(VoxelGIGraphicResources graphicsResources, SerializableSettings settings)
        {
            Dispose();
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            m_IsSetup = true;
            m_GraphicResources = graphicsResources;
            m_Settings = settings;
        }

        public void Dispose()
        {
            if (!m_IsSetup)
            {
                return;
            }

            m_Settings = null;
            m_GraphicResources = null;
            m_IsSetup = false;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!m_IsSetup)
            {
                return;
            }

#if VOLUME_RESOURCE_IS_RENDERTEXTURE3D
            m_ClearGroupSize = m_GraphicResources.GetVolumeSize() / 8;

            // Clear volume render texture and count buffer...
            if (m_Settings._ClearBuffersComputeShader != null)
            {
                var commandBuffer = CommandBufferPool.Get(CommandBufferNames.CLEAR_ALL_VOLUME_BUFFERS);
                commandBuffer.SetGlobalBuffer(VoxelGIShaderPropIDs.UINTCOLOR_VOLUME_BUFFER, m_GraphicResources.GetUintColorVolumeBuffer());
                commandBuffer.SetGlobalBuffer(VoxelGIShaderPropIDs.COUNT_VOLUME_BUFFER, m_GraphicResources.GetCountVolumeBuffer());
                commandBuffer.SetComputeTextureParam(m_Settings._ClearBuffersComputeShader, 0, VoxelGIShaderPropIDs.VOLUME_RENDER_TEXTURE_3D, m_GraphicResources.GetVolumeRenderTexture3D());
                commandBuffer.SetGlobalInt(VoxelGIShaderPropIDs.VOLUME_SIZE, m_GraphicResources.GetVolumeSize());
                commandBuffer.DispatchCompute(m_Settings._ClearBuffersComputeShader, 0, m_ClearGroupSize, m_ClearGroupSize, m_ClearGroupSize);
                context.ExecuteCommandBuffer(commandBuffer);
                CommandBufferPool.Release(commandBuffer);
            }
#else
            m_ClearGroupSize = m_GraphicResources.GetVolumeSize() / 8;
            m_OctreeGroupSize = m_GraphicResources.GetVolumeSize() / 2;

            // Clear structure buffer
            if (m_Settings._ClearBuffersComputeShader != null)
            {
                var commandBuffer = CommandBufferPool.Get(CommandBufferNames.CLEAR_ALL_VOLUME_BUFFERS);
                commandBuffer.SetGlobalBuffer(VoxelGIShaderPropIDs.UINTCOLOR_VOLUME_BUFFER, m_GraphicResources.GetUintColorVolumeBuffer());
                commandBuffer.SetGlobalBuffer(VoxelGIShaderPropIDs.COUNT_VOLUME_BUFFER, m_GraphicResources.GetCountVolumeBuffer());
                commandBuffer.SetGlobalBuffer(VoxelGIShaderPropIDs.COLOR_VOLUME_BUFFER, m_GraphicResources.GetColorVolumeBuffer());
                commandBuffer.SetGlobalInt(VoxelGIShaderPropIDs.VOLUME_SIZE, m_GraphicResources.GetVolumeSize());
                commandBuffer.SetGlobalBuffer(VoxelGIShaderPropIDs.OCTREE_INDEX_BUFFER, m_GraphicResources.GetOctreeIndexBuffer());
                commandBuffer.DispatchCompute(m_Settings._ClearBuffersComputeShader, 0, m_ClearGroupSize, m_ClearGroupSize, m_ClearGroupSize);
                context.ExecuteCommandBuffer(commandBuffer);
                CommandBufferPool.Release(commandBuffer);
            }
#endif


            // Render all to the volume buffer
            if (m_Settings._DrawObjectShader != null)
            {
                var commandBuffer = CommandBufferPool.Get(CommandBufferNames.DRAW_OBJECT_INTO_VOLUME);
                commandBuffer.SetRandomWriteTarget(3, m_GraphicResources.GetUintColorVolumeBuffer());
                commandBuffer.SetRandomWriteTarget(4, m_GraphicResources.GetCountVolumeBuffer());
                commandBuffer.SetGlobalInt(VoxelGIShaderPropIDs.VOLUME_SIZE, m_GraphicResources.GetVolumeSize());
                commandBuffer.SetGlobalFloat(VoxelGIShaderPropIDs.VOXEL_SIZE, m_GraphicResources.GetVoxelSize());
                commandBuffer.SetGlobalVector(VoxelGIShaderPropIDs.MAIN_CAMERA_WORLD_POS, VoxelGICamera.GetMainCameraWorldPos());

                int count = VoxelGIRenderingDataManager.GetDataCount();

                for (int i = 0; i < count; ++i)
                {
                    var data = VoxelGIRenderingDataManager.GetDataAt(i);

                    if (!data.IsActive())
                    {
                        continue;
                    }

                    var material = data.GetMatrial(m_Settings._DrawObjectShader);
                    commandBuffer.DrawMesh(data.GetMesh(), data.GetModelMatrix(), material, data.GetSubMeshIndex(), 0);
                }

                context.ExecuteCommandBuffer(commandBuffer);
                commandBuffer.Clear();
                commandBuffer.ClearRandomWriteTargets();
                context.ExecuteCommandBuffer(commandBuffer);
                CommandBufferPool.Release(commandBuffer);
            }


#if VOLUME_RESOURCE_IS_RENDERTEXTURE3D
            // Calculate average color values for each voxel in volume render texture
            // And also generate volume render texture mipmaps...
            if (m_Settings._AverageColorsComputeShader != null)
            {
                var commandBuffer = CommandBufferPool.Get(CommandBufferNames.AVERAGE_COLORS);
                commandBuffer.SetGlobalBuffer(VoxelGIShaderPropIDs.UINTCOLOR_VOLUME_BUFFER, m_GraphicResources.GetUintColorVolumeBuffer());
                commandBuffer.SetGlobalBuffer(VoxelGIShaderPropIDs.COUNT_VOLUME_BUFFER, m_GraphicResources.GetCountVolumeBuffer());
                commandBuffer.SetComputeTextureParam(m_Settings._AverageColorsComputeShader, 0, VoxelGIShaderPropIDs.VOLUME_RENDER_TEXTURE_3D, m_GraphicResources.GetVolumeRenderTexture3D());
                commandBuffer.SetGlobalInt(VoxelGIShaderPropIDs.VOLUME_SIZE, m_GraphicResources.GetVolumeSize());
                commandBuffer.DispatchCompute(m_Settings._AverageColorsComputeShader, 0, m_ClearGroupSize, m_ClearGroupSize, m_ClearGroupSize);
                commandBuffer.GenerateMips(m_GraphicResources.GetVolumeRenderTexture3D());
                context.ExecuteCommandBuffer(commandBuffer);
                CommandBufferPool.Release(commandBuffer);
            }
#else
            // Calculate average color values for each voxel in volume buffer
            if (m_Settings._AverageColorsComputeShader != null)
            {
                var commandBuffer = CommandBufferPool.Get(CommandBufferNames.AVERAGE_COLORS);
                commandBuffer.SetGlobalBuffer(VoxelGIShaderPropIDs.UINTCOLOR_VOLUME_BUFFER, m_GraphicResources.GetUintColorVolumeBuffer());
                commandBuffer.SetGlobalBuffer(VoxelGIShaderPropIDs.COUNT_VOLUME_BUFFER, m_GraphicResources.GetCountVolumeBuffer());
                commandBuffer.SetGlobalBuffer(VoxelGIShaderPropIDs.COLOR_VOLUME_BUFFER, m_GraphicResources.GetColorVolumeBuffer());
                commandBuffer.SetGlobalInt(VoxelGIShaderPropIDs.VOLUME_SIZE, m_GraphicResources.GetVolumeSize());
                commandBuffer.DispatchCompute(m_Settings._AverageColorsComputeShader, 0, m_ClearGroupSize, m_ClearGroupSize, m_ClearGroupSize);
                context.ExecuteCommandBuffer(commandBuffer);
                CommandBufferPool.Release(commandBuffer);
            }


            // Generate octree from the volume buffer
            if (m_Settings._GenerateOctreeComputeShader != null)
            {
                var commandBuffer = CommandBufferPool.Get(CommandBufferNames.GENERATE_OCTREE_BUFFER);
                commandBuffer.SetGlobalBuffer(VoxelGIShaderPropIDs.OCTREE_BUFFER, m_GraphicResources.GetOctreeBuffer());
                commandBuffer.DispatchCompute(m_Settings._GenerateOctreeComputeShader, 0, m_OctreeGroupSize, m_OctreeGroupSize, m_OctreeGroupSize);
                context.ExecuteCommandBuffer(commandBuffer);
                CommandBufferPool.Release(commandBuffer);
            }
#endif

            // Return back to original camera view
        }

        private bool m_IsSetup = false;
        private VoxelGIGraphicResources m_GraphicResources;
        private SerializableSettings m_Settings;
        private int m_ClearGroupSize;
        private int m_OctreeGroupSize;

        private RenderTargetHandle m_VolumeRenderTexture3DHandle;

        [System.Serializable]
        public class SerializableSettings
        {
            public ComputeShader _ClearBuffersComputeShader;
            public Shader _DrawObjectShader; // Cope with both opaque and transparent?
            public ComputeShader _AverageColorsComputeShader;
            public ComputeShader _GenerateOctreeComputeShader;
        }

        private static class CommandBufferNames
        {
            public const string CLEAR_ALL_VOLUME_BUFFERS = "CLEAR_ALL_VOLUME_BUFFERS";
            public const string DRAW_OBJECT_INTO_VOLUME = "DRAW_OBJECT_INTO_VOLUME";
            public const string AVERAGE_COLORS = "AVERAGE_COLORS";
            public const string GENERATE_OCTREE_BUFFER = "GENERATE_OCTREE_BUFFER";
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VoxelGIPostProcessPassFeature : ScriptableRendererFeature
{
    [Header("[Actual Settings]")]
    [SerializeField] private VoxelGIGraphicResources _GraphicResources;
    [SerializeField] private GlobalIlluminationRenderPass.SerializableSettings _SettingsForGlobalIllumination;

    public override void Create()
    {
        Dispose(true);

        if (SystemInfo.graphicsShaderLevel < 50)
        {
            Debug.LogError($"[VoxelGIRenderPassFeature] this feature is not supported in your device shader model {SystemInfo.graphicsShaderLevel}.");
            return;
        }

        m_GlobalIlluminationRenderPass = new GlobalIlluminationRenderPass();
        m_GlobalIlluminationRenderPass.Setup(_GraphicResources, _SettingsForGlobalIllumination);
    }

    protected override void Dispose(bool disposing)
    {
        if (m_GlobalIlluminationRenderPass != null)
        {
            m_GlobalIlluminationRenderPass.Dispose();
            m_GlobalIlluminationRenderPass = null;
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
#if UNITY_EDITOR
        if (renderingData.cameraData.isSceneViewCamera || renderingData.cameraData.isPreviewCamera)
        {
            return;
        }
#endif

        m_GlobalIlluminationRenderPass.BeforeBeingAdded();
        renderer.EnqueuePass(m_GlobalIlluminationRenderPass);
    }

    private GlobalIlluminationRenderPass m_GlobalIlluminationRenderPass;

    class GlobalIlluminationRenderPass : ScriptableRenderPass
    {
        public void BeforeBeingAdded()
        {
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        public void Setup(VoxelGIGraphicResources graphicResources, SerializableSettings settings)
        {
            Dispose();
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            m_GraphicResources = graphicResources;
            m_Settings = settings;

            if (m_Settings._DrawFullScreenShader != null)
            {
                m_DrawFullScreenMaterial = new Material(m_Settings._DrawFullScreenShader);
            }
            else
            {
                m_DrawFullScreenMaterial = null;
            }
        }

        public void Dispose()
        {
            if (m_DrawFullScreenMaterial != null)
            {
#if UNITY_EDITOR
                if (UnityEditor.EditorApplication.isPlaying)
                {
                    GameObject.Destroy(m_DrawFullScreenMaterial);
                }
                else
                {
                    GameObject.DestroyImmediate(m_DrawFullScreenMaterial);
                }
#else
                GameObject.Destroy(m_DrawFullScreenMaterial);
#endif
                m_DrawFullScreenMaterial = null;
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_DrawFullScreenMaterial != null)
            {
#if VOLUME_RESOURCE_IS_RENDERTEXTURE3D
                var commandBuffer = CommandBufferPool.Get(CommandBufferNames.POSTPROCESS_GLOBAL_ILLUMINATION);
                commandBuffer.SetGlobalTexture(VoxelGIShaderPropIDs.VOLUME_RENDER_TEXTURE_3D, m_GraphicResources.GetVolumeRenderTexture3D());
                commandBuffer.SetGlobalInt(VoxelGIShaderPropIDs.VOLUME_MIP_COUNT, m_GraphicResources.GetVolumeMipCount());
                commandBuffer.SetGlobalInt(VoxelGIShaderPropIDs.VOLUME_SIZE, m_GraphicResources.GetVolumeSize());
                commandBuffer.SetGlobalFloat(VoxelGIShaderPropIDs.VOXEL_SIZE, m_GraphicResources.GetVoxelSize());
                commandBuffer.SetGlobalVector(VoxelGIShaderPropIDs.MAIN_CAMERA_WORLD_POS, VoxelGICamera.GetMainCameraWorldPos());

                var matrix = renderingData.cameraData.camera.transform.localToWorldMatrix * Matrix4x4.Translate(Vector3.forward * (renderingData.cameraData.camera.nearClipPlane + 0.01f));
                commandBuffer.DrawMesh(RenderingUtils.fullscreenMesh, matrix, m_DrawFullScreenMaterial, 0, 0);
                context.ExecuteCommandBuffer(commandBuffer);
#else
                var commandBuffer = CommandBufferPool.Get(CommandBufferNames.POSTPROCESS_GLOBAL_ILLUMINATION);
                commandBuffer.SetGlobalBuffer(VoxelGIShaderPropIDs.COLOR_VOLUME_BUFFER, m_GraphicResources.GetColorVolumeBuffer());
                commandBuffer.SetGlobalBuffer(VoxelGIShaderPropIDs.OCTREE_BUFFER, m_GraphicResources.GetOctreeBuffer());
                commandBuffer.SetGlobalInt(VoxelGIShaderPropIDs.VOLUME_SIZE, m_GraphicResources.GetVolumeSize());
                commandBuffer.SetGlobalFloat(VoxelGIShaderPropIDs.VOXEL_SIZE, m_GraphicResources.GetVoxelSize());
                commandBuffer.SetGlobalVector(VoxelGIShaderPropIDs.MAIN_CAMERA_WORLD_POS, VoxelGICamera.GetMainCameraWorldPos());

                var matrix = renderingData.cameraData.camera.transform.localToWorldMatrix * Matrix4x4.Translate(Vector3.forward * (renderingData.cameraData.camera.nearClipPlane + 0.01f));
                commandBuffer.DrawMesh(RenderingUtils.fullscreenMesh, matrix, m_DrawFullScreenMaterial, 0, 0);
                context.ExecuteCommandBuffer(commandBuffer);
#endif
            }
        }

        private VoxelGIGraphicResources m_GraphicResources;
        private SerializableSettings m_Settings;
        private Material m_DrawFullScreenMaterial;

        [System.Serializable]
        public class SerializableSettings
        {
            public Shader _DrawFullScreenShader;
        }

        private static class CommandBufferNames
        {
            public const string POSTPROCESS_GLOBAL_ILLUMINATION = "POSTPROCESS_GLOBAL_ILLUMINATION";
        }
    }

    public enum PossibleVolumeSize
    {
        _8 = 8,
        _16 = 16,
        _32 = 32,
        _64 = 64,
        _128 = 128,
        _256 = 256,
    };
}



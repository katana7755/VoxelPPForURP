using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

[CreateAssetMenu(fileName = "VoxelGIGraphicResources", menuName = "Voxel GI/Graphic Resources")]
public class VoxelGIGraphicResources : ScriptableObject
{
    [SerializeField]                    private VoxelGIPossibleVolumeSize   _VolumeSize = VoxelGIPossibleVolumeSize._64;
    [SerializeField]                    private VoxelGIRenderTargetDensity  _RenderTargetDesity = VoxelGIRenderTargetDensity._1;
    [SerializeField, Range(0.01f, 1f)]  private float                       _VoxelSize = 1f;

#if UNITY_EDITOR
    public void ApplyCurrentSettings()
    {
        // TODO: Need to check this is the active one...
        Setup();

        if (VoxelGICamera.IsExist())
        {
            VoxelGICamera.UpdateCameraSettings();
        }

        Resources.UnloadUnusedAssets();
    }
#endif

    public void Setup()
    {
        Dispose();
        m_IsSetup = true;
        m_VolumeSize = (int)_VolumeSize;
        m_RenderTargetDesity = (int)_RenderTargetDesity;
        m_VoxelSize = _VoxelSize;
        m_VolumeTotalCount = m_VolumeSize * m_VolumeSize * m_VolumeSize;

#if VOLUME_RESOURCE_IS_RENDERTEXTURE3D
        m_VolumeMipCount = Mathf.CeilToInt(Mathf.Log(m_VolumeSize, 2f)) + 1;

        RenderTextureDescriptor desc = new RenderTextureDescriptor();
        desc.width = m_VolumeSize;
        desc.height = m_VolumeSize;
        desc.volumeDepth = m_VolumeSize;
        desc.dimension = TextureDimension.Tex3D;
        desc.useMipMap = true;
        desc.mipCount = m_VolumeMipCount;
        desc.graphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.HDR); //SystemInfo.GetCompatibleFormat(GraphicsFormat.R8G8B8A8_SRGB, FormatUsage.Render);

        if (desc.graphicsFormat == GraphicsFormat.None)
        {
            desc.graphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR); // ????...
        }

        desc.msaaSamples = 1;
        desc.enableRandomWrite = true;
        m_VolumeRenderTexture3D = new RenderTexture(desc);
        m_VolumeRenderTexture3D.Create();
#else
        m_ColorVolumeBuffer = new ComputeBuffer(m_VolumeTotalCount, 4 * 4, ComputeBufferType.Default, ComputeBufferMode.Immutable);
        m_OctreeBuffer = new ComputeBuffer(m_VolumeTotalCount / 8, 4 * 8, ComputeBufferType.Default, ComputeBufferMode.Immutable);
        m_OctreeIndexBuffer = new ComputeBuffer(1, 4, ComputeBufferType.Default, ComputeBufferMode.Immutable);
#endif

        m_UintColorVolumeBuffer = new ComputeBuffer(m_VolumeTotalCount, 4 * 4, ComputeBufferType.Default, ComputeBufferMode.Immutable);
        m_CountVolumeBuffer = new ComputeBuffer(m_VolumeTotalCount, 4, ComputeBufferType.Default, ComputeBufferMode.Immutable);
    }

    public void Dispose()
    {
        if (!m_IsSetup)
        {
            return;
        }

#if VOLUME_RESOURCE_IS_RENDERTEXTURE3D
        if (m_VolumeRenderTexture3D != null)
        {
            m_VolumeRenderTexture3D.DiscardContents();
            m_VolumeRenderTexture3D = null;
        }
#else
        if (m_OctreeIndexBuffer != null)
        {
            m_OctreeIndexBuffer.Release();
            m_OctreeIndexBuffer = null;
        }

        if (m_OctreeBuffer != null)
        {
            m_OctreeBuffer.Release();
            m_OctreeBuffer = null;
        }

        if (m_ColorVolumeBuffer != null)
        {
            m_ColorVolumeBuffer.Release();
            m_ColorVolumeBuffer = null;
        }
#endif

        if (m_CountVolumeBuffer != null)
        {
            m_CountVolumeBuffer.Release();
            m_CountVolumeBuffer = null;
        }

        if (m_UintColorVolumeBuffer != null)
        {
            m_UintColorVolumeBuffer.Release();
            m_UintColorVolumeBuffer = null;
        }

        m_IsSetup = false;
    }

    public int GetVolumeSize()
    {
        return m_VolumeSize;
    }

    public int GetRenderTargetDesity()
    {
        return m_RenderTargetDesity;
    }

    public float GetVoxelSize()
    {
        return m_VoxelSize;
    }

    public int GetVolumeTotalCount()
    {
        return m_VolumeTotalCount;
    }

#if VOLUME_RESOURCE_IS_RENDERTEXTURE3D
    public int GetVolumeMipCount()
    {
        return m_VolumeMipCount;
    }

    public RenderTexture GetVolumeRenderTexture3D()
    {
        return m_VolumeRenderTexture3D;
    }
#else
    public ComputeBuffer GetColorVolumeBuffer()
    {
        return m_ColorVolumeBuffer;
    }

    public ComputeBuffer GetOctreeBuffer()
    {
        return m_OctreeBuffer;
    }

    public ComputeBuffer GetOctreeIndexBuffer()
    {
        return m_OctreeIndexBuffer;
    }
#endif

    public ComputeBuffer GetUintColorVolumeBuffer()
    {
        return m_UintColorVolumeBuffer;
    }

    public ComputeBuffer GetCountVolumeBuffer()
    {
        return m_CountVolumeBuffer;
    }

    private bool m_IsSetup;
    private int m_VolumeSize;
    private int m_RenderTargetDesity;
    private float m_VoxelSize;
    private int m_VolumeTotalCount;

#if VOLUME_RESOURCE_IS_RENDERTEXTURE3D
    private int m_VolumeMipCount;
    private RenderTexture m_VolumeRenderTexture3D;
#else
    private ComputeBuffer m_ColorVolumeBuffer;
    private ComputeBuffer m_OctreeBuffer;
    private ComputeBuffer m_OctreeIndexBuffer;
#endif

    private ComputeBuffer m_UintColorVolumeBuffer;
    private ComputeBuffer m_CountVolumeBuffer;
}

internal enum VoxelGIPossibleVolumeSize
{
    _8 = 8,
    _16 = 16,
    _32 = 32,
    _64 = 64,
    _128 = 128,
};

internal enum VoxelGIRenderTargetDensity
{
    _1 = 1,
    _10 = 10,
    _20 = 20,
    _40 = 40,
}

internal static class VoxelGIShaderPropNames
{
    public const string UINTCOLOR_VOLUME_BUFFER = "UINTCOLOR_VOLUME_BUFFER";
    public const string COUNT_VOLUME_BUFFER = "COUNT_VOLUME_BUFFER";
    public const string COLOR_VOLUME_BUFFER = "COLOR_VOLUME_BUFFER";
    public const string VOLUME_SIZE = "VOLUME_SIZE";
    public const string VOXEL_SIZE = "VOXEL_SIZE";
    public const string OCTREE_BUFFER = "OCTREE_BUFFER";
    public const string OCTREE_INDEX_BUFFER = "OCTREE_INDEX_BUFFER";
    public const string MAIN_CAMERA_WORLD_POS = "MAIN_CAMERA_WORLD_POS";
    public const string VOLUME_RENDER_TEXTURE_3D = "VOLUME_RENDER_TEXTURE_3D";
    public const string VOLUME_MIP_COUNT = "VOLUME_MIP_COUNT";
}

internal static class VoxelGIShaderPropIDs
{
    public readonly static int UINTCOLOR_VOLUME_BUFFER = Shader.PropertyToID(VoxelGIShaderPropNames.UINTCOLOR_VOLUME_BUFFER);
    public readonly static int COUNT_VOLUME_BUFFER = Shader.PropertyToID(VoxelGIShaderPropNames.COUNT_VOLUME_BUFFER);
    public readonly static int COLOR_VOLUME_BUFFER = Shader.PropertyToID(VoxelGIShaderPropNames.COLOR_VOLUME_BUFFER);
    public readonly static int VOLUME_SIZE = Shader.PropertyToID(VoxelGIShaderPropNames.VOLUME_SIZE);
    public readonly static int VOXEL_SIZE = Shader.PropertyToID(VoxelGIShaderPropNames.VOXEL_SIZE);
    public readonly static int OCTREE_BUFFER = Shader.PropertyToID(VoxelGIShaderPropNames.OCTREE_BUFFER);
    public readonly static int OCTREE_INDEX_BUFFER = Shader.PropertyToID(VoxelGIShaderPropNames.OCTREE_INDEX_BUFFER);
    public readonly static int MAIN_CAMERA_WORLD_POS = Shader.PropertyToID(VoxelGIShaderPropNames.MAIN_CAMERA_WORLD_POS);
    public readonly static int VOLUME_RENDER_TEXTURE_3D = Shader.PropertyToID(VoxelGIShaderPropNames.VOLUME_RENDER_TEXTURE_3D);
    public readonly static int VOLUME_MIP_COUNT = Shader.PropertyToID(VoxelGIShaderPropNames.VOLUME_MIP_COUNT);
}

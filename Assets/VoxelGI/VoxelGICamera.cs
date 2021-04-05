using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class VoxelGICamera : MonoBehaviour
{
    public static void RenderCurrentFrame()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (s_Instance == null)
        {
            Debug.LogError($"[VoxelGICamera] there is no instance.");
        }
#endif

        s_Instance.RenderCurrentFrameInternal();
    }

    public static void UpdateCameraSettings()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (s_Instance == null)
        {
            Debug.LogError($"[VoxelGICamera] there is no instance.");
        }
#endif

        s_Instance.UpdateCameraSettingsInternal();
    }

    public static Vector3 GetMainCameraWorldPos()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (s_Instance == null)
        {
            Debug.LogError($"[VoxelGICamera] there is no instance.");
        }
#endif

        return s_Instance.m_VolumeOrigin;
    }

#if UNITY_EDITOR
    public static bool IsExist()
    {
        return s_Instance != null;
    }
#endif

    [SerializeField] private bool _RenderEveryFrame = false;
    [SerializeField] private Vector3 _CameraOffset = Vector3.zero;

    private void OnEnable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (s_Instance != null)
        {
            Debug.LogError($"[VoxelGICamera] multiple instances have been discovered. you need to leave one single instance and remove others.");
        }
#endif

        s_Instance = this;
        m_Camera = GetComponent<Camera>();
        m_GraphicResource = (GetComponent<UniversalAdditionalCameraData>().scriptableRenderer as VoxelGIForwardRenderer).GetGraphicResource();
        m_MainCameraTransform = Camera.main.transform;
        UpdateCameraSettingsInternal();
        FollowMainCamera();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (s_Instance != this)
        {
            Debug.LogError($"[VoxelGICamera] multiple instances have been discovered. you need to leave one single instance and remove others.");
        }
#endif

        s_Instance = null;
    }

    private void Update()
    {
        if (_RenderEveryFrame == false)
        {
            return;
        }

        FollowMainCamera();
        RenderCurrentFrameInternal();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // tpdp" Draw volume area...
    }
#endif

    private void UpdateCameraSettingsInternal()
    {
        float physicalSize = m_GraphicResource.GetVolumeSize() * m_GraphicResource.GetVoxelSize();
        m_Camera.enabled = false;
        m_Camera.nearClipPlane = 0f;
        m_Camera.farClipPlane = physicalSize;
        m_Camera.orthographic = true;
        m_Camera.orthographicSize = physicalSize * 0.5f;

        var renderTarget = m_Camera.targetTexture;

        if (renderTarget != null)
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying)
            {
                GameObject.Destroy(renderTarget);
            }
            else
            {
                GameObject.DestroyImmediate(renderTarget);
            }

            m_Camera.targetTexture = null;
#else
            GameObject.Destroy(renderTarget);
            m_Camera.targetTexture = null;
#endif
        }

        int texelSize = m_GraphicResource.GetVolumeSize() * m_GraphicResource.GetRenderTargetDesity();
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(texelSize, texelSize, RenderTextureFormat.ARGB32);
        descriptor.useMipMap = false;
        descriptor.depthBufferBits = 32;
        renderTarget = new RenderTexture(descriptor);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        renderTarget.name = $"{texelSize} X {texelSize}";
#endif

        m_Camera.targetTexture = renderTarget;
    }

    private void FollowMainCamera()
    {
        var voxelSize = m_GraphicResource.GetVoxelSize();
        m_VolumeOrigin = m_MainCameraTransform.position + _CameraOffset;
        m_VolumeOrigin.x = (float)Mathf.RoundToInt(m_VolumeOrigin.x / voxelSize) * voxelSize;
        m_VolumeOrigin.y = (float)Mathf.RoundToInt(m_VolumeOrigin.y / voxelSize) * voxelSize;
        m_VolumeOrigin.z = (float)Mathf.RoundToInt(m_VolumeOrigin.z / voxelSize) * voxelSize;

        var volumeCameraPos = m_VolumeOrigin;
        volumeCameraPos.z -= 0.5f * m_GraphicResource.GetVolumeSize() * voxelSize;
        transform.SetPositionAndRotation(volumeCameraPos, Quaternion.identity);
    }

    private void RenderCurrentFrameInternal()
    {
        if (m_Camera == null || m_Camera.targetTexture == null)
        {
            return;
        }

        m_Camera.Render();
    }

    private Camera m_Camera;
    private VoxelGIGraphicResources m_GraphicResource;
    private Transform m_MainCameraTransform;
    private Vector3 m_VolumeOrigin = Vector3.zero;

    private static VoxelGICamera s_Instance = null;
}

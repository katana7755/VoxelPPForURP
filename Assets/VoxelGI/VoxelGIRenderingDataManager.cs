using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class VoxelGIRenderingDataManager : MonoBehaviour
{
    public static int GetDataCount()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (s_Instance == null)
        {
            Debug.LogError($"[VoxelGIRenderManager] there is no instance.");

            return 0;
        }
#endif

        return s_Instance.GetDataCountInternal();
    }

    public static VoxelGIRendereringData GetDataAt(int index)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (s_Instance == null)
        {
            Debug.LogError($"[VoxelGIRenderManager] there is no instance.");

            return null;
        }
#endif

        return s_Instance.GetDataAtInternal(index);
    }

    public static void CollectAllFromCurrentScene()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (s_Instance == null)
        {
            Debug.LogError($"[VoxelGIRenderManager] there is no instance.");

            return;
        }
#endif

        s_Instance.CollectAllFromCurrentSceneInternal();
    }

    public static void ClearAll()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (s_Instance == null)
        {
            Debug.LogError($"[VoxelGIRenderManager] there is no instance.");

            return;
        }
#endif

        s_Instance.ClearAllInternal();
    }

    [SerializeField] private List<VoxelGIRendereringData> _RendererList;

    private void OnEnable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (s_Instance != null)
        {
            Debug.LogError($"[VoxelGIRenderManager] multiple instances have been discovered. you need to leave one single instance and remove others.");
        }
#endif

        s_Instance = this;
    }

    private void OnDisable()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (s_Instance != this)
        {
            Debug.LogError($"[VoxelGIRenderManager] multiple instances have been discovered. you need to leave one single instance and remove others.");
        }
#endif

        s_Instance = null;
    }

    private int GetDataCountInternal()
    {
        return (_RendererList != null) ? _RendererList.Count : 0;
    }

    private VoxelGIRendereringData GetDataAtInternal(int index)
    {
        if (_RendererList == null)
        {
            return null;
        }

        if (index < 0 || index >= _RendererList.Count)
        {
            return null;
        }

        return _RendererList[index];
    }

    private void CollectAllFromCurrentSceneInternal()
    {
        if (_RendererList == null)
        {
            return;
        }

        _RendererList.Clear();

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var rootGameObjects = scene.GetRootGameObjects();

        foreach (var rootGO in rootGameObjects)
        {
            var renderers = rootGO.GetComponentsInChildren<Renderer>();

            foreach (var renderer in renderers)
            {
                var meshFilter = renderer.GetComponent<MeshFilter>();

                if (meshFilter == null)
                {
                    continue;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (meshFilter.sharedMesh.subMeshCount != renderer.sharedMaterials.Length)
                {
                    Debug.LogError($"[VoxelGIRenderManager] {renderer.name} has different numbers of submeshes and materials.");
                }
#endif

                int count = meshFilter.sharedMesh.subMeshCount;

                for (int i = 0; i <  meshFilter.sharedMesh.subMeshCount; ++i)
                {
                    if (i >= renderer.sharedMaterials.Length)
                    {
                        continue;
                    }

                    var material = renderer.sharedMaterials[i];
                    var voxelRenderer = new VoxelGIRendereringData(renderer.transform, meshFilter.sharedMesh, i, material.mainTexture, material.color);
                    _RendererList.Add(voxelRenderer);
                }
                foreach (var material in renderer.sharedMaterials)
                {

                }
            }
        }
    }

    private void ClearAllInternal()
    {
        _RendererList.Clear();
    }

    private static VoxelGIRenderingDataManager s_Instance = null;

    [System.Serializable]
    public class VoxelGIRendereringData
    {
        [SerializeField] private Transform _Transform;
        [SerializeField] private Mesh _Mesh;
        [SerializeField] private int _SubMeshIndex;
        [SerializeField] private Texture _Texture;
        [SerializeField] private Color _TintColor;

        public VoxelGIRendereringData(Transform tf, Mesh mesh, int subMeshIndex, Texture mainTexture, Color tintColor)
        {
            _Transform = tf;
            _Mesh = mesh;
            _SubMeshIndex = subMeshIndex;
            _Texture = mainTexture;
            _TintColor = tintColor;
        }

        ~VoxelGIRendereringData()
        {
            ReleaseTemporaryMaterial();
        }

        public bool IsActive()
        {
            return _Transform.gameObject.activeSelf;
        }

        public Mesh GetMesh()
        {
            return _Mesh;
        }

        public int GetSubMeshIndex()
        {
            return _SubMeshIndex;
        }

        public Matrix4x4 GetModelMatrix()
        {
            return _Transform.localToWorldMatrix;
        }

        public Material GetMatrial(Shader baseShader)
        {
            if (m_TemporaryMaterial == null || m_TemporaryMaterial.shader != baseShader)
            {
                ReleaseTemporaryMaterial();
                m_TemporaryMaterial = new Material(baseShader);
                m_TemporaryMaterial.mainTexture = _Texture;
                m_TemporaryMaterial.color = _TintColor;
            }

            return m_TemporaryMaterial;
        }

        private void ReleaseTemporaryMaterial()
        {
#if UNITY_EDITOR
            if (m_TemporaryMaterial != null)
            {
                if (UnityEditor.EditorApplication.isPlaying)
                {
                    GameObject.Destroy(m_TemporaryMaterial);
                    m_TemporaryMaterial = null;
                }
                else
                {
                    GameObject.DestroyImmediate(m_TemporaryMaterial);
                    m_TemporaryMaterial = null;
                }
            }
#else
            if (m_TemporaryMaterial != null)
            {
                GameObject.DestroyImmediate(m_TemporaryMaterial);
                m_TemporaryMaterial = null;
            }
#endif
        }

        private GameObject m_GameObject = null;
        private Material m_TemporaryMaterial = null;
    }
}

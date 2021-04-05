using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VoxelGIForwardRendererData : ScriptableRendererData
{
#if UNITY_EDITOR
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812")]
    internal class CreateForwardRendererAsset : UnityEditor.ProjectWindowCallback.EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            var instance = CreateInstance<VoxelGIForwardRendererData>();
            UnityEditor.AssetDatabase.CreateAsset(instance, pathName);
            UnityEditor.Selection.activeObject = instance;
        }
    }

    [UnityEditor.MenuItem("Assets/Create/Voxel GI/Forward Renderer", priority = CoreUtils.assetCreateMenuPriority2)]
    static void CreateForwardRendererData()
    {
        UnityEditor.ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, CreateInstance<CreateForwardRendererAsset>(), "VoxelGIForwardRenderer.asset", null, null);
    }
#endif

    public VoxelGIGraphicResources                                                  _GraphicResources;
    public VoxelGIForwardRenderer.DrawAllIntoVolumeRenderPass.SerializableSettings  _SettingsForDrawAllIntoVolume;

    protected override ScriptableRenderer Create()
    {
        return new VoxelGIForwardRenderer(this);
    }
}

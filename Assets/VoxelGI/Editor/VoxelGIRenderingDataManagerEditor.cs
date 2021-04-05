using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(VoxelGIRenderingDataManager), true)]
public class VoxelGIRenderingDataManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        GUILayout.BeginVertical();

        if (GUILayout.Button("Collect All From Scene"))
        {
            VoxelGIRenderingDataManager.CollectAllFromCurrentScene();
            EditorUtility.SetDirty(serializedObject.targetObject);
        }

        if (GUILayout.Button("Clear All"))
        {
            VoxelGIRenderingDataManager.ClearAll();
            EditorUtility.SetDirty(serializedObject.targetObject);
        }

        GUILayout.EndVertical();
    }
}

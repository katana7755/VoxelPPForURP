using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(VoxelGIGraphicResources))]
public class VoxelGIGraphicResourcesEditor : Editor
{
    private void OnEnable()
    {
        EditorApplication.update += ApplyChangedSetting;
    }

    private void OnDisable()
    {
        EditorApplication.update -= ApplyChangedSetting;
    }

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        base.OnInspectorGUI();

        if (EditorGUI.EndChangeCheck())
        {
            m_IsNeededToUpdate = true;
        }
    }

    private void ApplyChangedSetting()
    {
        if (!m_IsNeededToUpdate)
        {
            return;
        }

        if (Time.realtimeSinceStartup < m_LastAppliedTime + MINIMUM_DELAY)
        {
            return;
        }

        m_IsNeededToUpdate = false;
        m_LastAppliedTime = Time.realtimeSinceStartup;

        var graphicsResource = serializedObject.targetObject as VoxelGIGraphicResources;
        graphicsResource.ApplyCurrentSettings();
    }

    private float m_LastAppliedTime = -1f;
    private bool m_IsNeededToUpdate = false;

    private const float MINIMUM_DELAY = 1f;
}

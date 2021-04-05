using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(VoxelGIForwardRendererData), true)]
public class VoxelGIForwardRendererDataEditor : Editor
{
    private static class Styles
    {
        public static readonly GUIContent RenderTitle = new GUIContent("Voxel GI Forward Renderer");
        public static readonly GUIContent GraphicResource = new GUIContent("Graphic Resources");
        public static readonly GUIContent SettingsForDrawAllIntoVolume = new GUIContent("Settings For Draw All Into Volume");
    }

    private void OnEnable()
    {
        m_GraphicResources = serializedObject.FindProperty("_GraphicResources");
        m_SettingsForDrawAllIntoVolume = serializedObject.FindProperty("_SettingsForDrawAllIntoVolume");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(Styles.RenderTitle, EditorStyles.boldLabel); // Title
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(m_GraphicResources, Styles.GraphicResource);
        EditorGUILayout.PropertyField(m_SettingsForDrawAllIntoVolume, Styles.SettingsForDrawAllIntoVolume);
        EditorGUI.indentLevel--;
        EditorGUILayout.Space();

        serializedObject.ApplyModifiedProperties();
    }

    private SerializedProperty m_GraphicResources;
    private SerializedProperty m_SettingsForDrawAllIntoVolume;
}

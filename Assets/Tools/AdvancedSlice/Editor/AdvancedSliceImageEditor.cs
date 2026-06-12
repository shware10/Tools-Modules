using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

[CustomEditor(typeof(AdvancedSliceImage))]
public class AdvancedSliceImageEditor : ImageEditor
{
    SerializedProperty sliceModeProp;
    
    protected override void OnEnable()
    {
        base.OnEnable();
        
        sliceModeProp = serializedObject.FindProperty("_sliceMode");
    }
    
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        
        serializedObject.Update();
        
        EditorGUILayout.Space();
        
        EditorGUILayout.LabelField(
            "Advanced Slice",
            EditorStyles.boldLabel);
        
        EditorGUILayout.PropertyField(sliceModeProp);
        
        AdvancedSliceImage image = (AdvancedSliceImage)target;
        
        EditorGUILayout.Space();
        
        if(GUILayout.Button("Open Advanced Slice Editor"))
        {
            AdvancedSliceEditorWindow.Open(
                image.overrideSprite ?? image.sprite);
        }
        
        serializedObject.ApplyModifiedProperties();
    }
}

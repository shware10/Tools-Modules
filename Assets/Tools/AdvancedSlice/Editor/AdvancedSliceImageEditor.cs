using UnityEditor;
using UnityEditor.UI;

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
        
        serializedObject.ApplyModifiedProperties();
    }
}

using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

/// <summary>
/// AdvancedSliceImage 전용 Custom Inspector
/// 기본 Image Inspector를 유지하면서
/// SliceMode, FillMode 및 Slice Editor 기능을 추가
/// </summary>
[CustomEditor(typeof(AdvancedSliceImage))]
public class AdvancedSliceImageEditor : ImageEditor
{
    // AdvancedSliceImage의 SerializeField와 연결되는 Property
    private SerializedProperty sliceModeProp;
    private SerializedProperty fillModeProp;
    private SerializedProperty fillAmountProp;
    
    /// <summary>
    /// Custom Editor가 활성화될 때 호출
    /// AdvancedSliceImage의 직렬화 필드와 SerializedProperty를 연결
    /// </summary>
    protected override void OnEnable()
    {
        // 부모 ImageEditor 초기화
        base.OnEnable();
        
        // AdvancedSliceImage의 SerializeField 검색 및 연결
        sliceModeProp   = serializedObject.FindProperty("_sliceMode");
        fillModeProp    = serializedObject.FindProperty("_fillMode");
        fillAmountProp  = serializedObject.FindProperty("_fillAmount");
    }
    
    /// <summary>
    /// AdvancedSliceImage의 Inspector GUI를 그리는 함수
    /// </summary>
    public override void OnInspectorGUI()
    {
        // Unity 기본 Image Inspector 먼저 출력
        base.OnInspectorGUI();
        
        // 실제 객체의 값을 SerializedObject에 동기화
        serializedObject.Update();
        
        EditorGUILayout.Space();
        
        // Advanced Slice 설정
        EditorGUILayout.LabelField("Advanced Slice", EditorStyles.boldLabel);
        
        AdvancedSliceImage image = (AdvancedSliceImage)target;
        
        // SliceMode 변경 감지
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.PropertyField(sliceModeProp);

        if(EditorGUI.EndChangeCheck())
        {
            // 변경된 SliceMode를 실제 객체에 먼저 적용
            serializedObject.ApplyModifiedProperties();

            Sprite sprite = image.overrideSprite ?? image.sprite;
            
            if(sprite != null)
            {
                // SliceMode 변경 시 기존 Slice 위치 초기화
                AdvancedSliceData defaultData = AdvancedSliceImporterUtil.GenerateDefault(sprite);

                // 초기화된 데이터를 Sprite에 저장
                AdvancedSliceImporterUtil.Save(sprite, defaultData);
            }
        }
        
        // 현재 Sprite와 SliceMode를 전달하여
        // Advanced Slice EditorWindow 실행
        if(GUILayout.Button("Open Advanced Slice Editor"))
        {
            AdvancedSliceEditorWindow.Open(image.overrideSprite ?? image.sprite, image.SliceMode);
        }
        
        EditorGUILayout.Space();
        
        // Fill 설정
        EditorGUILayout.LabelField("+FillMode", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(fillModeProp);
        
        // Fill을 사용하는 경우에만 FillAmount 표시
        if((AdvancedFillMode)fillModeProp.enumValueIndex != AdvancedFillMode.None)
        {
            EditorGUILayout.PropertyField(fillAmountProp);
        }

        // Inspector에서 변경된 SerializedProperty 값을
        // 실제 AdvancedSliceImage에 적용
        serializedObject.ApplyModifiedProperties();
    }
}
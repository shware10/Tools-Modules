using UnityEditor;
using UnityEngine;


public class MeshBuilderWindow : EditorWindow
{
    private GameObject curObj;
    
    private string meshName;
    private Mesh targetMesh;

    [MenuItem("Tools/Shware/MeshBuilder For Effects")]
    public static void Open() => GetWindow<MeshBuilderWindow>("MeshBuilder For Effects");
    private enum ShapeType
    {
        Cone,
        Ring,
        Spiral
    }
    
    ShapeType shapeType;
    
    void OnGUI()
    {
        EditorGUILayout.Space(20);
        
        EditorGUI.BeginChangeCheck();
        
        CenterLabel("Effects Shape Type");
        // shape 선택 바
        shapeType = (ShapeType)GUILayout.Toolbar
            (
                (int)shapeType,
                new string[] { "Cone", "Ring", "Spiral"}
            );
            
        EditorGUILayout.Space(20);
        
        // 선택한 shape에 따른 처리
        if(GUILayout.Button("Generate"))
        {
            switch(shapeType)
            {
                case ShapeType.Cone:
                    GenerateCone();
                    break;
                case ShapeType.Ring:
                    GenerateBand();
                    break;
                case ShapeType.Spiral:
                    GenerateSpiral();
                    break;
            }
        }
        
        EditorGUILayout.Space(20);
        
        meshName = EditorGUILayout.TextField("Mesh Name", meshName);
        
        if(GUILayout.Button("Save"))
        {
            SaveMesh();
        }
    }
    
    void OnDestroy()
    {
        if (curObj != null)
            DestroyImmediate(curObj);
    }
    
    private void CenterLabel(string text, params GUILayoutOption[] options)
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label(text, options);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }
    
    private void SetMeshObject(string objectName)
    {
        if(curObj != null) DestroyImmediate(curObj);
        
        curObj = new GameObject(objectName);
        MeshFilter filter = curObj.AddComponent<MeshFilter>();
        MeshRenderer rdr = curObj.AddComponent<MeshRenderer>();
        
        filter.mesh = new Mesh();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        rdr.material = mat;
    }
    
    private void GenerateCone()
    {
        SetMeshObject("__Mesh_Cone");
        curObj.AddComponent<MeshCone>();
    }
    
    private void GenerateBand()
    {
        SetMeshObject("__Mesh_Band");
        curObj.AddComponent<MeshBand>();
    }
    
    private void GenerateSpiral()
    {
        SetMeshObject("__Mesh_Spiral");
        curObj.AddComponent<MeshSpiral>();
    }
    
    private void SaveMesh()
    {
        if(curObj == null) return;
        
        MeshFilter filter = curObj.GetComponent<MeshFilter>();
        
        string path = GeneratePath();
        path = AssetDatabase.GenerateUniqueAssetPath(path); // 중복 방지
        
        Mesh meshCopy = Instantiate(filter.sharedMesh);
        meshCopy.name = meshName;
        
        AssetDatabase.CreateAsset(meshCopy, path);
        AssetDatabase.SaveAssets();
    }
    private string GeneratePath()  
    {
        // 현재 스크립트가 위치한 폴더의 상위 폴더 위치
        string baseFolder = GetThisScriptUpperFolderPath();
	
        string generatedRoot = $"{baseFolder}/Generated";   
	    
        if (!AssetDatabase.IsValidFolder(generatedRoot)) 
        {
            AssetDatabase.CreateFolder(baseFolder, "Generated");  // generated 폴더가 없으면 생성해 줍니다.
        }
        
        meshName = string.IsNullOrEmpty(meshName) ? "New_Mesh" : meshName;
	
        string path = $"{generatedRoot}/{meshName}.asset";
	
        return path;    // 최종 메쉬 경로 리턴
    }
    
    private string GetThisScriptUpperFolderPath()             
    {
        string scriptPath = AssetDatabase.GetAssetPath(     // 이 스크립트의 Asset/ 기준 상대경로를 가져오기
            MonoScript.FromScriptableObject(this));
	    
	    string folder = System.IO.Path.GetDirectoryName(scriptPath); // 현재 스크립트가 담긴 폴더 위치
	    
        return  System.IO.Path.GetDirectoryName(folder); // 현재 폴더의 상위 폴더 위치
    }
    
}
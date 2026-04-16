using Image = UnityEngine.UI.Image;
using UnityEditor;
using UnityEngine;
using Color = UnityEngine.Color;
using Graphics = UnityEngine.Graphics;

public class MSDFBakeryWindow : EditorWindow
{
    [SerializeField] private Texture2D source;
    [SerializeField] private float alphaThreshold = 0.5f;
    [SerializeField] private int spread = 1;    // outline 폭
    [SerializeField] private int softness = 15; // outline 페더
    [SerializeField] private Color outlineColor = Color.yellow; // Outline 컬러
    private int tileSize = 16;
    private int maxDistance = 16;
    
    private Texture2D msdf;
    private Material previewMaterial;
    private RenderTexture canvasRT;
    private Camera canvasCam;
    private Canvas canvas;
    private Image image;
    private GameObject root;
    
    [MenuItem("Tools/Shware/MSDF Bakery")]
    public static void Open() => GetWindow<MSDFBakeryWindow>("MSDF Bakery");
    
    private void OnGUI()
    {
        EditorGUILayout.Space(20);
        
        source = (Texture2D)EditorGUILayout.ObjectField(
            "Source (RGBA PNG)",
            source,
            typeof(Texture2D),
            false
        );
        
        alphaThreshold = EditorGUILayout.Slider(
            "Alpha Threshold", 
            alphaThreshold, 
            0.01f, 
            0.99f
        );
        
        spread = EditorGUILayout.IntSlider(
            "Outline Spread (px)", 
            spread, 
            1, 
            maxDistance-1
        );
        
        softness = EditorGUILayout.IntSlider(
            "Outline Softness (px)", 
            softness,
            0,
            maxDistance-spread
        );
        
        outlineColor = EditorGUILayout.ColorField(
            "Outline Color",
            outlineColor
        );


        EditorGUILayout.Space(20);
        
        // 윈도우에 버튼을 생성해 줍니다.
        using (new EditorGUI.DisabledScope(source == null))
        {
            if (GUILayout.Button("Bake MSDF", GUILayout.Height(32)))
            {
                // 버튼을 누르면 실행될 MSDF Bake함수 입니다.
                Bake();
            }
        }
        
        EditorGUILayout.Space(20);
        
        if (previewMaterial != null && msdf != null)
        {
            // 필드 값이 변하면 실시간의로 머터리얼의 값을 변경해주어 변화를 확인할 수 있도록 합니다.
            previewMaterial.SetFloat("_OutlineWidth", spread);
            previewMaterial.SetFloat("_Softness", softness);
            previewMaterial.SetFloat("_MaxDistance", maxDistance);
            previewMaterial.SetColor("_OutlineColor", outlineColor);
        }
        // 프리뷰를 그려줍니다.
        DrawPreview();  
        
        EditorGUILayout.Space(20);
      
        using (new EditorGUI.DisabledScope(msdf == null))
        {
            if(GUILayout.Button("Save MSDF", GUILayout.Height(32) ))
            {
                //세이브 버튼을 누르면 저장해줍니다.
                Save();
            }
        }
    }
    
    private void OnDisable()
    {
        if (msdf != null) DestroyImmediate(msdf);
        if (previewMaterial != null) DestroyImmediate(previewMaterial);
        
        if (canvasRT != null) canvasRT.Release();
        if (root != null) DestroyImmediate(root);
    }

    #region Bake MSDF
    private void Bake()
    {
        if (source == null) return;
		
        // 현재 사용할 타겟이 될 메인 텍스쳐를 RenderTexture를 통해 복사합니다.
        Texture2D work = CopyViaRenderTexture(source);
    
        // 전에 쓰던 msdf가 있다면 제거해 줍니다.
        if (msdf!= null) DestroyImmediate(msdf);
		
        // BakeMSDF를 실행 시 복사한 readable texture를 인자로 보내주어 msdf 텍스쳐를 생성합니다.
        msdf= MSDFCore.BakeMSDF(
            work,
            alphaThreshold,
            maxDistance,
            tileSize
        );

        // MSDF와 source를 프리뷰 셰이더에 적용합니다.
        PreparePreviewMaterial();
    
        // 베이크를 위해 복사했던 텍스쳐를 제거합니다.
        DestroyImmediate(work);
    }
    
    // RenderTexture를 통해 복사하는 함수
    private Texture2D CopyViaRenderTexture(Texture2D src)
    {
        // 텍스쳐 크기의 rt를 생성해 줍니다.
        RenderTexture rt = RenderTexture.GetTemporary(
            src.width,
            src.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear
        );
        // GPU 메모리 source를 GPU rt로 복사합니다.
        Graphics.Blit(src, rt);
    
        // 활성 RT로 만들어 줍니다. 활성 RT만 cpu Texture로 복사할 수 있습니다.
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
		
        // 새로운 텍스쳐를 만들어 줍니다.
        Texture2D tex = new Texture2D(
            src.width,
            src.height,
            TextureFormat.ARGB32,
            false,
            true
        );
		
        // RT를 읽어 텍스트에 적용해 줍니다.
        tex.ReadPixels(new Rect(0, 0, src.width, src.height),0,0);
        tex.Apply();
		
        // 이전 상태로 돌려주고, RT는 메모리에서 해제해줍니다.
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        return tex;
    }
    
    private void PreparePreviewMaterial()
    {
        // 프리뷰 머터리얼을 생성합니다.
        if(previewMaterial == null)
        {
            Shader shader = Shader.Find("Shader Graphs/PreviewShader");
            previewMaterial = new Material(shader);
            previewMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
		
        // 셰이더의 msdf텍스쳐에 생성한 msdf텍스쳐를 넣어줍니다.
        previewMaterial.SetTexture("_MSDFTex", msdf);
        // 에디터 윈도우의 필드값으로 받은 값들을 넣어줍니다.
        previewMaterial.SetColor("_OutlineColor", outlineColor);
        previewMaterial.SetFloat("_MaxDistance", maxDistance);
        previewMaterial.SetFloat("_OutlineWidth", spread);
        previewMaterial.SetFloat("_Softness", softness);
    }
    
    #endregion 
    
    #region Preview
    private void DrawPreview()
    {
        // 생성한 msdf텍스쳐가 없거나 프리뷰 머터리얼이 없으면 리턴합니다.
        if (msdf == null || previewMaterial == null) return;

        GUILayout.Label("Preview", EditorStyles.boldLabel);
        // 텍스쳐를 적용할 캔버스 상의 이미지를 생성해줍니다.
        PrepareImage();
    
        // 현재 보고있는 RT를 렌더합니다.
        canvasCam.Render();

        float aspect = (float)source.width / source.height;

        float width = source.width;
        float height = width / aspect;
		
        // 소스 크기의 Rect를 생성합니다.
        Rect rect = GUILayoutUtility.GetRect(
            width,
            height,
            GUILayout.ExpandWidth(false)
        );
		
        // 프리뷰 상에 RT를 보여줍니다.
        EditorGUI.DrawPreviewTexture(
            rect,
            canvasRT,
            null,
            ScaleMode.ScaleToFit
        );
    }
    
    private void PrepareImage()
    {
        if(root != null) return;
        root = new GameObject("__MSDF_Preview");
        root.hideFlags = HideFlags.HideAndDontSave;
        // 카메라로 확인할 렌더 텍스쳐입니다.
        canvasRT = new RenderTexture(source.width, source.height, 24, RenderTextureFormat.ARGB32);
        canvasRT.hideFlags = HideFlags.HideAndDontSave;
    
        // 카메라 오브젝트입니다.
        GameObject camGO = new GameObject("__MSDF_Camera");
        camGO.transform.SetParent(root.transform);
        camGO.hideFlags = HideFlags.HideAndDontSave;
    
        // 카메라 컴포넌트를 추가해주고 투명배경 설정과 ortho 설정을 해줍니다.
        canvasCam = camGO.AddComponent<Camera>();
        canvasCam.clearFlags = CameraClearFlags.SolidColor;
        canvasCam.backgroundColor = Color.clear;
        canvasCam.orthographic = true;
        canvasCam.orthographicSize = source.height / 2f;
        canvasCam.targetTexture = canvasRT;
    
        // 캔버스 오브젝트입니다.
        GameObject canvasGO = new GameObject("__MSDF_Canvas");
        canvasGO.transform.SetParent(root.transform);
        canvasGO.hideFlags = HideFlags.HideAndDontSave;
    
        // 캔버스를 렌더할 카메라를 지정해줍니다.
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = canvasCam;
    
        // 이미지 오브젝트입니다.
        GameObject imageGO = new GameObject("__MSDF_Image");
        imageGO.transform.SetParent(canvas.transform, false);
        imageGO.hideFlags = HideFlags.HideAndDontSave;
    
        // 이미지 컴포넌트 할당 후 이미지 크기를 소스 크기로 설정해줍니다.
        image = imageGO.AddComponent<Image>();
        image.rectTransform.sizeDelta = new Vector2(source.width, source.height);
    
        // 소스를 스프라이트로 넣어줍니다.
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            AssetDatabase.GetAssetPath(source)
        );
        // 이미지의 머터리얼에 생성한 셰이더의 머터리얼을 넣어줍니다. 메인텍스쳐가 source가 됩니다.
        image.material = previewMaterial;
    }
    
    #endregion
    
    #region Save MSDF
    private void Save()
    {
        if (msdf == null) return;
        //msdf의 이름을 바꿔줍니다.
        msdf.name = source.name + "_MSDF";
    
        // 이미지의 AssetDataBase상의 path를 생성합니다.
        string assetPath = GeneratePath(msdf.name);
        // 디스크 상의 fullPath를 생성합니다.
        string fullPath = System.IO.Path.GetFullPath(assetPath);
    
        // msdf를 png 파일로 인코딩해 저장해 줍니다.
        byte[] png = msdf.EncodeToPNG();
        System.IO.File.WriteAllBytes(fullPath, png);
        // 디스크 상에만 존재하기 때문에 리프레시를 통해 에셋데이터 베이스에 등록해줍니다.
        AssetDatabase.Refresh();
		
        // 임포터에 접근합니다.
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
    
        if (importer == null)
        {
            Debug.LogWarning($"MSDF Bakery: TextureImporter not found at {assetPath}");
            return;
        }
        // 임포터 설정을 해줍니다. sRGB를 false로 해 감마 연산을 하지 않도록 해줍니다.
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.isReadable = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        // 설정을 meta데이터에 반영해줍니다.
        importer.SaveAndReimport();
    }
    

    private string GeneratePath(string srcName)
    {
        // EditorWindow 스크립트가 존재하는 AssetDatabase상의 폴더 경로를 가져옵니다.
        string baseFolder = GetThisScriptFolderPath();
		
        string generatedRoot = $"{baseFolder}/Generated";
        // Generated 폴더사 없으면 생성해줍니다.
        if (!AssetDatabase.IsValidFolder(generatedRoot))
        {
            AssetDatabase.CreateFolder(baseFolder, "Generated");
        }
        // msdf의 파일이름으로 경로를 생성해줍니다.
        string path = AssetDatabase.GenerateUniqueAssetPath(
            $"{generatedRoot}/{srcName}.png");
        return path;
    }
    
    private string GetThisScriptFolderPath()
    {
        string scriptPath = AssetDatabase.GetAssetPath(
            MonoScript.FromScriptableObject(this));
        // 스크립트의 상위 폴더 경로를 리턴합니다.
        return System.IO.Path.GetDirectoryName(scriptPath);
    }

    #endregion
}

using System;
using UnityEditor;
using UnityEngine;

public class MSDFBakeryWindow : EditorWindow
{
    [SerializeField] private Texture2D source;
    [SerializeField] private float alphaThreshold = 0.5f;
    [SerializeField] private int spread = 1;    // outline 폭
    [SerializeField] private int softness = 15; // outline 페더
    [SerializeField] private Color outlineColor = Color.yellow; // Outline 컬러
    private int tileSize = 16;
    private int maxDistance = 16;
    
    [NonSerialized] private Texture2D preview;
    [NonSerialized] private Material previewMaterial;
    
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
        
        using (new EditorGUI.DisabledScope(source == null))
        {
            if (GUILayout.Button("Bake MSDF", GUILayout.Height(32)))
            {
                Bake();
            }
        }
        
        EditorGUILayout.Space(20);
        
        if (previewMaterial != null && preview != null)
        {
            previewMaterial.SetFloat("_OutlineWidth", spread);
            previewMaterial.SetFloat("_Softness", softness);
            previewMaterial.SetFloat("_MaxDistance", maxDistance);
            previewMaterial.SetColor("_OutlineColor", outlineColor);
        }
        DrawPreview();
        
        EditorGUILayout.Space(20);
        
        using (new EditorGUI.DisabledScope(preview == null))
        {
            if(GUILayout.Button("Save MSDF", GUILayout.Height(32) ))
            {
                Save();
            }
        }
    }
    
    private void OnDisable()
    {
        if (preview != null) DestroyImmediate(preview);
        if (previewMaterial != null) DestroyImmediate(previewMaterial);
    }

    private void Bake()
    {
        if (source == null) return;

        Texture2D work = CopyViaRenderTexture(source);
        
        //전에 쓰던 프리뷰있으면 제거
        if (preview != null) DestroyImmediate(preview);

        preview = MSDFCore.BakeMSDF(
            work,
            alphaThreshold,
            maxDistance,
            tileSize
        );

        preview.name = source.name + "_MSDF";
        preview.hideFlags = HideFlags.HideAndDontSave;

        PreparePreviewMaterial();
        
        //사용한 텍스쳐 제거
        DestroyImmediate(work);
    }
    
    private void Save()
    {
        if (preview == null) return;
        
        string assetPath = GeneratePath(preview.name);
        string fullPath = System.IO.Path.GetFullPath(assetPath);
        
        byte[] png = preview.EncodeToPNG();
        System.IO.File.WriteAllBytes(fullPath, png);
        AssetDatabase.Refresh();

        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        
        if (importer == null)
        {
            Debug.LogWarning($"MSDF Bakery: TextureImporter not found at {assetPath}");
            return;
        }
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.isReadable = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
    
    private string GeneratePath(string srcName)
    {
        string baseFolder = GetThisScriptFolderPath();

        string generatedRoot = $"{baseFolder}/Generated";
        
        if (!AssetDatabase.IsValidFolder(generatedRoot))
        {
            AssetDatabase.CreateFolder(baseFolder, "Generated");
        }
        string path = AssetDatabase.GenerateUniqueAssetPath(
            $"{generatedRoot}/{srcName}.png");
        return path;
    }
    
    private string GetThisScriptFolderPath()
    {
        string scriptPath = AssetDatabase.GetAssetPath(
            MonoScript.FromScriptableObject(this));

        return System.IO.Path.GetDirectoryName(scriptPath);
    }

    private Texture2D CopyViaRenderTexture(Texture2D src)
    {
        RenderTexture rt = RenderTexture.GetTemporary(
            src.width,
            src.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear
        );

        Graphics.Blit(src, rt);
        
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(
            src.width,
            src.height,
            TextureFormat.ARGB32,
            false,
            true
        );

        tex.ReadPixels(new Rect(0, 0, src.width, src.height),0,0);
        tex.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        return tex;
    }

    private void DrawPreview()
    {
        if (preview == null || previewMaterial == null) return;

        GUILayout.Label("Preview", EditorStyles.boldLabel);

        float maxWidth = EditorGUIUtility.currentViewWidth - 40;
        float aspect = (float)preview.width / preview.height;

        float width = Mathf.Min(maxWidth, preview.width);
        float height = width / aspect;

        Rect rect = GUILayoutUtility.GetRect(
            width,
            height,
            GUILayout.ExpandWidth(false)
        );

        EditorGUI.DrawPreviewTexture(
            rect,
            source,
            previewMaterial,
            ScaleMode.ScaleToFit
        );
    }
    
    
    private void PreparePreviewMaterial()
    {
        if(previewMaterial == null)
        {
            Shader shader = Shader.Find("Shader Graphs/PreviewShader");
            previewMaterial = new Material(shader);
            previewMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
        
        previewMaterial.SetTexture("_MainTex", source);
        previewMaterial.SetTexture("_MSDFTex", preview);

        previewMaterial.SetColor("_OutlineColor", outlineColor);
        previewMaterial.SetFloat("_MaxDistance", maxDistance);
        previewMaterial.SetFloat("_OutlineWidth", spread);
        previewMaterial.SetFloat("_Softness", softness);
    }
}

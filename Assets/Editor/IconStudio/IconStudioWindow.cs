using System;
using UnityEditor;
using UnityEngine;

public class IconStudioWindow : EditorWindow
{
    public int width = 512;
    public int height = 512;
    public string iconName;
    public bool showFrame = true;

    [MenuItem("Tools/Shware/Icon Studio")]
    public static void Open() => GetWindow<IconStudioWindow>("Icon Studio");

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(20);
        
        EditorGUI.BeginChangeCheck();

        width = EditorGUILayout.IntField("Width", Mathf.Max(1, width));
        height = EditorGUILayout.IntField("Height", Mathf.Max(1, height));
        iconName = EditorGUILayout.TextField("Icon Name",iconName);
        showFrame = EditorGUILayout.Toggle("Show Capture Frame", showFrame);

        if(GUILayout.Button("Capture"))
        {
            string assetPath = GeneratePath();
            string fullPath = System.IO.Path.GetFullPath(assetPath);

            CaptureIcon(width, height, fullPath);
            ApplyTextureImportSettings(assetPath);
        }
    }
    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }
    
    private void OnSceneGUI(SceneView sceneView)
    {
        if (!showFrame) return;
        if (sceneView == null || sceneView.camera == null) return;

        DrawCaptureFrame(sceneView.camera);
    }
    
    // 가이드 라인
    private void DrawCaptureFrame(Camera cam)
    {
        float aspect = (float)width / height;

        float planeDist = Mathf.Max(0.5f, cam.nearClipPlane + 0.5f); 

        float halfHeight;
        float halfWidth;

        if (cam.orthographic)
        {
            halfHeight = cam.orthographicSize;
            halfWidth = halfHeight * aspect;
        }
        else
        {
            halfHeight = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f) * planeDist;
            halfWidth = halfHeight * aspect;
        }

        Vector3 center = cam.transform.position + cam.transform.forward * planeDist;
        Vector3 right  = cam.transform.right * halfWidth;
        Vector3 up     = cam.transform.up * halfHeight;

        Vector3 tl = center + up - right;
        Vector3 tr = center + up + right;
        Vector3 bl = center - up - right;
        Vector3 br = center - up + right;

        Handles.color = Color.cyan;

        float thickness = 3f;

        Handles.DrawAAPolyLine(
            thickness,
            tl, tr, br, bl, tl
        );
    }

    private void CaptureIcon(int width, int height, string fullPath)
    {
        var camGO = new GameObject("__IconStudio_CaptureCam");
        camGO.hideFlags = HideFlags.HideAndDontSave;

        var captureCam = camGO.AddComponent<Camera>();

        captureCam.enabled = false;
        captureCam.clearFlags = CameraClearFlags.SolidColor;
        captureCam.backgroundColor = new Color(0, 0, 0, 0);

        if(!SetCaptureCamera(captureCam))
        {
            DestroyImmediate(camGO);
            Debug.LogWarning("[IconStudio] No active SceneView camera");
            return;
        }

        var renderTex = CreateRenderTexture(width, height);

        RenderToTexture(captureCam, renderTex);

        var tex2D = ReadBackToTexture2D(renderTex);

        SavePng(tex2D, fullPath);

        DestroyImmediate(renderTex);
        DestroyImmediate(tex2D);
        DestroyImmediate(camGO);

        Debug.Log($"[IconStudio] Saved PNG: {fullPath}");
    } 
    
    /// <summary>
    /// SceneView Camera 정보를 생성된 임시 캡쳐카메라에 적용하는 함수
    /// </summary>
    /// <param name="captureCam"></param>
    /// <returns></returns>
    private bool SetCaptureCamera(Camera captureCam)
    {

        if (!TryGetSceneViewCamera(out var sceneCam)) return false;

        captureCam.transform.position = sceneCam.transform.position;
        captureCam.transform.rotation = sceneCam.transform.rotation;

        captureCam.orthographic = sceneCam.orthographic;
        captureCam.fieldOfView = sceneCam.fieldOfView;
        captureCam.orthographicSize = sceneCam.orthographicSize;
        
        // 클립플레인 예외 처리 
        captureCam.nearClipPlane = Mathf.Max(0.01f, sceneCam.nearClipPlane);
        captureCam.farClipPlane = Mathf.Max(
            captureCam.nearClipPlane + 0.01f,
            sceneCam.farClipPlane
            );
        return true;
    }

    /// <summary>
    /// SceneCamera를 가져오는 함수
    /// </summary>
    /// <param name="sceneCam"></param>
    /// <returns></returns>
    private bool TryGetSceneViewCamera(out Camera sceneCam)
    {
        var sv = SceneView.lastActiveSceneView;
        if(sv == null || sv.camera == null)
        {
            sceneCam = null; 
            return false;
        }

        sceneCam = sv.camera;
        return true;
    }

    /// <summary>
    /// gpu상에 rendertexture를 생성하는 함수
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <returns></returns>
    private RenderTexture CreateRenderTexture(int width, int height)
    {
        var rt = new RenderTexture(
            width,
            height,
            24,
            RenderTextureFormat.ARGB32
            );
        rt.Create(); // 생성한 렌더 텍스쳐를 메모리에 대응시키기
        return rt;
    }

    /// <summary>
    /// CaptureCamera에 비추는 화면을 RenderTexture에 렌더링하는 함수
    /// </summary>
    /// <param name="cam"></param>
    /// <param name="rt"></param>
    private void RenderToTexture(Camera cam, RenderTexture rt)
    {
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = null;
    }

    /// <summary>
    /// gpu상의 RenderTexture를 cpu상의 Texture2D로 변환하는 함수
    /// </summary>
    /// <param name="rt"></param>
    /// <returns></returns>
    private Texture2D ReadBackToTexture2D(RenderTexture rt)
    {
        var prev = RenderTexture.active; 
        RenderTexture.active = rt; //활성 렌더 텍스쳐로 설정해 CPU가 read/write가 가능하게 함

        var tex = new Texture2D(
            rt.width,
            rt.height,
            TextureFormat.ARGB32,
            false);

        tex.ReadPixels( // CPU read/write
            new Rect(0, 0, rt.width, rt.height), 
            0,
            0);

        tex.Apply();

        RenderTexture.active = prev; // 기존 활성 텍스쳐로 다시 돌아가기

        return tex;
    }

    /// <summary>
    /// 변환된 Texture2D를 PNG로 인코딩해 저장하는 함수
    /// </summary>
    /// <param name="tex2D"></param>
    /// <param name="savePath"></param>
    private void SavePng(Texture2D tex2D, string savePath)
    {
        byte[] png = tex2D.EncodeToPNG();
        System.IO.File.WriteAllBytes(savePath, png);
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// icon path를 생성해주는 함수
    /// </summary>
    /// <returns></returns>
    private string GeneratePath()
    {
        string baseFolder = GetThisScriptFolderPath();

        string generatedRoot = $"{baseFolder}/Generated";
        
        if (!AssetDatabase.IsValidFolder(generatedRoot))
        {
            AssetDatabase.CreateFolder(baseFolder, "Generated");
        }

        string name = string.IsNullOrEmpty(iconName) ? "New" : iconName;

        string path = $"{generatedRoot}/{name}_Icon.png";

        return path;
    }
    
    /// <summary>
    /// 이 스크립트가 포함된 폴더경로를 알아내는 함수
    /// </summary>
    /// <returns></returns>
    private string GetThisScriptFolderPath()
    {
        string scriptPath = AssetDatabase.GetAssetPath(
            MonoScript.FromScriptableObject(this));

        return System.IO.Path.GetDirectoryName(scriptPath);
    }

    /// <summary>
    /// 생성된 PNG의 텍스쳐 세팅을 바꿔주는 함수
    /// </summary>
    /// <param name="assetPath"></param>
    private void ApplyTextureImportSettings(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[IconStudio] Importer not found for path: {assetPath}");
            return;
        }

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        settings.textureType = TextureImporterType.Sprite;
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spritePixelsPerUnit = 100f;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.alphaIsTransparency = true;
        settings.mipmapEnabled = false;
        settings.npotScale = TextureImporterNPOTScale.None;
        settings.sRGBTexture = true;
        settings.filterMode = FilterMode.Bilinear;
        settings.wrapMode = TextureWrapMode.Clamp;

        importer.SetTextureSettings(settings);
        
        importer.SaveAndReimport();
        Debug.Log($"[IconStudio] Import settings applied: {assetPath}");
    }

}

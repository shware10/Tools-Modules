using System;
using UnityEditor;
using UnityEngine;

public class IconStudioWindow : EditorWindow
{
    public int width = 512;
    public int height = 512;
    public string iconName;
    public bool showFrame = true;

    [MenuItem("Tools/Shware/IconStudio")]
    public static void Open() => GetWindow<IconStudioWindow>("IconStudio");

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

		private void OnGUI()
		{
		    EditorGUILayout.Space(20);
		    
		    EditorGUI.BeginChangeCheck();
		
		    width = EditorGUILayout.IntField("Width", Mathf.Max(1, width));       // 넓이 input
		    height = EditorGUILayout.IntField("Height", Mathf.Max(1, height));    // 높이 input
		    iconName = EditorGUILayout.TextField("Icon Name",iconName);           // 저장할 아이콘 이름 input
		    showFrame = EditorGUILayout.Toggle("Show Capture Frame", showFrame);  // 가이드라인을 보여줄 지 toggle
		    
		    if(GUILayout.Button("Capture"))                                // 캡처 버튼을 누르면                      
		    {
		        string assetPath = GeneratePath();                         // Asset 폴더 기준 경로
		        string fullPath = System.IO.Path.GetFullPath(assetPath);   // SavePng(Texture2D tex2D, string fullPath) 함수에 전달할 디스크 경로(fullPath)를 생성
		
		        CaptureIcon(width, height, fullPath);                      // Icon을 캡쳐해 디스크 경로에 저장합니다.
		        ApplyTextureImportSettings(assetPath);                     // AssetDataBase에 저장된 png 파일에 접근해 텍스쳐 임포트 셋팅을 변경합니다.
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
	
	    float planeDist = Mathf.Max(0.5f, cam.nearClipPlane + 0.5f); // nearClipPlane에서 적당한 거리에 띄워놓기 미세하게 확대된 가이드 이지만 짤림 방지 가능
	
	    float halfHeight;
	    float halfWidth;
	
	    if (cam.orthographic) // orthocamera의 경우 처리
	    {
	        halfHeight = cam.orthographicSize;
	        halfWidth = halfHeight * aspect;
	    }
	    else                  // perpective의 경우 처리
	    {
	        halfHeight = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f) * planeDist;
	        halfWidth = halfHeight * aspect;
	    }
	
	    Vector3 center = cam.transform.position + cam.transform.forward * planeDist;
	    Vector3 right  = cam.transform.right * halfWidth;
	    Vector3 up     = cam.transform.up * halfHeight;
		
			// 정점 위치
	    Vector3 tl = center + up - right;
	    Vector3 tr = center + up + right;
	    Vector3 bl = center - up - right;
	    Vector3 br = center - up + right;
	
	    Handles.color = Color.cyan;
	    float thickness = 3f;
			
			// 정점 연결한 폴리라인 그리기
	    Handles.DrawAAPolyLine(
	        thickness,
	        tl, tr, br, bl, tl
	    );
	}

    private void CaptureIcon(int width, int height, string fullPath)
    {
				var camGO = new GameObject("__IconStudio_CaptureCam");             // 오브젝트 생성
				camGO.hideFlags = HideFlags.HideAndDontSave;                       // 씬에 저장하지도 하이어라키에도 보이지 않게 
				
				var captureCam = camGO.AddComponent<Camera>();                     // 카메라 컴포넌트 추가
				
				captureCam.enabled = false;                                        // 렌더 루프에서 제외하고 수동으로 부르기 위해 꺼두기
				captureCam.clearFlags = CameraClearFlags.SolidColor;               // 화면을 단색으로 초기화
				captureCam.backgroundColor = new Color(0, 0, 0, 0);                // alpha 값을 0으로 투명하게

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
	
	    if (!TryGetSceneViewCamera(out var sceneCam)) return false;     // SceneView 카메라를 가져오지 못했으면 false return
			
			// SceneView Camera의 상태를 임시 카메라에 복사
	    captureCam.transform.position = sceneCam.transform.position;
	    captureCam.transform.rotation = sceneCam.transform.rotation;
	
	    captureCam.orthographic = sceneCam.orthographic;
	    captureCam.fieldOfView  = sceneCam.fieldOfView;
	    captureCam.orthographicSize = sceneCam.orthographicSize;
			
	    // nearClipPlane이 너무 작으면 Render()에 아무것도 안찍힐 수도 있기에 최소값은 0.01f로 설정
	    captureCam.nearClipPlane = Mathf.Max(0.01f, sceneCam.nearClipPlane);  
	    // farClipPlane이 nearClipPlane보다 가까우면 렌더 불능
	    captureCam.farClipPlane  = Mathf.Max(captureCam.nearClipPlane + 0.01f, sceneCam.farClipPlane); 
	        
	    return true;
	}

    /// <summary>
    /// SceneCamera를 가져오는 함수
    /// </summary>
    /// <param name="sceneCam"></param>
    /// <returns></returns>
	private bool TryGetSceneViewCamera(out Camera sceneCam) 
	{
	    var sv = SceneView.lastActiveSceneView;             // 마지막으로 사용자가 클릭/조작한 SceneView를 가져오기
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
	    var rt = new RenderTexture(       // RenderTexture를 생성합니다.
	        width,
	        height,
	        24,                           // SceneView Camera가 사용하는 표준 24bit depth을 따라줍니다.
	        RenderTextureFormat.ARGB32    // 알파 채널 포함 32bit 컬러 채널
	        );
	    rt.Create();       // RenderTexture에 대응되는 GPU 메모리를 할당해 줍니다.
	    return rt;
	}

    /// <summary>
    /// CaptureCamera에 비추는 화면을 RenderTexture에 렌더링하는 함수
    /// </summary>
    /// <param name="cam"></param>
    /// <param name="rt"></param>
	private void RenderToTexture(Camera cam, RenderTexture rt)
	{
	    cam.targetTexture = rt;       // 임시 카메라의 타겟 텍스쳐로 설정해줍니다.
	    cam.Render();                 // RenderTexture에 현재 카메라가 보고 있는 화면을 그려줍니다.
	    cam.targetTexture = null;     // 렌더완료 후 RenderTexture와의 연결을 끊어줍니다.
	}
		
    /// <summary>
    /// gpu상의 RenderTexture를 cpu상의 Texture2D로 변환하는 함수
    /// </summary>
    /// <param name="rt"></param>
    /// <returns></returns>
	private Texture2D ReadBackToTexture2D(RenderTexture rt)
	{
	    var prev = RenderTexture.active;  // 현재 활성 렌더 텍스쳐는 캐싱
	    RenderTexture.active = rt;        // 화면을 그린 렌더 텍스쳐를 활성 렌더 텍스쳐로 설정
	
	    var tex = new Texture2D(          // Texture2D 생성
	        rt.width,
	        rt.height,
	        TextureFormat.ARGB32,
	        false);
	
	    tex.ReadPixels(                                  // 활성 렌더 텍스쳐를 읽어드림
	        new Rect(0, 0, rt.width, rt.height), 0, 0);  // 텍스쳐가 0,0에서 부터 활성 텍스쳐를 읽습니다.
	
	    tex.Apply();                      // 렌더 텍스쳐를 2D텍스쳐에 적용합니다.
	
	    RenderTexture.active = prev;      // 캐싱해둔 원래 활성 렌더 텍스쳐 다시 활성 텍스쳐로 설정해 줍니다.
	
	    return tex;
	}
		
    /// <summary>
    /// 변환된 Texture2D를 PNG로 인코딩해 저장하는 함수
    /// </summary>
    /// <param name="tex2D"></param>
    /// <param name="savePath"></param>
	private void SavePng(Texture2D tex2D, string savePath)
	{
	    byte[] png = tex2D.EncodeToPNG();             // 텍스쳐를 PNG로 인코딩
	    System.IO.File.WriteAllBytes(savePath, png);  // 원하는 저장 경로에 작성
	    AssetDatabase.Refresh();                      // Assets/ 폴더 전체(또는 변경분) 재스캔
	}

    /// <summary>
    /// icon path를 생성해주는 함수
    /// </summary>
    /// <returns></returns>
	private string GeneratePath()  
	{
	    string baseFolder = GetThisScriptFolderPath();            // 이 스크립트 파일이 존재하는 Assets/ 기준 상대경로를 가져옵니다.
	
	    string generatedRoot = $"{baseFolder}/Generated";   
	    
	    if (!AssetDatabase.IsValidFolder(generatedRoot)) 
	    {
	        AssetDatabase.CreateFolder(baseFolder, "Generated");  // generated 폴더가 없으면 생성해 줍니다.
	    }
	
	    string name = string.IsNullOrEmpty(iconName) ? "New" : iconName;
	
	    string path = $"{generatedRoot}/{name}_Icon.png";
	
	    return path;    // 최종 아이콘 경로 리턴
	}
    
    /// <summary>
    /// 이 스크립트가 포함된 폴더경로를 알아내는 함수
    /// </summary>
    /// <returns></returns>
	private string GetThisScriptFolderPath()             
	{
	    string scriptPath = AssetDatabase.GetAssetPath(     // 이 스크립트의 Asset/ 기준 상대경로를 가져오기
	        MonoScript.FromScriptableObject(this));
	
	    return System.IO.Path.GetDirectoryName(scriptPath); // 디렉토리를 리턴
	}

    /// <summary>
    /// 생성된 PNG의 텍스쳐 세팅을 바꿔주는 함수
    /// </summary>
    /// <param name="assetPath"></param>
	private void ApplyTextureImportSettings(string assetPath)
	{
	    var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;           // 생성한 PNG파일 경로를 통해 importer에 접근합니다.
	    if (importer == null)
	    {
	        Debug.LogWarning($"[IconStudio] Importer not found for path: {assetPath}"); // 에셋이 없을 때 예외처리
	        return;
	    }
	
	    var settings = new TextureImporterSettings();  // 유니티는 셋팅값을 바꿀 수 있도록 TextureImporterSettings라는 구조체를 제공합니다.
	    
	    importer.ReadTextureSettings(settings);        // importer에서 settings 디폴트 값을 읽습니다. 
	
			// 변경하고자 하는 설정 값들을 변경해 줍니다.
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
	
	    importer.SetTextureSettings(settings);         // 변경 값을 임포터에 반영해 .meta파일을 수정합니다.
	    
	    importer.SaveAndReimport();                    // 임포터 설정을 저장하고 reimport 해 실제 Asset에 적용합니다.
	    Debug.Log($"[IconStudio] Import settings applied: {assetPath}");
	}
}

using UnityEditor;
using UnityEngine;

public static class AdvancedSliceImporterUtil
{
    private const string Prefix = "ADVANCED_SLICE:";
    
    /// <summary>
    /// SliceData 저장 함수
    /// </summary>
    /// <param name="sprite"></param>
    /// <param name="data"></param>
    /// <param name="reimport"></param>
    public static void Save(
    Sprite sprite,
    AdvancedSliceData data,
    bool reimport = true)
    {
        TextureImporter importer = GetImporter(sprite);
        
        if(importer == null) return;
        
        string json = JsonUtility.ToJson(data);
        
        importer.userData = Prefix + json;
        
        EditorUtility.SetDirty(importer); 
        
        if(reimport) importer.SaveAndReimport();
    }
    
    /// <summary>
    /// SliceData 로드 함수
    /// </summary>
    /// <param name="sprite"></param>
    /// <returns></returns>
    public static AdvancedSliceData LoadOrCreateDefault(Sprite sprite)
    {
        if (TryLoad(sprite, out AdvancedSliceData data))
            return data;

        return GenerateDefault(sprite);
    }
    
    // 로드를 시도
    public static bool TryLoad(
    Sprite sprite,
    out AdvancedSliceData data)
    {
        data = default;
        
        TextureImporter importer = GetImporter(sprite);
        
        if(importer == null) return false;
        
        string userData = importer.userData;
        
        if(string.IsNullOrEmpty(userData)) return false;
        
        if(!userData.StartsWith(Prefix)) return false;
        
        string json = userData.Substring(Prefix.Length);
        
        data = JsonUtility.FromJson<AdvancedSliceData>(json);
        return true;
    }
    
    // 로드 실패시 디폴트 슬라이스 데이터 생성
    public static AdvancedSliceData GenerateDefault(Sprite sprite)
    {
        if(sprite == null) return default;
        
        Rect rect =  sprite.rect;
        
        return AdvancedSliceData.GenerateDefault(rect.width, rect.height);
    }
    
    // 스프라이트 위치 경로로 접근해 텍스쳐임포터를 가져오는 함수
    private static TextureImporter GetImporter(Sprite sprite)
    {
        if(sprite == null) return null;
        
        string path = AssetDatabase.GetAssetPath(sprite);
        
        if(string.IsNullOrEmpty(path)) return null;
        
        // 스프라이트의 에셋경로에 접근해 에셋임포터를 가져와서 텍스쳐임포터로 다운캐스팅
        return AssetImporter.GetAtPath(path) as TextureImporter;
    }
}

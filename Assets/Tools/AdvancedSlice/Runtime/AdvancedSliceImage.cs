using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Unity 기본 9-Slice를 확장한 5x5 Slice Image.
/// 
/// 구조
/// Border | Stretch | Center | Stretch | Border
/// 중앙 영역(Center)과 Border의 크기가 유지되고
/// Stretch 영역만 남은 공간을 차지합니다.
/// 
/// Slice 정보는 Sprite의
/// TextureImporter.userData에 저장됩니다.
/// </summary>
[AddComponentMenu("UI/Advanced Slice Image")]
public class AdvancedSliceImage : Image
{
    public enum AdvancedSliceMode
    {
        FiveByFive,
        FiveByThree,
        ThreeByFive
    }

    // Sprite에 저장된 Slice 정보를 캐시
    [SerializeField] private AdvancedSliceData _sliceData;
    [SerializeField] private AdvancedSliceMode _sliceMode = AdvancedSliceMode.FiveByFive;
    
    private Sprite activeSprite =>
        overrideSprite != null ?
        overrideSprite : sprite;
            
    
    #if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SyncSliceData();
    }
    
    private void SyncSliceData()
    {
        if(activeSprite == null) return;
        
        if(AdvancedSliceImporterUtil.TryLoad(activeSprite, out var data))
        {
            _sliceData = data;
            
            // EditorWindow에서 Save를 누른 후에도 갱신될 수 있도록
            SetVerticesDirty();
            SetMaterialDirty();
        }
        else
        {
            _sliceData = AdvancedSliceData.GenerateDefault
            (activeSprite.rect.width, activeSprite.rect.height);
        }
    }
    #endif
    
    /// <summary>
    /// UGUI가 다시 그려질 때 호출되는 함수
    /// </summary>
    /// <param name="vh"></param>
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        
        if(activeSprite == null) return;
        
        GenerateAdvancedSliceMesh(vh);
    }
    
    /// <summary>
    /// 5x5 Grid 생성.
    ///
    /// 36개의 Vertex를 생성한 후
    /// 25개의 Quad가 이를 공유합니다.
    /// </summary>
    private void GenerateAdvancedSliceMesh(VertexHelper vh)
    {
        // Canvas Scale/Pixel Perfect 보정을 반영한 실제 Rect 영역 가져오기
        Rect rect = GetPixelAdjustedRect();
        
        // 텍스쳐 안에서 스프라이트의 위치를 고려한 Rect 영역
        Rect textureRect = activeSprite.textureRect;
        
        // RectTransform의 크기를 가져오기 textureRect를 통해 접근해 Atlas 대응
        float sourceWidth   = textureRect.width;
        float sourceHeight  = textureRect.height;
        
        // 원본 스프라이트 내부 경계 지점 생성
        float[] sourceX = BuildSourceX(sourceWidth);
        float[] sourceY = BuildSourceY(sourceHeight);
        
        
        // 스프라이트의 좌료를 실제 RectTransform 크기에 맞는 좌표로 변환
        float[] pointsX = BuildAxisPoints(
        rect.xMin,
        rect.xMax,
        sourceX
        );
        
        float[] pointsY = BuildAxisPoints(
        rect.yMin,
        rect.yMax,
        sourceY
        );
        
        // vertex, uv 생성
        Vector2[,] vertices =
            BuildVertex(
                pointsX,
                pointsY);
        
        Vector2[,] uvs = BuildUV(
        textureRect,
        activeSprite.texture,
        sourceX,
        sourceY
        );
        
        //메쉬 생성        
        GenerateMesh(vh, vertices, uvs);
    }
    
    private float[] BuildSourceX(float sourceWidth)
    {
        switch(_sliceMode)
        {
            case AdvancedSliceMode.ThreeByFive :
                return new[]
                {
                    0f,
                    _sliceData.Left,
                    _sliceData.Right,
                    sourceWidth
                };
            default:
                return new[]
                {
                    0f,
                    _sliceData.Left,
                    _sliceData.LeftInner,
                    _sliceData.RightInner,
                    _sliceData.Right,
                    sourceWidth
                };
        }
    }
    
    private float[] BuildSourceY(float sourceHeight)
    {
        switch(_sliceMode)
        {
            case AdvancedSliceMode.FiveByThree :
                return new[]
                {
                    0f,
                    _sliceData.Bottom,
                    _sliceData.Top,
                    sourceHeight
                };
            default:
                return new[]
                {
                    0f,
                    _sliceData.Bottom,
                    _sliceData.BottomInner,
                    _sliceData.TopInner,
                    _sliceData.Top,
                    sourceHeight
                };
        }
    }
    
    private static float[] BuildAxisPoints(
    float start,
    float end,
    float[] source
    )
    {
        if(source.Length == 6)
        {
            return BuildFiveAxisPoints(start, end, source);
        }
        
        if(source.Length == 4)
        {
            return BuildThreeAxisPoints(start, end, source);
        }        
        
        return null;
    }
    
    /// <summary>
    /// Sprite 축의 내부 5개의 좌표들을
    /// 실제 RectTransform 좌표들로 변환합니다.
    /// </summary>
    private static float[] BuildFiveAxisPoints(
    float start,
    float end,
    float[] source
    )
    {
        // 현재 RectTrasnform의 총 길이
        float totalLength = end - start;
        
        // BorderA
        float fixedA    = source[1] - source[0];
        // StretchA
        float stretchA  = source[2] - source[1];
        // Center
        float center    = source[3] - source[2];
        // StretchB
        float stretchB  = source[4] - source[3];
        // BorderB
        float fixedB    = source[5] - source[4];
        
        // 고정 영역 총합
        float fixedTotal    = fixedA + center + fixedB;
        // stretch 영역 총합
        float stretchTotal  = stretchA + stretchB;
        
        float fixedScale = 1f;
        
        // RectTransform이 고정 영역의 합보다 작은 경우 고정 영역의 비율을 유지하며 축소
        if(fixedTotal > totalLength && fixedTotal > 0f)
        {
            fixedScale = totalLength / fixedTotal;
        }
        
        fixedA *= fixedScale;
        fixedB *= fixedScale;
        center *= fixedScale;
        
        float scaledFixedTotal = fixedA + center + fixedB;
        //부동 소수점 오차 방지 예외처리한 고정 범위를 제외한 범위
        float remaining = Mathf.Max(0f, totalLength - scaledFixedTotal);
        
        if(stretchTotal > 0f)
        {
            stretchA = remaining * (stretchA / stretchTotal);
            stretchB = remaining * (stretchB / stretchTotal); 
        }
        else
        {
            // Stretch 영역을 설정하지 않은 경우
            stretchA = remaining * 0.5f;
            stretchB = remaining * 0.5f;
        }
        
        float[] points = new float[6];
        
        //최종 좌표 계산
        points[0] = start;
        points[1] = points[0] + fixedA;
        points[2] = points[1] + stretchA;
        points[3] = points[2] + center;
        points[4] = points[3] + stretchB;
        points[5] = end;
        
        return points;
    }
    
    private static float[] BuildThreeAxisPoints(
    float start,
    float end,
    float[] source
    )
    {
        float totalLength = end - start;
        
        float fixedA  = source[1] - source[0];
        float stretch = source[2] - source[1];
        float fixedB  = source[3] - source[2];
        
        float fixedTotal = fixedA + fixedB;
        
        float fixedScale = 1f;
        
        if(fixedTotal > totalLength && fixedTotal > 0f)
        {
            fixedScale = totalLength / fixedTotal;
        }
        
        fixedA *= fixedScale;
        fixedB *= fixedScale;
        
        float ScaledFixedTotal = fixedA + fixedB;
        float remaining = Mathf.Max(0f, totalLength - ScaledFixedTotal);
        
        float[] points = new float[4];
        
        points[0] = start;
        points[1] = points[0] + fixedA;
        points[2] = points[1] + remaining;
        points[3] = end;
        
        return points;
    }
    
    
    
    /// <summary>
    /// x,y좌표로 생성되는 교차점을 Vertex로 변환하는 함수
    /// </summary>
    /// <param name="pointsX"></param>
    /// <param name="pointsY"></param>
    /// <returns></returns>
    private static Vector2[,] BuildVertex(
    float[] pointsX,
    float[] pointsY
    )
    {
        Vector2[,] grid = new Vector2[pointsX.Length,pointsY.Length];
        
        for(int y = 0; y < pointsY.Length; ++y)
        {
            for(int x = 0; x < pointsX.Length; ++x)
            {
                grid[x, y] = new Vector2(pointsX[x], pointsY[y]);
            }
        }
        
        return grid;
    }
    
    
    /// <summary>
    /// x,y 좌표로 생성되는 교차점을 UV에 맵핑해 변환하는 함수
    /// </summary>
    /// <param name="textureRect"></param>
    /// <param name="texture"></param>
    /// <param name="sourceX"></param>
    /// <param name="sourceY"></param>
    /// <returns></returns>
    private static Vector2[,] BuildUV(
    Rect textureRect,
    Texture texture,
    float[] sourceX,
    float[] sourceY
    )
    {
        Vector2[,] uvs = new Vector2[sourceX.Length, sourceY.Length];
        
        for(int x = 0; x < sourceX.Length; ++x)
        {
            for(int y = 0; y < sourceY.Length; ++y)
            {
                // 교차점들을 uv(0-1)에 맵핑
                uvs[x, y] = new Vector2(
                (textureRect.x + sourceX[x]) / texture.width, 
                (textureRect.y + sourceY[y]) / texture.height  
                );
            }
        }
        
        return uvs;
    }
    
    
    /// <summary>
    /// vertex와 uv정보를 바탕으로 UGUI용 Mesh를 생성
    /// </summary>
    /// <param name="vh"></param>
    /// <param name="vertices"></param>
    /// <param name="uvs"></param>
    private void GenerateMesh(
    VertexHelper vh,
    Vector2[,] vertices,
    Vector2[,] uvs
    )
    {
        Color32 color32 = this.color;
        
        int xCount = vertices.GetLength(0);
        int yCount = vertices.GetLength(1);
        
        // vertex생성 UI는 vertexhelper를 써서 메쉬를 생성합
        for(int y = 0; y < yCount; ++y)
        {
            for(int x = 0; x < xCount; ++x)
            {
                vh.AddVert(vertices[x, y], color32, uvs[x, y]);
            }
        }
        
        for(int y = 0; y < yCount - 1; ++y)
        {
            for(int x = 0; x < xCount - 1; ++x)
            {
                // 쿼드 별 인덱스 계산
                int bottomLeft  = y * xCount + x;
                int topLeft     = bottomLeft + xCount;
                int topRight    = topLeft + 1;
                int bottomRight = bottomLeft + 1;
                
                //삼각형을 생성
                vh.AddTriangle(bottomLeft, topLeft, topRight);
                vh.AddTriangle(topRight, bottomLeft, bottomRight);
            }
        }
    }
    
}

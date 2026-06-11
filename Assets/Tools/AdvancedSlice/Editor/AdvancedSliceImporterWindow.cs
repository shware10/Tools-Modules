using UnityEditor;
using UnityEngine;

public class AdvancedSliceImporterWindow : EditorWindow
{
    private enum LineType
    {
        None,
        
        Left,
        LeftInner,
        RightInner,
        Right,
        
        Bottom,
        BottomInner,
        TopInner,
        Top
    }
    private struct SliceLine
    {
        public LineType Type;
        public bool IsVertical;
        
        public SliceLine(LineType type, bool isVertical)
        {
            Type = type;
            IsVertical = isVertical;
        }
    }
    
    private readonly SliceLine[] _lines =
    {
        new(LineType.Left, true),
        new(LineType.LeftInner, true),
        new(LineType.RightInner, true),
        new(LineType.Right, true),
        
        new(LineType.Bottom, false),
        new(LineType.BottomInner, false),
        new(LineType.TopInner, false),
        new(LineType.Top, false),
    };
    
    private Sprite _sprite;
    private AdvancedSliceData _sliceData;
    private Texture2D _spriteTexture;
    
    private static Texture2D _checkerTexture;
    private static Texture2D CheckerTexture
    {
        get
        {
            if (_checkerTexture == null)
                _checkerTexture = CreateCheckerTexture();

            return _checkerTexture;
        }
    }

    private Rect _backgroundRect;
    private Rect _spriteRect;
    
    private SliceLine hoveredLine;
    private SliceLine draggingLine;
    
    private bool _isDirty;
    
    private float _zoom = 1f;

    private const float MinZoom = 0.25f;
    private const float MaxZoom = 16f;
    
    private Vector2 _panOffset;
    private bool _isPanning;
    
    [MenuItem("Tools/Shware/AdvancedSlice")]
    public static void Open() => GetWindow<AdvancedSliceImporterWindow>("AdvancedSlice");
    
    private void OnGUI()
    {
        DrawToolbar();
        
        DrawPreview();
        
        Handles.BeginGUI();
        foreach(var line in _lines) DrawLine(line);
        Handles.EndGUI();
        
        HandleZoom();
        
        HandlePan();
        
        HandleLine();
        
        DrawPixelInfo();
    }

    private void DrawPixelInfo()
    {
        if(_sprite == null) return;
        
        SliceLine targetLine = 
        draggingLine.Type != LineType.None ?
        draggingLine : hoveredLine;
        
        if(targetLine.Type == LineType.None) return;
        
        float pixel = GetPixel(targetLine.Type);
        
        string text = 
        targetLine.IsVertical ?
        $"{targetLine.Type} X : {pixel:0} / {_sprite.rect.width:0}px":
        $"{targetLine.Type} Y : {pixel:0} / {_sprite.rect.height:0}px";
        
        EditorGUI.DrawRect(
            new Rect(
                _backgroundRect.x + 5,
                _backgroundRect.y + 5,
                230,
                24),
            new Color(0,0,0,0.6f));

        GUI.Label(
            new Rect(
                _backgroundRect.x + 10,
                _backgroundRect.y + 7,
                220,
                20),
            text);
    }

    #region Toolbar Method
    
    /// <summary>
    /// 버튼 등의 툴바를 그리는 함수
    /// </summary>
    private void DrawToolbar()
    {
        EditorGUI.BeginChangeCheck();
        
        _sprite = (Sprite)EditorGUILayout.ObjectField(
            "Sprite",
            _sprite,
            typeof(Sprite),
            false);
        // sprite가 바뀌면 
        if (EditorGUI.EndChangeCheck())
        {
            OnSpriteChanged();
        }
        
        EditorGUILayout.BeginHorizontal();
        
        // sprite가 존재하면 save 버튼 활성화
        GUI.enabled = _sprite != null && _isDirty;
        
        // save 버튼을 누르면 조정한 라인의 sliceData를 textureimporter.userdata에 저장
        if (GUILayout.Button("Save"))
        {
            AdvancedSliceImporterUtil.Save(
                _sprite,
                _sliceData);
                
            _isDirty = false;
        }

        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }
    
    /// <summary>
    /// 에디터 상의 sprite를 교체하면 새로운 sliceData와 texture를 가져오는 함수
    /// </summary>
    private void OnSpriteChanged()
    {
        if(_sprite == null)
        {
            _spriteTexture = null;
            return;
        }
        // sliceData 캐싱
        _sliceData = AdvancedSliceImporterUtil.LoadOrCreateDefault(_sprite);
        // sprite texture 캐싱
        _spriteTexture = AssetPreview.GetAssetPreview(_sprite);
    }
    
    #endregion
    
    #region Previw Method
    
    /// <summary>
    /// 라인을 조정할 수 있는 AdvancedSlice preview 그리기
    /// </summary>
    private void DrawPreview()
    {
        if (_sprite == null)
        {
            EditorGUILayout.HelpBox(
                "Select Sprite",
                MessageType.Info);

            return;
        }
        
        // 현재 OnGUI에서 남아있는 공간을 계산해서 Rect로 변환
        _backgroundRect =
            GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
        
        // 배경 Rect의 비율에 맞게 조정한 SpriteRect
        _spriteRect = CalculateSpriteRect();
        
        // 체커보드를 Sprite Rect에 맞게 그리고
        DrawCheckerBoard();
        // 그 위에 실제 sprite texture 그리기
        DrawSprite();
    }
    
    /// <summary>
    /// 실제 스프라이트를 그릴 Rect를 생성하는 함수
    /// </summary>
    /// <returns></returns>
    private Rect CalculateSpriteRect()
    {
        float spriteWidth = _sprite.rect.width;
        float spriteHeight = _sprite.rect.height;
        
        float spriteAspect = spriteWidth / spriteHeight;
        float backgroundAspect = _backgroundRect.width / _backgroundRect.height;
        
        Rect rect = _backgroundRect;
        
        // 스프라이트 비율을 유지하면서 배경의 중앙에 배치하기
        // 스프라이트의 가로비율이 배경의 가로비율보다 큰 경우 
        if(spriteAspect > backgroundAspect)
        {   
            // 가로로 납작한 스프라이트에 맞게 Rect의 세로 길이를 조정합니다.
            float height = rect.width / spriteAspect;
            
            // 바뀐 세로 비율의 중앙에 스프라이트를 맞추기 위해 위/아래로 남는 배경 공간을 반으로 나눠 offset.
            float y = rect.y + (rect.height - height) * 0.5f;
            
            // 새로운 sprite용 Rect를 생성합니다.
            rect = new Rect(rect.x, y, rect.width, height);
        }
        else // backGround의 가로비율이 스프라이트의 가로비율이 보다 큰 경우
        {
            // 세로로 긴 스프라이트에 맞게 프리뷰의 가로 길이를 조정합니다.
            float width = rect.height * spriteAspect;
            
            // 바뀐 가로 비율의 중앙에 스프라이트를 맞추기 위해 좌/우로 남는 배경 공간을 반으로 나눠 offset.
            float x = rect.x + (rect.width - width) * 0.5f;
            
            // 새로운 sprite용 Rect를 생성합니다.
            rect = new Rect(x, rect.y, width, rect.height);
        }
        
        float zoomWidth = rect.width * _zoom;
        float zoomHeight = rect.height * _zoom;

        rect = new Rect(
            rect.center.x - zoomWidth * 0.5f,
            _backgroundRect.yMin,
            zoomWidth,
            zoomHeight);
        
        rect.position += _panOffset;
        
        return rect;
    }
    
    /// <summary>
    /// 실제 sprite를 그리는 Rect에 채워 그리는 함수
    /// </summary>
    private void DrawSprite()
    {
        // spriteTexture가 없으면 slice를 조정할 sprite의 텍스쳐 캐싱
        if(_spriteTexture == null)
        {
            // sprite의 텍스쳐를 가져옵니다.
            _spriteTexture = AssetPreview.GetAssetPreview(_sprite);
            Repaint();
            
            return;
        }
        
        // Rect에 맞춰 sprite의 텍스쳐 그리기
        GUI.DrawTexture(_spriteRect, _spriteTexture, ScaleMode.StretchToFill);
    }
    
    #region CheckerBoard Method
    /// <summary>
    /// sprite Rect의 배경에 그려질 체커보드 텍스쳐를 생성하는 함수
    /// </summary>
    /// <returns></returns>
    private static Texture2D CreateCheckerTexture()
    {
        Texture2D tex = new Texture2D(2, 2);

        Color dark = new Color(0.35f, 0.35f, 0.35f);
        Color light = new Color(0.45f, 0.45f, 0.45f);

        tex.SetPixels(new[]
        {
            dark, light,
            light, dark
        });

        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.hideFlags = HideFlags.HideAndDontSave;

        tex.Apply();

        return tex;
    }
    
    /// <summary>
    /// 체커보드를 그리는 함수
    /// </summary>
    private void DrawCheckerBoard()
    {
        GUI.DrawTextureWithTexCoords(
            _spriteRect,
            CheckerTexture,
            new Rect(
                0,
                0,
                _spriteRect.width / 32f,
                _spriteRect.height / 32f));
    }
    #endregion
    #endregion
    
    #region Line Method
    
    // 라인에 대한 마우스 이벤트를 다루는 함수
    private void HandleLine()
    {
        if(_sprite == null) return;
        
        Event eve = Event.current;
        
        hoveredLine = GetHoveredLine(eve.mousePosition);
        
        // 마우스 클릭 이벤트 발생 시 호버중인 라인을 드래그 중인 라인으로 전환
        if( eve.type == EventType.MouseDown 
            && eve.button == 0
            && hoveredLine.Type != LineType.None)
        {
            draggingLine = hoveredLine;
            eve.Use();
        }
        
        // 마우스 드래그 이벤트 발생 시 선을 마우스 방향으로 움직이기
        if( eve.type == EventType.MouseDrag && draggingLine.Type != LineType.None)
        {
            MoveLine(draggingLine, eve.mousePosition);
            
            _isDirty = true;

            Repaint();
            eve.Use();
        }
        
        // 마우스 클릭이 종료되면 드래그 종료
        if(eve.type == EventType.MouseUp && draggingLine.Type != LineType.None)
        {
            draggingLine = new SliceLine(LineType.None, false);
            
            Repaint();
            eve.Use();
        }
    }
    
    // mouse의 위치로 선을 옮기는 함수
    private void MoveLine(SliceLine line, Vector2 mousePosition)
    {
        float pixel;
        
        if(line.IsVertical)
        {
            // 수직선을 움직이면 x좌표
            pixel = RectXToPixelX(mousePosition.x);
        }
        else
        {
            // 수평선을 움직이면 y좌표
            pixel = RectYToPixelY(mousePosition.y);
        }
        
        pixel = ClampPixel(line.Type, pixel);
        
        Debug.Log(pixel);
        
        SetPixel(line.Type, pixel);
    }
    
    // 현재 호버한 라인의 SliceLine을 리턴하는 메서드
    private SliceLine GetHoveredLine(Vector2 mousePosition)
    {
        foreach(var line in _lines)
        {
            float pixel = GetPixel(line.Type);
            
            bool hit = 
            line.IsVertical ? 
            CanGrapVerticalLine(mousePosition, pixel) : CanGrapHorizontalLine(mousePosition, pixel);
            
            if(hit) return line;
        }
        
        return new SliceLine(LineType.None, false);
    }
    
    // 수직선으로부터 x축으로 좌우 5픽셀 미만이면 그랩하는 함수
    private bool CanGrapVerticalLine(Vector2 mousePosition, float pixel)
    {
        float normalized = pixel / _sprite.rect.width;
        
        float x = Mathf.Lerp(_spriteRect.xMin, _spriteRect.xMax, normalized);
        
        
        return Mathf.Abs(mousePosition.x - x) < 5f;
    }
    
    // 수평선으로부터 y축으로 좌우 5픽셀 미만이면 그랩하는 함수
    private bool CanGrapHorizontalLine(Vector2 mousePosition, float pixel)
    {
        float normalized = pixel / _sprite.rect.height;
        
        float y = Mathf.Lerp(_spriteRect.yMax, _spriteRect.yMin, normalized);
        
        
        return Mathf.Abs(mousePosition.y - y) < 5f;
    }
    
    
    /// <summary>
    /// 조정에 필요한 8개의 Line을 그리는 함수
    /// </summary>
    private void DrawLine(SliceLine line)
    {
        if(_sprite == null) return;
        
        float pixel = GetPixel(line.Type);

        if(line.IsVertical) // 수직 라인 처리
        {
            float normalized = pixel / _sprite.rect.width;
        
            float x = Mathf.Lerp(_spriteRect.xMin, _spriteRect.xMax, normalized);
        
            Handles.color = GetLineColor(line.Type);
        
            Handles.DrawLine(
                new Vector2(x, _spriteRect.yMin), 
                new Vector2(x, _spriteRect.yMax)
            );
        }
        else // 수평 라인 처리
        {
            float normalized = pixel / _sprite.rect.height;
        
            // GUI좌표는 위가 0 아래가 증가이기 때문에 yMax -> yMin으로 Lerp
            float y = Mathf.Lerp(_spriteRect.yMax, _spriteRect.yMin, normalized);
        
            Handles.color = GetLineColor(line.Type);
        
            Handles.DrawLine(
                new Vector2(_spriteRect.xMin, y),
                new Vector2(_spriteRect.xMax, y)
            );
        }
    }
    
    // 호버/드래그 시 라인 
    private Color GetLineColor(LineType line)
    {
        if (draggingLine.Type == line)
            return Color.yellow;

        if (hoveredLine.Type == line)
            return Color.yellow;

        return Color.green;
    }


    // 프리뷰 상의 Rect x좌표 이동을 실제 sprite 기준의 pixel상의 x좌표로 전환하는 함수
    private float RectXToPixelX(float previewX)
    {
        float normalized = Mathf.InverseLerp(_spriteRect.xMin, _spriteRect.xMax, previewX);
        
        return Mathf.Round(normalized * _sprite.rect.width);
    }
    
    // 프리뷰 상의 Rect y좌표 이동을 실제 sprite 기준의 pixel상의 y좌표로 전환하는 함수
    private float RectYToPixelY(float previewY)
    {
        float normalized = Mathf.InverseLerp(_spriteRect.yMax, _spriteRect.yMin, previewY);
        
        return Mathf.Round(normalized * _sprite.rect.height);
    }
    
    // 라인이 다음 라인을 넘거나 겹치지 않도록 하는 함수
    private float ClampPixel(LineType line, float pixel)
    {
        const float minGap = 1f;
        
        pixel = Mathf.Round(pixel);
        
        switch (line)
        {
            case LineType.Left:
                return Mathf.Clamp(
                    pixel,
                    0f,
                    _sliceData.LeftInner - minGap);

            case LineType.LeftInner:
                return Mathf.Clamp(
                    pixel,
                    _sliceData.Left + minGap,
                    _sliceData.RightInner - minGap);

            case LineType.RightInner:
                return Mathf.Clamp(
                    pixel,
                    _sliceData.LeftInner + minGap,
                    _sliceData.Right - minGap);

            case LineType.Right:
                return Mathf.Clamp(
                    pixel,
                    _sliceData.RightInner + minGap,
                    _sprite.rect.width);

            case LineType.Bottom:
                return Mathf.Clamp(
                    pixel,
                    0f,
                    _sliceData.BottomInner - minGap);

            case LineType.BottomInner:
                return Mathf.Clamp(
                    pixel,
                    _sliceData.Bottom + minGap,
                    _sliceData.TopInner - minGap);

            case LineType.TopInner:
                return Mathf.Clamp(
                    pixel,
                    _sliceData.BottomInner + minGap,
                    _sliceData.Top - minGap);

            case LineType.Top:
                return Mathf.Clamp(
                    pixel,
                    _sliceData.TopInner + minGap,
                    _sprite.rect.height);

            default:
                return pixel;
        }
    }
    
    // 현재 라인 타입에 해당하는 실제 sprite의 sliceData를 가져오는 함수
    private float GetPixel(LineType line)
    {
        switch(line)
        {
            case LineType.Left:
                return _sliceData.Left;

            case LineType.LeftInner:
                return _sliceData.LeftInner;

            case LineType.RightInner:
                return _sliceData.RightInner;

            case LineType.Right:
                return _sliceData.Right;

            case LineType.Bottom:
                return _sliceData.Bottom;

            case LineType.BottomInner:
                return _sliceData.BottomInner;

            case LineType.TopInner:
                return _sliceData.TopInner;

            case LineType.Top:
                return _sliceData.Top;

            default:
                return 0f;
        }
    }
    
    // 현재 드래그 중인 Line의 위치 값을 바꾸는 함수
    private void SetPixel(LineType line, float value)
    {
        switch (line)
        {
            case LineType.Left:
                _sliceData.Left = value;
                break;

            case LineType.LeftInner:
                _sliceData.LeftInner = value;
                break;

            case LineType.RightInner:
                _sliceData.RightInner = value;
                break;

            case LineType.Right:
                _sliceData.Right = value;
                break;

            case LineType.Bottom:
                _sliceData.Bottom = value;
                break;

            case LineType.BottomInner:
                _sliceData.BottomInner = value;
                break;

            case LineType.TopInner:
                _sliceData.TopInner = value;
                break;

            case LineType.Top:
                _sliceData.Top = value;
                break;
        }
    }
    
    #endregion
    
    private void HandleZoom()
    {
        Event eve = Event.current;
        
        if(eve.type != EventType.ScrollWheel) return;
        
        float zoomDelta = -eve.delta.y * 0.1f;
        
        _zoom = Mathf.Clamp(_zoom + zoomDelta, MinZoom, MaxZoom);
        
        Repaint();
        
        eve.Use();
    }
    
    private void HandlePan()
    {
        Event eve = Event.current;
        
        if(eve.button == 2 && eve.type == EventType.MouseDown)
        {
            _isPanning = true;
            eve.Use();
        }
        
        if(_isPanning && eve.type == EventType.MouseDrag)
        {
            _panOffset += eve.delta;

            _panOffset.y = Mathf.Max(_panOffset.y, 0f);

            Repaint();
            eve.Use();
        }
        
        if(eve.button == 2 && eve.type == EventType.MouseUp)
        {
            _isPanning = false;
            eve.Use();
        }
        
        if(eve.keyCode == KeyCode.F)
        {
            _zoom = 1f;
            _panOffset = Vector2.zero;
        }
    }
}

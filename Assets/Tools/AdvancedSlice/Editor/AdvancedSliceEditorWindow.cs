using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AdvancedSliceEditorWindow : EditorWindow
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
    
    private SliceLine[] _lines;
    
    private static readonly LineType[] X3Lines =
    {
        LineType.Left,
        LineType.Right
    };

    private static readonly LineType[] X5Lines =
    {
        LineType.Left,
        LineType.LeftInner,
        LineType.RightInner,
        LineType.Right
    };

    private static readonly LineType[] Y3Lines =
    {
        LineType.Bottom,
        LineType.Top
    };

    private static readonly LineType[] Y5Lines =
    {
        LineType.Bottom,
        LineType.BottomInner,
        LineType.TopInner,
        LineType.Top
    };
    
    private Sprite _sprite;
    private AdvancedSliceMode _sliceMode;
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
    private const float _minGap = 1f;
    
    // 선 정보가 변경된 것을 감지하는 더티플래그
    private bool _isDirty;
    
    private float _zoom = 1f;

    private const float MinZoom = 0.25f;
    private const float MaxZoom = 16f;
    
    private Vector2 _panOffset;
    private bool _isPanning;
    
    public static void Open(Sprite sprite, AdvancedSliceMode sliceMode)
    {
        var window = GetWindow<AdvancedSliceEditorWindow>("AdvancedSlice");
        
        window.SetSprite(sprite, sliceMode);
    }

    private void SetSprite(Sprite sprite, AdvancedSliceMode sliceMode)
    {
        _sprite = sprite;
        
        _sliceMode = sliceMode;
        
        _lines = BuildLines(sliceMode);
        
        // sliceData 캐싱
        _sliceData = AdvancedSliceImporterUtil.LoadOrCreateDefault(_sprite);
        
        // sprite texture 캐싱
        _spriteTexture = AssetPreview.GetAssetPreview(_sprite);
        Repaint();
    }

    private void OnGUI()
    {
        DrawToolbar();
        
        DrawPreview();
        
        foreach(var line in _lines) DrawLine(line);
        
        HandleZoom();
        
        HandlePan();
        
        HandleLine();
        
        DrawPixelInfo();
    }

    #region Toolbar Method
    
    /// <summary>
    /// 버튼 등의 툴바를 그리는 함수
    /// </summary>
    private void DrawToolbar()
    {
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
    /// 전체 스프라이트 픽셀에서 선이 어느 위치에 있는지 표시하는 함수
    /// </summary>
    private void DrawPixelInfo() 
    {
        if(_sprite == null) return;
        
        SliceLine targetLine = draggingLine.Type != LineType.None ? draggingLine : hoveredLine;
        
        if(targetLine.Type == LineType.None) return;
        
        float pixel = GetPixel(targetLine.Type); // 현재 선택된 라인의 픽셀 위치
        
        string text = targetLine.IsVertical ?
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
    
    /// <summary>
    /// SliceMode에 따라 그릴 선을 선별하는 함수
    /// </summary>
    /// <param name="sliceMode"></param>
    /// <returns></returns>
    private SliceLine[] BuildLines(AdvancedSliceMode sliceMode)
    {
        var lines = new List<SliceLine>();

        bool hasX3 = sliceMode == AdvancedSliceMode.ThreeByOne ||
                     sliceMode == AdvancedSliceMode.ThreeByThree ||
                     sliceMode == AdvancedSliceMode.ThreeByFive;

        bool hasX5 = sliceMode == AdvancedSliceMode.FiveByOne ||
                     sliceMode == AdvancedSliceMode.FiveByThree ||
                     sliceMode == AdvancedSliceMode.FiveByFive;

        bool hasY3 = sliceMode == AdvancedSliceMode.OneByThree ||
                     sliceMode == AdvancedSliceMode.ThreeByThree ||
                     sliceMode == AdvancedSliceMode.FiveByThree;

        bool hasY5 = sliceMode == AdvancedSliceMode.OneByFive ||
                     sliceMode == AdvancedSliceMode.ThreeByFive ||
                     sliceMode == AdvancedSliceMode.FiveByFive;
        if(hasX5)
        {
            lines.Add(new SliceLine(LineType.Left, true));
            lines.Add(new SliceLine(LineType.LeftInner, true));
            lines.Add(new SliceLine(LineType.RightInner, true));
            lines.Add(new SliceLine(LineType.Right, true));
        }
        else if(hasX3)
        {
            lines.Add(new SliceLine(LineType.Left, true));
            lines.Add(new SliceLine(LineType.Right, true));
        }

        if(hasY5)
        {
            lines.Add(new SliceLine(LineType.Bottom, false));
            lines.Add(new SliceLine(LineType.BottomInner, false));
            lines.Add(new SliceLine(LineType.TopInner, false));
            lines.Add(new SliceLine(LineType.Top, false));
        }
        else if(hasY3)
        {
            lines.Add(new SliceLine(LineType.Bottom, false));
            lines.Add(new SliceLine(LineType.Top, false));
        }

        return lines.ToArray();
    }
    
    /// <summary>
    ///  라인에 대한 마우스 이벤트를 다루는 함수
    /// </summary>
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
    
    /// <summary>
    /// 조정에 필요한 Line을 그리는 함수
    /// </summary>
    private void DrawLine(SliceLine line)
    {
        if(_sprite == null) return;
        
        float pixel = GetPixel(line.Type);
        
        Vector2 start;
        Vector2 end;

        if(line.IsVertical) // 수직 라인 처리
        {
            float normalized = pixel / _sprite.rect.width;
        
            float x = Mathf.Lerp(_spriteRect.xMin, _spriteRect.xMax, normalized);
            
            start = new Vector2(x, _spriteRect.yMin);
            end   = new Vector2(x, _spriteRect.yMax);
        }
        else // 수평 라인 처리
        {
            float normalized = pixel / _sprite.rect.height;
        
            // GUI좌표는 위가 0 아래가 증가이기 때문에 yMax -> yMin으로 Lerp
            float y = Mathf.Lerp(_spriteRect.yMax, _spriteRect.yMin, normalized); 
            
            start = new Vector2(_spriteRect.xMin, y);
            end   = new Vector2(_spriteRect.xMax, y);
        }
        
        Handles.color = GetLineColor(line.Type);
        Handles.DrawLine(start, end);
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
        
        pixel = ClampPixel(line.Type, pixel); // 다른 선을 넘지 않도록 clamp
        
        SetPixel(line.Type, pixel);
    }
    
    // 현재 호버한 라인의 SliceLine을 리턴하는 메서드
    private SliceLine GetHoveredLine(Vector2 mousePosition)
    {
        foreach(var line in _lines)
        {
            float pixel = GetPixel(line.Type);
            
            bool hit = CanGrabLine(mousePosition, pixel, line.IsVertical);  
 
            if(hit) return line;
        }
        
        return new SliceLine(LineType.None, false);
    }
    
    // 수직/수평선으로부터 x축으로 좌우 5픽셀 미만이면 그랩하는 함수
    private bool CanGrabLine(Vector2 mousePosition, float pixel, bool vertical)
    {
        if (vertical)
        {
            float normalized = pixel / _sprite.rect.width;

            float x = Mathf.Lerp(
                _spriteRect.xMin,
                _spriteRect.xMax,
                normalized);

            return Mathf.Abs(mousePosition.x - x) < 5f;
        }
        else
        {
            float normalizedY = pixel / _sprite.rect.height;

            float y = Mathf.Lerp(
                _spriteRect.yMax,
                _spriteRect.yMin,
                normalizedY);

            return Mathf.Abs(mousePosition.y - y) < 5f;
        }
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
    
    private float ClampPixel(LineType line, float pixel)
    {
        pixel = Mathf.Round(pixel);

        if (IsVerticalLine(line))
        {
            LineType[] lines = GetXLines();

            return ClampLine(
                line,
                pixel,
                lines,
                0f,
                _sprite.rect.width);
        }
        else
        {
            LineType[] lines = GetYLines();

            return ClampLine(
                line,
                pixel,
                lines,
                0f,
                _sprite.rect.height);
        }
    }
    
    private float ClampLine(LineType line, float pixel, LineType[] lines, float min, float max)
    { 
        if (lines == null) return pixel;

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] != line) continue;
            
            float lower = i == 0 ? min : GetPixel(lines[i - 1]) + _minGap; //직전 인덱스의 위치 - 1px 까지
            
            float upper = i == lines.Length - 1 ? max : GetPixel(lines[i + 1]) - _minGap; // 다음 인덱스의 위치 + 1px 까지

            return Mathf.Clamp(pixel, lower, upper);
        }

        return pixel;
    }
    
    private LineType[] GetXLines()
    {
        switch (_sliceMode)
        {
            case AdvancedSliceMode.OneByThree:
            case AdvancedSliceMode.OneByFive:
                return null;

            case AdvancedSliceMode.ThreeByOne:
            case AdvancedSliceMode.ThreeByThree:
            case AdvancedSliceMode.ThreeByFive:
                return X3Lines;

            case AdvancedSliceMode.FiveByOne:
            case AdvancedSliceMode.FiveByThree:
            case AdvancedSliceMode.FiveByFive:
            default:
                return X5Lines;
        }
    }

    private LineType[] GetYLines()
    {
        switch (_sliceMode)
        {
            case AdvancedSliceMode.ThreeByOne:
            case AdvancedSliceMode.FiveByOne:
                return null;

            case AdvancedSliceMode.OneByThree:
            case AdvancedSliceMode.ThreeByThree:
            case AdvancedSliceMode.FiveByThree:
                return Y3Lines;

            case AdvancedSliceMode.OneByFive:
            case AdvancedSliceMode.ThreeByFive:
            case AdvancedSliceMode.FiveByFive:
            default:
                return Y5Lines;
        }
    }
    
    private static bool IsVerticalLine(LineType line)
    {
        return line == LineType.Left
               || line == LineType.LeftInner
               || line == LineType.RightInner
               || line == LineType.Right;
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
    // 스크롤 이벤트를 입력받아 줌 처리
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
        
        if(eve.type == EventType.KeyDown && eve.keyCode == KeyCode.F)
        {
            _zoom = 1f;
            _panOffset = Vector2.zero;
        }
    }
}

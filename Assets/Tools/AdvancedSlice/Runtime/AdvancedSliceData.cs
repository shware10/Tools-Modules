using System;

[Serializable]
public struct AdvancedSliceData
{
    public float Left;
    public float LeftInner;
    public float RightInner;
    public float Right;
    
    public float Bottom;
    public float BottomInner;
    public float TopInner;
    public float Top;
    
    public static AdvancedSliceData GenerateDefault(float width, float height)
    {
        return new AdvancedSliceData
        {
            Left = width * 0.15f,
            LeftInner = width * 0.3f,
            RightInner = width * 0.7f,
            Right = width * 0.85f,
            
            Bottom = height * 0.15f,
            BottomInner = height * 0.3f,
            TopInner = height * 0.7f,
            Top = height * 0.85f
        };
    }
}

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[InitializeOnLoad]
public static class MSDFColoredDrawer
{
    static List<MSDFCore.ColoredSegment> segments;

    static MSDFColoredDrawer()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    public static void SetSegments(List<MSDFCore.ColoredSegment> segs)
    {
        segments = segs;
        SceneView.RepaintAll();
    }

    static void OnSceneGUI(SceneView sceneView)
    {
        if (segments == null) return;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        foreach (var cs in segments)
        {
            Handles.color = GetColor(cs.color);

            Vector3 a = new Vector3(cs.seg.a.x, cs.seg.a.y, 0);
            Vector3 b = new Vector3(cs.seg.b.x, cs.seg.b.y, 0);

            Handles.DrawLine(a, b, 2f);
        }
    }

    static Color GetColor(MSDFCore.EdgeColor c)
    {
        switch (c)
        {
            case MSDFCore.EdgeColor.R: return Color.red;
            case MSDFCore.EdgeColor.G: return Color.green;
            case MSDFCore.EdgeColor.B: return Color.blue;
        }
        return Color.white;
    }
}
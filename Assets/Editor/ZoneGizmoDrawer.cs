using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ZoneGizmoDrawer
{
    static ZoneGizmoDrawer()
    {
        SceneView.duringSceneGui += OnSceneGui;
    }

    private static void OnSceneGui(SceneView sceneView)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        List<ZoneGizmoRect> airZones = CollectAirZones();
        DrawWaterZones(airZones);
        DrawAirZoneOutlines(airZones);
    }

    private static List<ZoneGizmoRect> CollectAirZones()
    {
        List<ZoneGizmoRect> airZones = new List<ZoneGizmoRect>();
        AirZone[] zones = Resources.FindObjectsOfTypeAll<AirZone>();
        for (int i = 0; i < zones.Length; i++)
        {
            AirZone airZone = zones[i];
            if (airZone == null || !Utils.IsSceneInstance(airZone.gameObject) || !airZone.TryGetGizmoBounds(out Bounds bounds))
            {
                continue;
            }

            airZones.Add(new ZoneGizmoRect(BoundsToRect(bounds), airZone.GizmoColor));
        }

        return airZones;
    }

    private static void DrawWaterZones(List<ZoneGizmoRect> airZones)
    {
        WaterZone[] waterZones = Resources.FindObjectsOfTypeAll<WaterZone>();
        for (int i = 0; i < waterZones.Length; i++)
        {
            WaterZone waterZone = waterZones[i];
            if (waterZone == null || !Utils.IsSceneInstance(waterZone.gameObject) || !waterZone.TryGetGizmoBounds(out Bounds bounds))
            {
                continue;
            }

            Rect waterRect = BoundsToRect(bounds);
            List<Rect> visibleRects = SubtractAirZones(waterRect, airZones);
            for (int rectIndex = 0; rectIndex < visibleRects.Count; rectIndex++)
            {
                DrawZoneRect(visibleRects[rectIndex], waterZone.GizmoColor, true);
            }
        }
    }

    private static void DrawAirZoneOutlines(List<ZoneGizmoRect> airZones)
    {
        for (int i = 0; i < airZones.Count; i++)
        {
            DrawZoneRect(airZones[i].Rect, airZones[i].Color, false);
        }
    }

    private static List<Rect> SubtractAirZones(Rect waterRect, List<ZoneGizmoRect> airZones)
    {
        List<Rect> remainingRects = new List<Rect> { waterRect };
        for (int i = 0; i < airZones.Count; i++)
        {
            Rect airRect = airZones[i].Rect;
            List<Rect> nextRects = new List<Rect>();
            for (int rectIndex = 0; rectIndex < remainingRects.Count; rectIndex++)
            {
                AddRectMinusOverlap(remainingRects[rectIndex], airRect, nextRects);
            }

            remainingRects = nextRects;
        }

        return remainingRects;
    }

    private static void AddRectMinusOverlap(Rect source, Rect cutter, List<Rect> output)
    {
        float overlapMinX = Mathf.Max(source.xMin, cutter.xMin);
        float overlapMaxX = Mathf.Min(source.xMax, cutter.xMax);
        float overlapMinY = Mathf.Max(source.yMin, cutter.yMin);
        float overlapMaxY = Mathf.Min(source.yMax, cutter.yMax);

        if (overlapMinX >= overlapMaxX || overlapMinY >= overlapMaxY)
        {
            output.Add(source);
            return;
        }

        if (source.yMin < overlapMinY)
        {
            output.Add(Rect.MinMaxRect(source.xMin, source.yMin, source.xMax, overlapMinY));
        }

        if (overlapMaxY < source.yMax)
        {
            output.Add(Rect.MinMaxRect(source.xMin, overlapMaxY, source.xMax, source.yMax));
        }

        if (source.xMin < overlapMinX)
        {
            output.Add(Rect.MinMaxRect(source.xMin, overlapMinY, overlapMinX, overlapMaxY));
        }

        if (overlapMaxX < source.xMax)
        {
            output.Add(Rect.MinMaxRect(overlapMaxX, overlapMinY, source.xMax, overlapMaxY));
        }
    }

    private static Rect BoundsToRect(Bounds bounds)
    {
        return Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
    }

    private static void DrawZoneRect(Rect rect, Color color, bool drawFill)
    {
        Vector3[] corners =
        {
            new Vector3(rect.xMin, rect.yMin, 0f),
            new Vector3(rect.xMin, rect.yMax, 0f),
            new Vector3(rect.xMax, rect.yMax, 0f),
            new Vector3(rect.xMax, rect.yMin, 0f)
        };

        Color fillColor = drawFill ? color : Color.clear;
        Color outlineColor = new Color(color.r, color.g, color.b, 0.95f);
        Handles.DrawSolidRectangleWithOutline(corners, fillColor, outlineColor);
    }

    private readonly struct ZoneGizmoRect
    {
        public ZoneGizmoRect(Rect rect, Color color)
        {
            Rect = rect;
            Color = color;
        }

        public Rect Rect { get; }
        public Color Color { get; }
    }
}

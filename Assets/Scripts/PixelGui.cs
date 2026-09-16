using System;
using UnityEngine;

namespace EarthRecovery
{
    // Keep the reference-coordinate layout, but rasterize IMGUI glyphs at their final pixel size.
    public static class PixelGui
    {
        public static int FontPixels(int size, float scale) => Mathf.Max(1, Mathf.RoundToInt(size * scale));

        sealed class Pixels : IDisposable
        {
            readonly Matrix4x4 matrix = GUI.matrix;
            public readonly Rect rect;
            public readonly GUIStyle style;
            public readonly float scale;
            public Pixels(Rect bounds, GUIStyle source)
            {
                scale = Mathf.Max(.01f, matrix.MultiplyVector(Vector3.up).magnitude);
                style = ScaledStyle(source, scale);
                GUI.matrix = matrix * Matrix4x4.Scale(new Vector3(1 / scale, 1 / scale, 1));
                rect = ScaledRect(bounds, scale);
            }
            public void Dispose() => GUI.matrix = matrix;
        }
        static Rect ScaledRect(Rect r, float scale) => new(r.position * scale, r.size * scale);
        static RectOffset Scale(RectOffset v, float s) => new(Mathf.RoundToInt(v.left*s), Mathf.RoundToInt(v.right*s), Mathf.RoundToInt(v.top*s), Mathf.RoundToInt(v.bottom*s));
        static GUIStyle ScaledStyle(GUIStyle source, float scale) => new(source)
        {
            fontSize = FontPixels(source.fontSize > 0 ? source.fontSize : 18, scale),
            padding = Scale(source.padding, scale), margin = Scale(source.margin, scale),
            border = Scale(source.border, scale), overflow = Scale(source.overflow, scale),
            fixedWidth = source.fixedWidth * scale, fixedHeight = source.fixedHeight * scale,
            contentOffset = source.contentOffset * scale
        };

        // Create the clip and its content in the same pixel coordinate system. Changing
        // GUI.matrix after BeginScrollView leaves Unity's clip origin at the old scale.
        public sealed class ScrollView : IDisposable
        {
            readonly Matrix4x4 matrix = GUI.matrix;
            readonly float scale;
            public ScrollView(Rect area, ref Vector2 scroll, Rect content)
            {
                scale = Mathf.Max(.01f, matrix.MultiplyVector(Vector3.up).magnitude);
                var pixels = new Rect(matrix.MultiplyPoint3x4(area.position), area.size * scale);
                GUI.matrix = Matrix4x4.identity;
                scroll = GUI.BeginScrollView(pixels, scroll * scale, ScaledRect(content, scale), false, false,
                    ScaledStyle(GUI.skin.horizontalScrollbar, scale), ScaledStyle(GUI.skin.verticalScrollbar, scale)) / scale;
            }
            public void Label(Rect area, string value, GUIStyle style) => GUI.Label(ScaledRect(area, scale), value, ScaledStyle(style, scale));
            public float CalcHeight(string value, float width, GUIStyle style) => ScaledStyle(style, scale).CalcHeight(new GUIContent(value), width * scale) / scale;
            public void Dispose() { GUI.EndScrollView(); GUI.matrix = matrix; }
        }
        public static void Label(Rect rect, string text, GUIStyle style)
        { using var p = new Pixels(rect, style); GUI.Label(p.rect, text, p.style); }
        public static float CalcHeight(string text, float width, GUIStyle style)
        { using var p = new Pixels(new Rect(0, 0, width, 0), style); return p.style.CalcHeight(new GUIContent(text), p.rect.width) / p.scale; }
        public static bool Button(Rect rect, string text, GUIStyle style) => Button(rect, new GUIContent(text), style);
        public static bool Button(Rect rect, GUIContent content, GUIStyle style)
        { using var p = new Pixels(rect, style); return GUI.Button(p.rect, content, p.style); }
        public static bool RepeatButton(Rect rect, string text, GUIStyle style)
        { using var p = new Pixels(rect, style); return GUI.RepeatButton(p.rect, text, p.style); }
        public static string TextField(Rect rect, string value, int maxLength)
        { using var p = new Pixels(rect, GUI.skin.textField); return GUI.TextField(p.rect, value, maxLength, p.style); }
    }
}

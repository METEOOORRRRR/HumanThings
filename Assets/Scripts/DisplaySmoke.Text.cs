#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;

namespace EarthRecovery
{
    public sealed partial class DisplaySmoke
    {
        bool textProbe;
        Rect probePixels;
        readonly Rect probeArea = new(696, 590, 440, 116);

        IEnumerator VerifyScrollPixels()
        {
            bool hudEnabled = hud.enabled;
            hud.enabled = false;
            textProbe = true;
            yield return null;
            yield return new WaitForEndOfFrame();
            var capture = ScreenCapture.CaptureScreenshotAsTexture();
            textProbe = false;
            hud.enabled = hudEnabled;
            var pixels = capture.GetPixels32();
            // A fractional clip edge can cover part of its boundary pixel.
            var pixelBounds = Rect.MinMaxRect(Mathf.Floor(probePixels.xMin), Mathf.Floor(probePixels.yMin), Mathf.Ceil(probePixels.xMax), Mathf.Ceil(probePixels.yMax));
            int inside = 0, outside = 0;
            for (int y = 0; y < capture.height; y++)
                for (int x = 0; x < capture.width; x++)
                {
                    var pixel = pixels[y * capture.width + x];
                    if (pixel.r < 180 || pixel.b < 180 || pixel.g > 70) continue;
                    if (pixelBounds.Contains(new Vector2(x + .5f, capture.height - y - .5f))) inside++;
                    else outside++;
                }
            if (inside < 20 || outside > 0)
            {
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(output, "scroll-pixels-failure.png"), capture.EncodeToPNG());
                Destroy(capture); Fail("scroll glyph pixels: inside=" + inside + ", outside=" + outside + ", expected=" + probePixels); yield break;
            }
            Destroy(capture);
            checks++;
            yield return null;
        }

        void OnGUI()
        {
            if (!textProbe) return;
            var matrix = GUI.matrix; var color = GUI.color;
            GUI.depth = -2000; GUI.matrix = Matrix4x4.identity; GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white; GUI.matrix = DisplayPreferences.GuiMatrix(1672, 941);
            float scale = GUI.matrix.MultiplyVector(Vector3.up).magnitude;
            probePixels = new Rect(GUI.matrix.MultiplyPoint3x4(probeArea.position), probeArea.size * scale);
            var style = new GUIStyle(GUI.skin.label) { font = world.font, fontSize = 22, padding = new RectOffset(), wordWrap = true };
            style.normal.textColor = Color.magenta;
            var scroll = new Vector2(0, 12);
            using (var view = new PixelGui.ScrollView(probeArea, ref scroll, new Rect(0, 0, 410, 500)))
                for (int i = 0; i < 12; i++) view.Label(new Rect(0, i * 42, 400, 36), "MMMM native pixel scroll", style);
            GUI.matrix = matrix; GUI.color = color;
        }
    }
}
#endif

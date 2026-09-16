#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace EarthRecovery
{
    public sealed partial class DisplaySmoke
    {
        Camera canvasCamera;
        Texture2D CaptureCanvas(RectTransform layout)
        {
            if (canvasCamera == null)
            {
                canvasCamera = new GameObject("Offscreen UI verification").AddComponent<Camera>();
                canvasCamera.transform.SetParent(transform, false);
                canvasCamera.enabled = false; canvasCamera.cullingMask = 1 << 31;
                canvasCamera.clearFlags = CameraClearFlags.SolidColor; canvasCamera.backgroundColor = Color.black;
                canvasCamera.nearClipPlane = .01f; canvasCamera.farClipPlane = 10;
                world.eye.cullingMask &= ~(1 << 31);
            }
            var target = RenderTexture.GetTemporary(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            var active = RenderTexture.active;
            try
            {
                canvasCamera.targetTexture = target;
                foreach (var canvas in GetComponentsInChildren<Canvas>())
                {
                    foreach (var child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                    if (!canvas.isRootCanvas) continue;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = canvasCamera; canvas.planeDistance = 1;
                }
                Canvas.ForceUpdateCanvases();
                foreach (var camera in GetComponentsInChildren<Camera>())
                    if (camera != canvasCamera && camera.targetTexture != null) camera.Render();
                canvasCamera.Render();
                if (layout != null)
                {
                    var corners = new Vector3[4]; layout.GetWorldCorners(corners); var viewport = DisplayPreferences.Viewport;
                    foreach (var corner in corners)
                    {
                        var p = RectTransformUtility.WorldToScreenPoint(canvasCamera, corner);
                        if (p.x < viewport.xMin - 1 || p.x > viewport.xMax + 1 || p.y < viewport.yMin - 1 || p.y > viewport.yMax + 1)
                            throw new System.InvalidOperationException("Canvas bounds " + p + " outside " + viewport + " at " + Screen.width + "x" + Screen.height);
                    }
                    checks++;
                }
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                return image;
            }
            finally { RenderTexture.active = active; canvasCamera.targetTexture = null; RenderTexture.ReleaseTemporary(target); }
        }
    }
}
#endif

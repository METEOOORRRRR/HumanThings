using UnityEngine;
using UnityEngine.Rendering;

namespace EarthRecovery
{
    public sealed class PlayerCameraVisibility : MonoBehaviour
    {
        Renderer[] renderers;
        ShadowCastingMode[] shadows;
        public bool Hidden { get; private set; }

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            shadows = new ShadowCastingMode[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) shadows[i] = renderers[i].shadowCastingMode;
        }

        public bool Present(Camera camera, bool followed)
        {
            bool hide = false;
            if (followed)
            {
                float margin = WorldView.CameraCollisionRadius(camera) + (Hidden ? .15f : .04f);
                foreach (var renderer in renderers)
                    if (renderer != null && renderer.enabled && renderer.bounds.SqrDistance(camera.transform.position) < margin * margin)
                    { hide = true; break; }
            }
            if (hide == Hidden) return false;
            Hidden = hide;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].shadowCastingMode = hide
                    ? (shadows[i] == ShadowCastingMode.Off ? ShadowCastingMode.Off : ShadowCastingMode.ShadowsOnly) : shadows[i];
            // Renderers that originally cast no shadow must still disappear from this camera.
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && shadows[i] == ShadowCastingMode.Off) renderers[i].forceRenderingOff = hide;
            return true;
        }

        void OnDisable()
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) { renderers[i].shadowCastingMode = shadows[i]; renderers[i].forceRenderingOff = false; }
            Hidden = false;
        }
    }
}

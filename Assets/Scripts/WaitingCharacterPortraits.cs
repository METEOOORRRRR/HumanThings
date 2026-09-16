using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EarthRecovery
{
    // One cached portrait per roster character, shared by all matching cards.
    // Render the gameplay prefab so portrait textures cannot drift from the game.
    public sealed class WaitingCharacterPortraits : MonoBehaviour
    {
        sealed class Portrait
        {
            public Camera camera;
            public RenderTexture texture;
            public Light[] lights;
        }

        readonly Dictionary<string, Portrait> portraits = new();
        PlayerCharacterCatalog catalog;
        bool wasVisible;
        int frames;

        public void Initialize()
        {
            catalog = PlayerCharacterCatalog.Load();
            if (catalog == null) throw new System.InvalidOperationException("Character catalog is required for waiting-room portraits.");
            transform.position = new Vector3(10000, -1000, 0);
            foreach (var character in catalog.characters)
            {
                var stage = new GameObject("Portrait " + character.id).transform;
                stage.SetParent(transform, false); stage.localPosition = Vector3.right * portraits.Count * 12;
                var model = Instantiate(character.Prefab, stage, false);
                model.name = character.id;
                foreach (var node in model.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 30;
                var avatar = model.GetComponent<PlayerAvatar>();
                avatar.animator.Rebind(); avatar.animator.Update(0); avatar.animator.enabled = false;
                avatar.enabled = false;
                model.GetComponent<HumanThingsCharacterVisual>().CompareOriginal(false);
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                { skin.updateWhenOffscreen = true; skin.shadowCastingMode = ShadowCastingMode.Off; }

                var camera = new GameObject("Portrait camera").AddComponent<Camera>();
                camera.transform.SetParent(stage, false);
                camera.transform.localPosition = new Vector3(0, 1.51f, 3);
                camera.transform.LookAt(stage.position + Vector3.up * 1.51f);
                camera.orthographic = true; camera.orthographicSize = .40f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 6;
                camera.cullingMask = 1 << 30; camera.useOcclusionCulling = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.085f, .105f, .11f);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                camera.enabled = false;
                var portrait = new Portrait { camera = camera, lights = new[] {
                    PortraitLight(stage, "Key", new Vector3(-.7f, 2.1f, 1.7f), 2.2f, new Color(1, .95f, .88f)),
                    PortraitLight(stage, "Fill", new Vector3(.9f, 1.6f, 1.2f), 1.3f, new Color(.86f, .93f, 1))
                }};
                portraits.Add(character.id, portrait);
                Resize(character.id, portrait, new Vector2Int(256, 320));
            }
        }

        static Light PortraitLight(Transform parent, string name, Vector3 position, float intensity, Color color)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false); light.transform.localPosition = position;
            // Local lights cannot change the main light or illuminate the game world.
            light.type = LightType.Point; light.range = 4; light.intensity = intensity;
            light.color = color; light.cullingMask = 1 << 30; light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        public RenderTexture TextureFor(string characterId)
        {
            var entry = catalog.Resolve(characterId);
            return entry != null && portraits.TryGetValue(entry.id, out var portrait) ? portrait.texture : null;
        }

        public void Prepare(float uiScale, bool visible)
        {
            if (visible && !wasVisible) frames = 3;
            wasVisible = visible;
            if (visible)
            {
                var size = WaitingRoom.PortraitResolution(uiScale);
                foreach (var pair in portraits)
                    if (pair.Value.texture.width != size.x || pair.Value.texture.height != size.y)
                    { Resize(pair.Key, pair.Value, size); frames = 3; }
            }
            bool render = visible && frames > 0 && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            foreach (var portrait in portraits.Values)
            {
                portrait.camera.enabled = render;
                foreach (var light in portrait.lights) light.enabled = render;
            }
            if (render) frames--;
        }

        static void Resize(string id, Portrait portrait, Vector2Int size)
        {
            var previous = portrait.texture;
            portrait.texture = new RenderTexture(size.x, size.y, 24) { name = "Waiting portrait: " + id, antiAliasing = 4 };
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) portrait.texture.Create();
            portrait.camera.targetTexture = portrait.texture;
            if (previous != null) { previous.Release(); DisposeTexture(previous); }
        }

        static void DisposeTexture(RenderTexture texture)
        { if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture); }

        void OnDestroy()
        {
            foreach (var portrait in portraits.Values)
                if (portrait.texture != null)
                {
                    if (portrait.camera != null) portrait.camera.targetTexture = null;
                    portrait.texture.Release(); DisposeTexture(portrait.texture);
                }
            portraits.Clear();
        }
    }
}

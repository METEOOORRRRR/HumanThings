using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace EarthRecovery.Editor
{
    public static class HumanThingsCharacterVisualBuilder
    {
        public const string Folder = "Assets/HumanThings/Characters/Visual";
        public const string PrefabPath = Folder + "/Prefabs/HumanThings_Character_Visual.prefab";
        public const string ProfilePath = Folder + "/Resources/HumanThingsCharacterVisualProfile.asset";
        public const string MaterialPath = Folder + "/Materials/HT_NeonVanguard.mat";

        [MenuItem("Tools/HumanThings/Character/Build Visual Variant")]
        public static void Build() => BuildVariant(PlayerCharacterBuilder.PrefabPath, "NeonVanguard", PrefabPath, ProfilePath, Classify);

        public static void BuildVariant(string sourcePath, string prefix, string prefabPath, string profilePath,
            Func<Vector3, Color, float, Color> classify)
        {
            foreach (string name in new[] { "Prefabs", "Resources", "Materials", "Masks", "Textures" }) Directory.CreateDirectory(Folder + "/" + name);
            Directory.CreateDirectory(Path.GetDirectoryName(profilePath));
            AssetDatabase.Refresh();
            var shader = Shader.Find("HumanThings/Weathered Character");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Character shader is missing or has errors.");
            var profile = AssetDatabase.LoadAssetAtPath<HumanThingsCharacterVisualProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<HumanThingsCharacterVisualProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            if (profile.environmentLighting == null)
                profile.environmentLighting = AssetDatabase.LoadAssetAtPath<HumanThingsVisualProfile>(HumanThingsVisualTools.ProfilePath);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) throw new InvalidOperationException("Missing character prefab: " + sourcePath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                instance.name = Path.GetFileNameWithoutExtension(prefabPath);
                var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                if (skin.sharedMaterials.Length != 1 || skin.sharedMesh.subMeshCount != 1)
                    throw new InvalidOperationException("Character visual mapping requires a single-atlas model.");
                var original = skin.sharedMaterial;
                BakeMasks(instance, skin, original, prefix, classify);
                string materialPath = Folder + "/Materials/HT_" + prefix + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
                material.name = "HT_" + prefix;
                material.SetTexture("_BaseMap", MipmappedCopy(original.GetTexture("baseColorTexture"), prefix + "_Albedo", true));
                material.SetColor("_BaseColor", original.GetColor("baseColorFactor"));
                material.SetTexture("_BumpMap", MipmappedCopy(original.GetTexture("normalTexture"), prefix + "_Normal", false));
                material.SetTexture("_MetallicRoughnessMap", MipmappedCopy(original.GetTexture("metallicRoughnessTexture"), prefix + "_MetallicRoughness", false));
                material.SetTexture("_Regions", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Masks/" + prefix + "_Regions.png"));
                material.SetTexture("_WeatherMask", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Masks/" + prefix + "_Weather.png"));
                material.SetTexture("_DetailMap", AssetDatabase.LoadAssetAtPath<HumanThingsVisualProfile>(HumanThingsVisualTools.ProfilePath).detailTexture);
                profile.Apply(material); EditorUtility.SetDirty(material);
                skin.sharedMaterial = material;
                skin.skinnedMotionVectors = true;
                var visual = instance.AddComponent<HumanThingsCharacterVisual>();
                visual.skin = skin; visual.originalMaterial = original; visual.humanThingsMaterial = material;
                profile.visualPrefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
                Debug.Log("CHARACTER_VISUAL_BUILD_PASS original=" + original.name + " variant=" + PrefabUtility.GetPrefabAssetType(profile.visualPrefab));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        // Rasterize rest-pose surface labels into UV data. Neither mesh nor source textures are changed.
        static void BakeMasks(GameObject root, SkinnedMeshRenderer skin, Material original, string prefix,
            Func<Vector3, Color, float, Color> classify)
        {
            const int size = 1024;
            var baked = new Mesh();
            var albedo = Readback(original.GetTexture("baseColorTexture"), true);
            var metal = Readback(original.GetTexture("metallicRoughnessTexture"), false);
            try
            {
                skin.BakeMesh(baked, true);
                var points = baked.vertices.Select(p => root.transform.InverseTransformPoint(skin.transform.TransformPoint(p))).ToArray();
                var normals = baked.normals.Select(n => root.transform.InverseTransformDirection(skin.transform.TransformDirection(n)).normalized).ToArray();
                var uv = skin.sharedMesh.uv; var triangles = skin.sharedMesh.triangles;
                var neighbours = Enumerable.Range(0, points.Length).Select(_ => new System.Collections.Generic.HashSet<int>()).ToArray();
                for (int t = 0; t < triangles.Length; t += 3)
                    for (int j = 0; j < 3; j++)
                    {
                        int a = triangles[t + j], b = triangles[t + (j + 1) % 3];
                        neighbours[a].Add(b); neighbours[b].Add(a);
                    }
                var curve = new float[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    if (neighbours[i].Count == 0) continue;
                    var mean = Vector3.zero; float length = 0;
                    foreach (int j in neighbours[i]) { mean += points[j]; length += Vector3.Distance(points[i], points[j]); }
                    curve[i] = Vector3.Dot(points[i] - mean / neighbours[i].Count, normals[i]) / Mathf.Max(.001f, length / neighbours[i].Count);
                }
                var regions = new Color32[size * size]; var weather = new Color32[size * size]; var filled = new bool[size * size];
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    var u = uv[a] * size; var v = uv[b] * size; var w = uv[c] * size;
                    float denominator = Cross(v - u, w - u);
                    if (Mathf.Abs(denominator) < .00001f) continue;
                    int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(u.x, v.x, w.x)), 0, size - 1);
                    int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(u.x, v.x, w.x)), 0, size - 1);
                    int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(u.y, v.y, w.y)), 0, size - 1);
                    int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(u.y, v.y, w.y)), 0, size - 1);
                    for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
                    {
                        var q = new Vector2(x + .5f, y + .5f);
                        float wb = Cross(q - u, w - u) / denominator, wc = Cross(v - u, q - u) / denominator, wa = 1 - wb - wc;
                        if (wa < 0 || wb < 0 || wc < 0) continue;
                        int index = y * size + x; var at = points[a] * wa + points[b] * wb + points[c] * wc;
                        float cx = (x + .5f) / size, cy = (y + .5f) / size;
                        var color = albedo.GetPixelBilinear(cx, cy); float metallic = metal.GetPixelBilinear(cx, cy).b;
                        var region = classify(at, color, metallic); regions[index] = region;
                        float curvature = curve[a] * wa + curve[b] * wb + curve[c] * wc;
                        float lower = 1 - Smooth(.18f, .72f, at.y);
                        float knee = Mathf.Exp(-Mathf.Pow((at.y - .48f) / .1f, 2));
                        float hands = Smooth(.27f, .4f, Mathf.Abs(at.x)) * (1 - Smooth(.9f, 1.16f, at.y));
                        float elbow = Smooth(.3f, .45f, Mathf.Abs(at.x)) * Mathf.Exp(-Mathf.Pow((at.y - 1.18f) / .08f, 2));
                        float pack = (1 - Smooth(-.2f, -.07f, at.z)) * Mathf.Exp(-Mathf.Pow((at.y - 1.0f) / .12f, 2));
                        float noise = Mathf.PerlinNoise(17 + at.x * 15 + at.z * 7, 9 + at.y * 17);
                        float contact = Mathf.Clamp01(lower * .9f + knee * .3f + hands * .4f + elbow * .23f + pack * .25f) * Mathf.Lerp(.3f, 1, noise);
                        weather[index] = new Color(contact, Mathf.Clamp01(curvature * 5) * noise, Mathf.Clamp01(-curvature * 4), 1);
                        filled[index] = true;
                    }
                }
                // UV-island padding prevents the labels from bleeding to black at minified edges.
                for (int pass = 0; pass < 4; pass++)
                {
                    var next = (bool[])filled.Clone();
                    for (int y = 1; y < size - 1; y++) for (int x = 1; x < size - 1; x++)
                    {
                        int i = y * size + x; if (filled[i]) continue;
                        foreach (int j in new[] { i - 1, i + 1, i - size, i + size })
                            if (filled[j]) { regions[i] = regions[j]; weather[i] = weather[j]; next[i] = true; break; }
                    }
                    filled = next;
                }
                SaveMask(prefix + "_Regions", regions, size);
                SaveMask(prefix + "_Weather", weather, size);
                Debug.Log("CHARACTER_MASKS pixels=" + filled.Count(f => f) + " skin=" + regions.Count(c => c.r > 127) + " hair=" + regions.Count(c => c.g > 127) + " cloth=" + regions.Count(c => c.b > 127) + " equipment=" + regions.Count(c => c.a > 127));
            }
            finally { Object.DestroyImmediate(baked); Object.DestroyImmediate(albedo); Object.DestroyImmediate(metal); }
        }

        public static Color Classify(Vector3 position, Color albedo, float metallic)
        {
            float x = Mathf.Abs(position.x), y = position.y;
            bool warmSkin = albedo.r > .3f && albedo.r > albedo.g * 1.07f && albedo.g > albedo.b * .72f && albedo.r < albedo.g * 1.9f;
            bool face = y > 1.56f && y < 1.735f && x < .135f;
            bool wrist = y > .91f && y < 1.12f && x > .26f;
            if (warmSkin && (face || wrist)) return new Color(1, 0, 0, 0);
            if (y > 1.695f && x < .17f) return new Color(0, 1, 0, 0);
            float luma = albedo.r * .2126f + albedo.g * .7152f + albedo.b * .0722f;
            float lightCloth = Smooth(.28f, .6f, luma) * Smooth(.28f, .45f, y);
            float trousers = (1 - Smooth(.18f, .55f, metallic)) * Smooth(.22f, .4f, y) * (1 - Smooth(.95f, 1.15f, y)) * (1 - Smooth(.26f, .38f, x));
            float cloth = Mathf.Max(lightCloth, trousers);
            return new Color(0, 0, cloth, 1 - cloth);
        }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static float Smooth(float a, float b, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, value));

        static Texture2D Readback(Texture source, bool srgb)
        {
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            var old = RenderTexture.active;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, !srgb);
            try { Graphics.Blit(source, rt); RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply(); return texture; }
            finally { RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); }
        }
        static Texture2D MipmappedCopy(Texture source, string name, bool srgb)
        {
            string path = Folder + "/Textures/" + name + ".png";
            var pixels = Readback(source, srgb);
            try { File.WriteAllBytes(path, pixels.EncodeToPNG()); }
            finally { Object.DestroyImmediate(pixels); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            // Keep glTF's RGB normal encoding; the character shader does not use Unity's packed normal format.
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.mipmapEnabled = true;
            importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(source.width, source.height));
            importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
            importer.wrapMode = source.wrapMode; importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static void SaveMask(string name, Color32[] pixels, int size)
        {
            string path = Folder + "/Masks/" + name + ".png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            try { texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false; importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false; importer.mipmapEnabled = true; importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Trilinear;
            importer.SaveAndReimport();
        }

        [MenuItem("Tools/HumanThings/Character/Apply Profile")]
        public static void ApplyProfile() => ApplyProfile(AssetDatabase.LoadAssetAtPath<HumanThingsCharacterVisualProfile>(ProfilePath));

        public static void ApplyProfile(HumanThingsCharacterVisualProfile profile)
        {
            var visualPrefab = profile != null && profile.visualPrefab != null ? profile.visualPrefab.GetComponent<HumanThingsCharacterVisual>() : null;
            var material = visualPrefab != null ? visualPrefab.humanThingsMaterial : null;
            if (profile == null || material == null) throw new InvalidOperationException("Build the character visual variant first.");
            if (profile.environmentLighting == null)
            {
                profile.environmentLighting = AssetDatabase.LoadAssetAtPath<HumanThingsVisualProfile>(HumanThingsVisualTools.ProfilePath);
                EditorUtility.SetDirty(profile);
            }
            profile.Apply(material); EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
            foreach (var visual in Object.FindObjectsByType<HumanThingsCharacterVisual>(FindObjectsSortMode.None)) visual.Refresh();
            SceneView.RepaintAll();
        }

        public static void ApplyProfileAndBuildPlayer()
        {
            ApplyProfile(); ProjectBuilder.Build();
        }

        [MenuItem("Tools/HumanThings/Character/Capture City Comparison")]
        public static void CaptureCity() => CaptureCity(PrefabPath, ProfilePath, "QA/CharacterVisual");

        public static void CaptureCity(string prefabPath, string profilePath, string output)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var city = CityWorldGenerator.Build(RuntimeCitySettings.Load().Generate(100), scene);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "Ground";
            ground.transform.SetParent(city.root.transform); ground.transform.position = new Vector3(0, -.55f, 0); ground.transform.localScale = new Vector3(650, 1, 650);
            var groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.29f, .32f, .32f) };
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
            var camera = new GameObject("Character city comparison").AddComponent<Camera>();
            camera.fieldOfView = 42; camera.nearClipPlane = .08f; camera.farClipPlane = 650;
            var sun = new GameObject("City sun").AddComponent<Light>(); sun.type = LightType.Directional;
            var atmosphere = city.root.AddComponent<HumanThingsVisualTreatment>();
            atmosphere.Apply(AssetDatabase.LoadAssetAtPath<HumanThingsVisualProfile>(HumanThingsVisualTools.ProfilePath), camera, sun);
            var character = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            var visual = character.GetComponent<HumanThingsCharacterVisual>();
            var buildings = city.chunks.SelectMany(c => c.city.buildings).ToArray();
            var shop = buildings.Where(b => BoundsOf(b.root).size.y > 12).OrderBy(b => b.root.transform.position.sqrMagnitude).First();
            var center = BoundsOf(shop.root).center; center.y = 0;
            var streets = city.chunks.SelectMany(c => c.city.roads.Select(r => (a: r.start + c.node.worldBounds.center, b: r.end + c.node.worldBounds.center)));
            Vector3 Closest(Vector3 a, Vector3 b) => a + (b - a) * Mathf.Clamp01(Vector3.Dot(center - a, b - a) / (b - a).sqrMagnitude);
            var point = streets.Select(r => Closest(r.a, r.b)).OrderBy(p => Vector3.Distance(p, center)).First(); point.y = 0;
            var front = (point - center).normalized;
            Physics.SyncTransforms();
            if (Physics.Raycast(point + Vector3.up * 3, Vector3.down, out var floor, 10)) point.y = floor.point.y;
            character.transform.position = point;
            character.transform.rotation = Quaternion.LookRotation(front);
            try
            {
                foreach (string view in new[] { "Front", "Back", "Close" })
                {
                    character.transform.rotation = Quaternion.LookRotation(view == "Back" ? -front : front);
                    camera.transform.position = character.transform.position + front * (view == "Close" ? 2.05f : 3.95f) + Vector3.up * (view == "Close" ? 1.5f : 1.2f);
                    camera.transform.LookAt(character.transform.position + Vector3.up * (view == "Close" ? 1.35f : .96f));
                    foreach (bool original in new[] { true, false })
                    {
                        visual.CompareOriginal(original);
                        RenderCity(camera, output, (original ? "Original-" : "HumanThings-") + view);
                    }
                }
                File.WriteAllText(output + "/Profile.json", JsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<HumanThingsCharacterVisualProfile>(profilePath), true));
                Debug.Log("CHARACTER_CITY_CAPTURE_PASS seed=100 position=" + point + " fog=" + RenderSettings.fog + " post=" + camera.GetUniversalAdditionalCameraData().renderPostProcessing);
            }
            finally
            {
                atmosphere.Restore(); Object.DestroyImmediate(character); Object.DestroyImmediate(city.root);
                Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(sun.gameObject); Object.DestroyImmediate(groundMaterial);
            }
        }

        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            return bounds;
        }
        static void RenderCity(Camera camera, string output, string name)
        {
            var rt = new RenderTexture(1440, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(1440, 900, TextureFormat.RGB24, false); var old = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; VolumeManager.instance.Update(camera.transform, ~0);
                camera.Render(); camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
                File.WriteAllBytes(output + "/" + name + ".png", image.EncodeToPNG());
            }
            finally { camera.targetTexture = null; RenderTexture.active = old; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image); }
        }

        [MenuItem("Tools/HumanThings/Character/Audit Original")]
        public static void Audit()
        {
            Directory.CreateDirectory("QA/CharacterVisual");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCharacterBuilder.PrefabPath);
            var report = new StringBuilder();
            report.AppendLine("Pipeline: " + GraphicsSettings.currentRenderPipeline);
            report.AppendLine("Color space: " + QualitySettings.activeColorSpace);
            report.AppendLine("Prefab: " + PlayerCharacterBuilder.PrefabPath);
            foreach (var t in source.GetComponentsInChildren<Transform>(true))
                report.AppendLine("Node: " + AnimationUtility.CalculateTransformPath(t, source.transform));
            foreach (var r in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                report.AppendLine($"Skin: {r.name}; vertices={r.sharedMesh.vertexCount}; submeshes={r.sharedMesh.subMeshCount}; bones={r.bones.Length}; slots={r.sharedMaterials.Length}; cast={r.shadowCastingMode}; receive={r.receiveShadows}");
                foreach (var m in r.sharedMaterials)
                {
                    report.AppendLine($"Material: {m.name}; shader={m.shader.name}; path={AssetDatabase.GetAssetPath(m)}; keywords={string.Join(",", m.shaderKeywords)}");
                    for (int i = 0; i < m.shader.GetPropertyCount(); i++)
                    {
                        string key = m.shader.GetPropertyName(i);
                        var type = m.shader.GetPropertyType(i);
                        string value = type switch
                        {
                            ShaderPropertyType.Texture => m.GetTexture(key) == null ? "none" : m.GetTexture(key).name + $" ({m.GetTexture(key).width}x{m.GetTexture(key).height})",
                            ShaderPropertyType.Color => m.GetColor(key).ToString(),
                            ShaderPropertyType.Vector => m.GetVector(key).ToString(),
                            _ => m.GetFloat(key).ToString(System.Globalization.CultureInfo.InvariantCulture)
                        };
                        report.AppendLine(key + " = " + value);
                    }
                }
            }
            File.WriteAllText("QA/CharacterVisual/Audit.txt", report.ToString());
            Debug.Log("CHARACTER_VISUAL_AUDIT\n" + report);
        }
    }

    [CustomEditor(typeof(HumanThingsCharacterVisualProfile))]
    public sealed class HumanThingsCharacterVisualProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("Apply to HumanThings Material"))
                HumanThingsCharacterVisualBuilder.ApplyProfile((HumanThingsCharacterVisualProfile)target);
        }
    }

    [CustomEditor(typeof(HumanThingsCharacterVisual))]
    public sealed class HumanThingsCharacterVisualEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck(); DrawDefaultInspector();
            var visual = (HumanThingsCharacterVisual)target;
            if (EditorGUI.EndChangeCheck() && !EditorUtility.IsPersistent(visual)) visual.Refresh();
            using (new EditorGUI.DisabledScope(EditorUtility.IsPersistent(visual)))
                if (GUILayout.Button(visual.ShowingOriginal ? "Show HumanThings Material" : "Compare Original Material"))
                { visual.CompareOriginal(!visual.ShowingOriginal); SceneView.RepaintAll(); }
        }
    }
}

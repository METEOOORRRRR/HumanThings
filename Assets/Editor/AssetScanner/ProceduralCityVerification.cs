using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EarthRecovery.Editor
{
    public static class ProceduralCityVerification
    {
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch verification only.");
            CityTemplateEditor.CreatePresets();
            var db = AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>("Assets/CityGeneration/CityAssetDatabase.asset");
            if (db == null || db.entries.Count != 335 || db.entries.Any(e => e.prefab == null)) throw new Exception("Database migration failed.");
            var template = AssetDatabase.LoadAssetAtPath<CityLayoutTemplate>("Assets/CityGeneration/Templates/MixedDistrict.asset");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            db = AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>("Assets/CityGeneration/CityAssetDatabase.asset");
            template = AssetDatabase.LoadAssetAtPath<CityLayoutTemplate>("Assets/CityGeneration/Templates/MixedDistrict.asset");
            var generator = new GameObject("Procedural City Generator").AddComponent<RuntimeCityGenerator>();
            generator.database = db; generator.template = template; generator.seed = 1234; generator.generateOnStart = true;
            generator.gameObject.AddComponent<EarthRecovery.Tests.CityPlayerSmokeProbe>();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "Preview Ground";
            ground.transform.position = new Vector3(0, -.3f, 0); ground.transform.localScale = new Vector3(520, .3f, 520);
            const string materialPath = "Assets/CityGeneration/PreviewGround.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.color = new Color(.24f, .28f, .26f); AssetDatabase.CreateAsset(mat, materialPath); }
            ground.GetComponent<Renderer>().sharedMaterial = mat;
            var light = new GameObject("Preview Sun").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.6f, .6f, .6f);
            var camera = new GameObject("Preview Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(140, 190, -160); camera.transform.LookAt(Vector3.zero);
            camera.orthographic = true; camera.orthographicSize = 130; camera.nearClipPlane = .1f; camera.farClipPlane = 1000;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .19f, .21f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            Directory.CreateDirectory("Docs/ProceduralCity");
            foreach (int seed in new[] { 1234, 4321 })
            {
                var plan = ProceduralCityGenerator.Generate(template, seed, db);
                var result = CityTemplateEditor.Build(plan, scene);
                if (result.buildings.Count == 0 || result.root.GetComponentsInChildren<Transform>().Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0))
                    throw new Exception("Empty city or missing scripts.");
                Capture(camera, "Docs/ProceduralCity/Seed" + seed + ".png");
                File.WriteAllText("Docs/ProceduralCity/Seed" + seed + ".json", JsonUtility.ToJson(plan.config, true));
                File.WriteAllText("Docs/ProceduralCity/Seed" + seed + "-validation.txt", "roads=" + result.roads.Count + " lots=" + result.lots.Count + " buildings=" + result.buildings.Count
                    + " vehicles=" + result.vehicles.Count + " props=" + result.props.Count + "\n" + string.Join("\n", result.warnings));
            }
            CityTemplateEditor.Clear(scene);
            const string scenePath = "Assets/Scenes/ProceduralCityPreview.unity";
            EditorSceneManager.SaveScene(scene, scenePath); AssetDatabase.SaveAssets();
            // Standalone runtime verification uses the normal Latest player with --city-smoke.
            Debug.Log("LOCAL_CITY_VERIFICATION_PASS playerBuild=false database=335 missingScripts=0 screenshots=2");
        }
        static void Capture(Camera camera, string path)
        {
            var rt = new RenderTexture(1280, 960, 24); var image = new Texture2D(1280, 960, TextureFormat.RGB24, false);
            var old = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 1280, 960), 0, 0); image.Apply();
                var pixels = image.GetPixels32(); int colors = pixels.Where((p, i) => i % 101 == 0).Select(p => (p.r << 16) | (p.g << 8) | p.b).Distinct().Count();
                if (colors < 50) throw new Exception("Blank city render: " + colors);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { camera.targetTexture = null; RenderTexture.active = old; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image); }
        }
    }
}

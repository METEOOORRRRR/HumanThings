using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EarthRecovery.Editor
{
    public static class CitySeedComparison
    {
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch capture only.");
            const string databasePath = "Assets/CityGeneration/CityAssetDatabase.asset";
            const string templatePath = "Assets/CityGeneration/AutoTemplates/MarketBranches.asset";
            const string folder = "Docs/SeedComparison/MarketBranches";
            CityTemplateAutoBuilder.Build(AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>(databasePath), rebuild: true);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var db = AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>(databasePath);
            var template = AssetDatabase.LoadAssetAtPath<CityLayoutTemplate>(templatePath);
            if (db == null || template == null) throw new Exception("Missing capture inputs.");
            Directory.CreateDirectory(folder);
            File.WriteAllText(folder + "/Template.json", JsonUtility.ToJson(template, true));
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Comparison Ground";
            ground.transform.position = new Vector3(0, -.3f, 0);
            ground.transform.localScale = new Vector3(520, .3f, 520);
            ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/CityGeneration/PreviewGround.mat");
            var sun = new GameObject("Comparison Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 2;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.6f, .6f, .6f);
            var camera = new GameObject("Comparison Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true;
            camera.nearClipPlane = .1f; camera.farClipPlane = 1000;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.16f, .19f, .21f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var report = new StringBuilder("seed,mapWidth,mapDepth,roads,lots,buildings,vehicles,props,warnings\n");
            foreach (var seed in new[] { 101, 202, 303, 404, 505 })
            {
                CitySceneBuilder.Clear(scene);
                var plan = ProceduralCityGenerator.Generate(template, seed, db);
                var result = CitySceneBuilder.Build(plan, scene);
                if (result.buildings.Count == 0 || result.root.GetComponentsInChildren<Transform>().Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0))
                    throw new Exception("Empty city or missing scripts: " + seed);
                camera.transform.position = new Vector3(140, 190, -160);
                camera.transform.LookAt(Vector3.zero); camera.orthographicSize = 90;
                Capture(camera, folder + "/Seed" + seed + "-Perspective.png");
                camera.transform.position = new Vector3(0, 220, 0);
                camera.transform.rotation = Quaternion.Euler(90, 0, 0); camera.orthographicSize = 77;
                Capture(camera, folder + "/Seed" + seed + "-Top.png");
                File.WriteAllText(folder + "/Seed" + seed + ".json", JsonUtility.ToJson(plan.config, true));
                File.WriteAllText(folder + "/Seed" + seed + "-warnings.txt", string.Join("\n", result.warnings));
                report.AppendLine(FormattableString.Invariant($"{seed},{plan.config.mapWidth},{plan.config.mapDepth},{result.roads.Count},{result.lots.Count},{result.buildings.Count},{result.vehicles.Count},{result.props.Count},{result.warnings.Count}"));
                Debug.Log("SEED_CAPTURE_DONE " + seed);
            }
            File.WriteAllText(folder + "/Summary.csv", report.ToString());
            Debug.Log("SEED_COMPARISON_PASS screenshots=10 template=" + templatePath);
        }

        private static void Capture(Camera camera, string path)
        {
            var rt = new RenderTexture(1440, 1080, 24);
            var image = new Texture2D(1440, 1080, TextureFormat.RGB24, false);
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 1440, 1080), 0, 0); image.Apply();
                var colors = image.GetPixels32().Where((p, i) => i % 101 == 0).Select(p => (p.r << 16) | (p.g << 8) | p.b).Distinct().Count();
                if (colors < 50) throw new Exception("Blank render: " + path);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}

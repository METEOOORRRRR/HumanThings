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
    public static class CityWorldVerification
    {
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch verification only.");
            var template = CityWorldGeneratorWindow.CreateStandard();
            var database = AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>("Assets/CityGeneration/CityAssetDatabase.asset");
            var report = CityWorldValidation.Run(template, database);
            Directory.CreateDirectory("Docs/CityWorld");
            File.WriteAllText("Docs/CityWorld/Validation.json", JsonUtility.ToJson(report, true));
            File.WriteAllLines("Docs/CityWorld/Validation.csv", new[] { "seed,passed,chunks,connectors,connected,buildingsRequested,buildingsPlaced,emptyChunks,roadFailures,overlaps" }
                .Concat(report.seeds.Select(s => $"{s.seed},{s.passed},{s.generatedChunks},{s.validConnectorPairs},{s.reachedChunks},{s.requestedBuildings},{s.placedBuildings},{s.emptyChunks},{s.roadFailures},{s.overlapPairs}")));
            if (!report.passed) throw new Exception("World validation failed: " + string.Join("; ", report.seeds.SelectMany(s => s.errors.Select(e => s.seed + ": " + e)).Take(20)));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            template = AssetDatabase.LoadAssetAtPath<CityWorldTemplate>(CityWorldGeneratorWindow.StandardPath);
            database = AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>("Assets/CityGeneration/CityAssetDatabase.asset");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "World Preview Ground"; ground.transform.position = new Vector3(0, -.3f, 0);
            ground.transform.localScale = new Vector3(1000, .3f, 1000);
            ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/CityGeneration/PreviewGround.mat");
            var sun = new GameObject("Preview Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 2;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.6f, .6f, .6f);
            var camera = new GameObject("Preview Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .19f, .21f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            foreach (int seed in new[] { 1234, 5678 })
            {
                CityWorldGenerator.Clear(scene);
                var plan = CityWorldGenerator.Generate(template, seed, database);
                var world = CityWorldGenerator.Build(plan, scene);
                if (world.root.GetComponentsInChildren<Transform>().Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0))
                    throw new Exception("Missing scripts in generated world.");
                camera.transform.position = new Vector3(0, 800, 0); camera.transform.rotation = Quaternion.Euler(90, 0, 0); camera.orthographicSize = 325;
                Capture(camera, "Docs/CityWorld/Seed" + seed + "-Top.png");
                camera.transform.position = new Vector3(550, 750, -650); camera.transform.LookAt(Vector3.zero); camera.orthographicSize = 370;
                Capture(camera, "Docs/CityWorld/Seed" + seed + "-Perspective.png");
                Debug.Log("WORLD_RENDER_PASS seed=" + seed + " chunks=" + world.chunks.Count + " locations=" + world.locations.Count);
            }
            Debug.Log("CITY_WORLD_VERIFICATION_PASS worlds=10 renders=4");
        }
        static void Capture(Camera camera, string path)
        {
            var rt = new RenderTexture(1800, 1600, 24); var texture = new Texture2D(1800, 1600, TextureFormat.RGB24, false);
            var old = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1800, 1600), 0, 0); texture.Apply();
                if (texture.GetPixels32().Where((p, i) => i % 101 == 0).Select(p => (p.r << 16) | (p.g << 8) | p.b).Distinct().Count() < 50)
                    throw new Exception("Blank world capture.");
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { camera.targetTexture = null; RenderTexture.active = old; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}

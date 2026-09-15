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
    public static class SpecialPOIVerification
    {
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch verification only.");
            CityWorldGeneratorWindow.CreateStandard();
            Directory.CreateDirectory("Docs/SpecialPOIs");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var t = AssetDatabase.LoadAssetAtPath<CityWorldTemplate>(CityWorldGeneratorWindow.StandardPath);
            var db = AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>("Assets/CityGeneration/CityAssetDatabase.asset");
            var report = new CityWorldValidationReport();
            foreach (int seed in new[] { 100, 200, 1234, 5678, 16847, 24766, 32685, 40604, 48523, 56442 })
            {
                try { report.seeds.Add(CityWorldValidation.Validate(CityWorldGenerator.Generate(t, seed, db))); }
                catch (Exception e) { report.seeds.Add(new CityWorldSeedReport { seed = seed, errors = new() { e.Message } }); }
            }
            report.passed = report.seeds.All(s => s.passed);
            File.WriteAllText("Docs/SpecialPOIs/Validation.json", JsonUtility.ToJson(report, true));
            if (!report.passed) throw new InvalidOperationException(string.Join("\n", report.seeds.SelectMany(s => s.errors.Select(e => s.seed + ": " + e))));
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "Preview Ground";
            ground.transform.position = new Vector3(0, -.3f, 0); ground.transform.localScale = new Vector3(1600, .3f, 1600);
            ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/CityGeneration/PreviewGround.mat");
            var light = new GameObject("Preview Light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2; light.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.6f, .6f, .6f);
            var camera = new GameObject("Preview Camera").AddComponent<Camera>(); camera.orthographic = true; camera.farClipPlane = 2000;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            foreach (int seed in new[] { 100, 200 })
            {
                CityWorldGenerator.Clear(scene); var plan = CityWorldGenerator.Generate(t, seed, db); var world = CityWorldGenerator.Build(plan, scene);
                File.WriteAllLines("Docs/SpecialPOIs/Seed" + seed + ".csv", new[] { "id,name,chunk,x,y,z,prefab" }.Concat(world.specialPOIs.OrderBy(p => p.id)
                    .Select(p => FormattableString.Invariant($"{p.id},{p.displayName},{p.chunk.Id},{p.bounds.center.x},{p.bounds.center.y},{p.bounds.center.z},{AssetDatabase.GetAssetPath(p.definition.prefab)}"))));
                camera.transform.position = new Vector3(0, 800, 0); camera.transform.rotation = Quaternion.Euler(90, 0, 0); camera.orthographicSize = 325;
                Capture(camera, "Docs/SpecialPOIs/Seed" + seed + "-Top.png");
                var poi = world.specialPOIs.Single(p => p.id == "CUL_SCHOOL");
                var front = poi.instance.transform.rotation * poi.definition.localForward;
                camera.transform.position = poi.bounds.center + front * 24 + Vector3.up * 13; camera.transform.LookAt(poi.bounds.center); camera.orthographicSize = 10;
                Capture(camera, "Docs/SpecialPOIs/Seed" + seed + "-Label.png");
            }
            Debug.Log("SPECIAL_POI_VERIFICATION_PASS worlds=10 requiredPOIs=150 screenshots=4");
        }
        static void Capture(Camera camera, string path)
        {
            var rt = new RenderTexture(1600, 1400, 24); var tex = new Texture2D(1600, 1400, TextureFormat.RGB24, false); var old = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1600, 1400), 0, 0); tex.Apply();
                if (tex.GetPixels32().Where((p, i) => i % 101 == 0).Select(p => (p.r << 16) | (p.g << 8) | p.b).Distinct().Count() < 50) throw new Exception("Blank render.");
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally { camera.targetTexture = null; RenderTexture.active = old; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); }
        }
    }
}

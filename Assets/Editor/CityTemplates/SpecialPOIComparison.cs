using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EarthRecovery.Editor
{
    [Serializable] public sealed class POIComparisonItem
    {
        public int number;
        public string id, name, chunk, prefab, image;
        public Vector3 position, viewport;
        public bool fixedPrefab, labelCorrect;
    }
    [Serializable] public sealed class POIComparisonChunk
    {
        public string id;
        public int count;
        public Vector3 min, max;
    }
    [Serializable] public sealed class POIComparisonSeed
    {
        public int seed, ordinaryBuildingOverlaps;
        public bool passed, campCentered;
        public Vector3 campPosition;
        public List<POIComparisonItem> pois = new();
        public List<POIComparisonChunk> chunks = new();
        public List<string> errors = new();
    }
    [Serializable] public sealed class POIComparisonReport
    {
        public bool passed, allLocationsMove, repeatSeedIdentical;
        public List<POIComparisonSeed> seeds = new();
    }

    public static class SpecialPOIComparison
    {
        const string Folder = "Docs/POIComparison";
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch capture only.");
            Directory.CreateDirectory(Folder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var template = AssetDatabase.LoadAssetAtPath<CityWorldTemplate>(CityWorldGeneratorWindow.StandardPath);
            var db = AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>("Assets/CityGeneration/CityAssetDatabase.asset");
            string before = JsonUtility.ToJson(template);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "Comparison Ground";
            ground.transform.position = new Vector3(0, -.3f, 0); ground.transform.localScale = new Vector3(1600, .3f, 1600);
            ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/CityGeneration/PreviewGround.mat");
            var sun = new GameObject("Comparison Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 2;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.6f, .6f, .6f);
            var camera = new GameObject("Comparison Camera").AddComponent<Camera>(); camera.orthographic = true; camera.farClipPlane = 2000;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var report = new POIComparisonReport();
            string signature100 = null;
            foreach (int seed in new[] { 100, 200, 300, 400, 500 })
            {
                var entry = new POIComparisonSeed { seed = seed }; report.seeds.Add(entry);
                CityWorldGenerator.Clear(scene);
                var plan = CityWorldGenerator.Generate(template, seed, db);
                entry.errors.AddRange(CityWorldValidation.Validate(plan).errors);
                var world = CityWorldGenerator.Build(plan, scene, (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent));
                POIDebugLabel.SetVisible(world.root, true);
                entry.errors.AddRange(SpecialPOIValidation.ValidateInstances(world));
                entry.campPosition = world.baseCampInstance.transform.position;
                entry.campCentered = SpecialPOIPlacementSolver.DistanceXZ(entry.campPosition, world.worldBounds.center) < .001f;
                if (!entry.campCentered) entry.errors.Add("Base Camp is not at world center.");
                if (world.root.transform.Find("SpecialPOIs").childCount != 15) entry.errors.Add("SpecialPOIs hierarchy must contain exactly 15 instances.");
                SetTop(camera);
                foreach (var chunk in world.chunks)
                    entry.chunks.Add(new POIComparisonChunk { id = chunk.node.Id, count = world.specialPOIs.Count(p => p.chunk.gridPosition == chunk.node.gridPosition),
                        min = camera.WorldToViewportPoint(chunk.node.worldBounds.min), max = camera.WorldToViewportPoint(chunk.node.worldBounds.max) });
                for (int i = 0; i < SpecialPOIDatabase.RequiredIds.Length; i++)
                {
                    string id = SpecialPOIDatabase.RequiredIds[i];
                    var poi = world.specialPOIs.Single(p => p.id == id);
                    var label = poi.instance.GetComponentInChildren<POIDebugLabel>();
                    var text = label == null ? null : label.GetComponent<TextMeshPro>();
                    Vector3 front = (poi.instance.transform.rotation * poi.definition.localForward).normalized;
                    float extent = Mathf.Abs(front.x) * poi.bounds.extents.x + Mathf.Abs(front.z) * poi.bounds.extents.z;
                    bool labelOK = text != null && text.text == "[" + poi.displayName + "]" && text.textInfo.characterCount > 0
                        && text.font.HasCharacters(text.text) && Vector3.Dot(label.transform.position - poi.bounds.center, front) > extent
                        && Vector3.Dot(-label.transform.forward, front) > .999f && label.transform.position.y > poi.bounds.min.y + 2;
                    bool prefabOK = PrefabUtility.GetCorrespondingObjectFromSource(poi.instance) == poi.definition.prefab;
                    entry.pois.Add(new POIComparisonItem { number = i + 1, id = id, name = poi.displayName, chunk = poi.chunk.Id,
                        position = poi.instance.transform.position, viewport = camera.WorldToViewportPoint(poi.bounds.center),
                        prefab = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(poi.instance)),
                        image = "Seed" + seed + "-" + id + ".png", fixedPrefab = prefabOK, labelCorrect = labelOK });
                    if (!prefabOK || !labelOK) entry.errors.Add(id + ": prefab=" + prefabOK + ", label=" + labelOK);
                    var bounds = ExteriorBounds(poi.instance);
                    foreach (var building in world.chunks.SelectMany(c => c.city.buildings))
                        if (Overlaps(bounds, ExteriorBounds(building.root)))
                        { entry.ordinaryBuildingOverlaps++; entry.errors.Add("Renderer bounds overlap: " + id + " / " + building.id); }
                }
                Capture(camera, Folder + "/Seed" + seed + "-Map.png", 1600, 1400);
                foreach (var item in entry.pois)
                {
                    var poi = world.specialPOIs.Single(p => p.id == item.id);
                    Vector3 front = poi.instance.transform.rotation * poi.definition.localForward;
                    camera.transform.position = poi.bounds.center + front * 24 + Vector3.up * 10;
                    camera.transform.LookAt(poi.bounds.center); camera.orthographicSize = 8.5f;
                    Capture(camera, Folder + "/" + item.image, 960, 720);
                }
                string signature = Signature(entry.pois);
                if (seed == 100) signature100 = signature;
                entry.passed = entry.errors.Count == 0;
                Debug.Log("POI_COMPARISON_SEED " + seed + " passed=" + entry.passed + " POIs=" + entry.pois.Count);
            }
            report.allLocationsMove = SpecialPOIDatabase.RequiredIds.All(id => report.seeds.Select(s => s.pois.Single(p => p.id == id).position).Distinct().Count() == 5);
            var repeat = CityWorldGenerator.Generate(template, 100, db);
            var repeated = repeat.specialPOIs.Select(p => new POIComparisonItem { id = p.definition.id, chunk = p.chunk.Id, position = p.position,
                prefab = AssetDatabase.GetAssetPath(p.definition.prefab) }).ToList();
            report.repeatSeedIdentical = signature100 == Signature(repeated);
            report.passed = report.seeds.All(s => s.passed) && report.allLocationsMove && report.repeatSeedIdentical && before == JsonUtility.ToJson(template);
            File.WriteAllText(Folder + "/Report.json", JsonUtility.ToJson(report, true));
            File.WriteAllText(Folder + "/report-data.js", "window.POI_REPORT = " + JsonUtility.ToJson(report) + ";");
            if (!report.passed) throw new Exception("POI comparison failed: " + string.Join("; ", report.seeds.SelectMany(s => s.errors)));
            Debug.Log("POI_COMPARISON_PASS actualWorlds=5 actualPOIs=75 captures=80 seed100Repeat=true");
        }
        static void SetTop(Camera c)
        { c.transform.position = new Vector3(0, 800, 0); c.transform.rotation = Quaternion.Euler(90, 0, 0); c.orthographicSize = 325; c.aspect = 1600f / 1400; }
        static string Signature(List<POIComparisonItem> list) => string.Join("|", list.OrderBy(p => p.id).Select(p => p.id + p.chunk + JsonUtility.ToJson(p.position) + p.prefab));
        static bool Overlaps(Bounds a, Bounds b) => a.min.x < b.max.x - .01f && a.max.x > b.min.x + .01f && a.min.z < b.max.z - .01f && a.max.z > b.min.z + .01f;
        static Bounds ExteriorBounds(GameObject root)
        {
            var all = root.GetComponentsInChildren<Renderer>().Where(r => r.GetComponentInParent<POIDebugLabel>() == null).ToArray();
            if (all.Length == 0) throw new Exception("No exterior renderer: " + root.name);
            Bounds b = all[0].bounds; foreach (var r in all.Skip(1)) b.Encapsulate(r.bounds); return b;
        }
        static void Capture(Camera camera, string path, int width, int height)
        {
            var target = new RenderTexture(width, height, 24); var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var old = RenderTexture.active;
            try
            {
                camera.aspect = (float)width / height; camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                if (texture.GetPixels32().Where((p, i) => i % 101 == 0).Select(p => (p.r << 16) | (p.g << 8) | p.b).Distinct().Count() < 50) throw new Exception("Blank capture.");
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { camera.targetTexture = null; RenderTexture.active = old; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}

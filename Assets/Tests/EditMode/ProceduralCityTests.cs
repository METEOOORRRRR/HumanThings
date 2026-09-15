using System;
using System.Collections.Generic;
using System.Linq;
using EarthRecovery.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EarthRecovery.Tests
{
    public sealed class ProceduralCityTests
    {
        static HumanThingsAssetDatabase Database() => AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>("Assets/CityGeneration/CityAssetDatabase.asset");
        static string Signature(ProceduralCityPlan p) => JsonUtility.ToJson(p.config) + string.Join("|", p.roads.Select(r => r.id + r.start.ToString("F5") + r.end.ToString("F5")))
            + string.Join("|", p.placement.placements.Select(v => v.asset.assetGuid + v.position.ToString("F5") + v.rotation.ToString("F5") + v.scale.ToString("F5")));

        [TestCase(CityTemplatePreset.DenseCommercial)] [TestCase(CityTemplatePreset.MixedDistrict)]
        [TestCase(CityTemplatePreset.ResidentialAlleys)] [TestCase(CityTemplatePreset.SubwayHub)] [TestCase(CityTemplatePreset.WideAvenue)]
        public void PresetsAreDeterministicConnectedAndDoNotOverlap(CityTemplatePreset preset)
        {
            var template = ScriptableObject.CreateInstance<CityLayoutTemplate>(); template.ApplyPreset(preset);
            try
            {
                var signatures = new HashSet<string>();
                for (int seed = 0; seed < 12; seed++)
                {
                    var p = ProceduralCityGenerator.Generate(template, seed, Database());
                    Assert.That(Signature(ProceduralCityGenerator.Generate(template, seed, Database())), Is.EqualTo(Signature(p)));
                    signatures.Add(string.Join("|", p.roads.Select(r => r.start.ToString("F4") + r.end.ToString("F4"))));
                    Assert.That(p.placement.warnings.Any(w => w.StartsWith("INVALID:")), Is.False, string.Join("\n", p.placement.warnings));
                    Assert.That(p.lots.Count, Is.GreaterThan(0));
                    var surface = p.placement.placements.Where(v => v.surface).ToArray();
                    for (int i = 0; i < surface.Length; i++) for (int j = i + 1; j < surface.Length; j++)
                        Assert.That(HumanThingsPlacementSolver.Overlaps(surface[i].footprint, surface[j].footprint, -.01f), Is.False, "Overlapping surfaces");
                    foreach (var lot in p.lots)
                    {
                        foreach (var road in p.roads) Assert.That(HumanThingsPlacementSolver.Overlaps(lot.zone.Bounds, road.bounds), Is.False);
                        var access = p.roads.Single(r => r.id == lot.roadId).bounds;
                        if (lot.zone.roadDirection.x != 0)
                        { Assert.That(lot.zone.Bounds.min.z, Is.GreaterThanOrEqualTo(access.min.z)); Assert.That(lot.zone.Bounds.max.z, Is.LessThanOrEqualTo(access.max.z)); }
                        else
                        { Assert.That(lot.zone.Bounds.min.x, Is.GreaterThanOrEqualTo(access.min.x)); Assert.That(lot.zone.Bounds.max.x, Is.LessThanOrEqualTo(access.max.x)); }
                    }
                }
                Assert.That(signatures.Count, Is.EqualTo(12));
            }
            finally { UnityEngine.Object.DestroyImmediate(template); }
        }

        [Test] public void SpatialStructureChangesEvenWhenCountsAndDimensionsAreFixed()
        {
            var t = ScriptableObject.CreateInstance<CityLayoutTemplate>();
            try
            {
                t.map.width = new(180, 180); t.map.depth = new(180, 180); t.roads.mainRoadCount = new(1, 1);
                t.roads.sideRoadCount = new(3, 3); t.roads.alleyCount = new(0, 0); t.roads.intersectionCount = new(2, 2); t.roads.width = new(12, 12);
                var signatures = Enumerable.Range(0, 15).Select(seed => RoadNetworkGenerator.Generate(CityGenerationConfigBuilder.Build(t, seed)))
                    .Select(roads => string.Join("|", roads.Select(r => r.start.ToString("F4") + r.end.ToString("F4")))).ToArray();
                Assert.That(signatures.Distinct().Count(), Is.EqualTo(15));
            }
            finally { UnityEngine.Object.DestroyImmediate(t); }
        }

        [Test] public void RangesValidateAndSamplingDoesNotChangeUnityRandomState()
        {
            Assert.Throws<ArgumentException>(() => new IntRange(5, 2).Sample(new System.Random(1)));
            Assert.Throws<ArgumentException>(() => new FloatRange(float.NaN, 2).Sample(new System.Random(1)));
            var a = new System.Random(12); var b = new System.Random(12);
            for (int i = 0; i < 100; i++) Assert.That(new IntRange(int.MinValue, int.MaxValue).Sample(a), Is.EqualTo(new IntRange(int.MinValue, int.MaxValue).Sample(b)));
            var t = ScriptableObject.CreateInstance<CityLayoutTemplate>(); var state = UnityEngine.Random.state;
            try
            {
                var before = JsonUtility.ToJson(state); ProceduralCityGenerator.Generate(t, 123, Database());
                Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(before));
                t.map.width = new(5, 5); Assert.That(CityGenerationConfigBuilder.Build(t, 1).mapWidth, Is.EqualTo(100));
                t.buildings.commercialRatio = t.buildings.residentialRatio = t.buildings.officeRatio = t.buildings.publicRatio = new(0, 0);
                Assert.Throws<ArgumentException>(() => CityGenerationConfigBuilder.Build(t, 1));
            }
            finally { UnityEngine.Random.state = state; UnityEngine.Object.DestroyImmediate(t); }
        }

        [Test] public void DatabaseMigrationPreservesReferencesAndMissingAssetsWarn()
        {
            var db = Database(); Assert.That(db, Is.Not.Null); Assert.That(db.entries.Count, Is.EqualTo(335));
            Assert.That(db.entries.All(e => e.prefab != null && !string.IsNullOrEmpty(e.assetGuid)), Is.True);
            Assert.That(typeof(HumanThingsAssetDatabase).Assembly.GetName().Name, Is.EqualTo("EarthRecovery"));
            var t = ScriptableObject.CreateInstance<CityLayoutTemplate>();
            var empty = ScriptableObject.CreateInstance<HumanThingsAssetDatabase>();
            try
            {
                Assert.Throws<InvalidOperationException>(() => ProceduralCityGenerator.Generate(t, 1, empty));
                t.ApplyPreset(CityTemplatePreset.ResidentialAlleys);
                var p = ProceduralCityGenerator.Generate(t, 1, db);
                Assert.That(p.placement.warnings.Any(w => w.Contains("missing assets")), Is.True);
                Assert.That(p.placement.placements.Where(v => v.group == "Buildings").All(v =>
                    p.lots.Single(l => l.id == v.zoneId).usage != BuildingUsage.Residential), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(t); UnityEngine.Object.DestroyImmediate(empty); }
        }

        [Test] public void SceneBuildQueriesRegenerateClearAndUndoPreserveUnownedObjects()
        {
            var scene = EditorSceneManager.NewPreviewScene(); var t = ScriptableObject.CreateInstance<CityLayoutTemplate>();
            var sentinel = new GameObject(CitySceneBuilder.RootName); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(sentinel, scene);
            try
            {
                var p = ProceduralCityGenerator.Generate(t, 1234, Database());
                var r = CityTemplateEditor.Build(p, scene);
                Assert.That(r.root.transform.childCount, Is.EqualTo(6)); Assert.That(r.GetGeneratedLocations().Count, Is.GreaterThan(r.roads.Count));
                Assert.That(r.GetLocationsByTag("Road").Count(), Is.EqualTo(r.roads.Count));
                Assert.That(r.GetRandomLocationByType(LocationType.Street, new System.Random(7)).id,
                    Is.EqualTo(r.GetRandomLocationByType(LocationType.Street, new System.Random(7)).id));
                Assert.That(r.GetLocationsByType(LocationType.Subway).All(l => l.root != null), Is.True);
                foreach (var building in r.buildings)
                {
                    var renderers = building.root.GetComponentsInChildren<Renderer>();
                    foreach (var renderer in renderers)
                    {
                        var expected = building.bounds; expected.Expand(.03f);
                        Assert.That(expected.Contains(renderer.bounds.min) && expected.Contains(renderer.bounds.max), Is.True, building.assetGuid);
                    }
                }
                CityTemplateEditor.Build(ProceduralCityGenerator.Generate(t, 1235, Database()), scene);
                Assert.That(CitySceneBuilder.FindGenerated(scene).Length, Is.EqualTo(1)); Assert.That(sentinel != null, Is.True);
                CityTemplateEditor.Clear(scene); Assert.That(CitySceneBuilder.FindGenerated(scene).Length, Is.Zero); Assert.That(sentinel != null, Is.True);
                Undo.PerformUndo(); Assert.That(CitySceneBuilder.FindGenerated(scene).Length, Is.EqualTo(1));
            }
            finally { Undo.ClearAll(); EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(t); }
        }

        [Test] public void RuntimeBuilderFailureRetainsPreviousRootAndCleansPartialRoot()
        {
            var scene = EditorSceneManager.NewPreviewScene(); var t = ScriptableObject.CreateInstance<CityLayoutTemplate>();
            try
            {
                var p = ProceduralCityGenerator.Generate(t, 1, Database()); var before = CitySceneBuilder.Build(p, scene);
                Assert.Throws<InvalidOperationException>(() => CitySceneBuilder.Build(p, scene, (prefab, parent) => throw new InvalidOperationException("fixture")));
                Assert.That(CitySceneBuilder.FindGenerated(scene).Length, Is.EqualTo(1)); Assert.That(before.root != null, Is.True);
                Assert.That(before.roads.All(r => r.root != null), Is.True);
                CitySceneBuilder.Clear(scene); Assert.That(CitySceneBuilder.FindGenerated(scene).Length, Is.Zero);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(t); }
        }

        [Test] public void WindowAndFiveEditablePresetsExist()
        {
            CityTemplateEditor.CreatePresets();
            foreach (CityTemplatePreset preset in Enum.GetValues(typeof(CityTemplatePreset)))
                Assert.That(AssetDatabase.LoadAssetAtPath<CityLayoutTemplate>("Assets/CityGeneration/Templates/" + preset + ".asset"), Is.Not.Null);
            var window = ScriptableObject.CreateInstance<HumanThingsCityBlockGeneratorWindow>();
            Assert.That(window, Is.Not.Null); UnityEngine.Object.DestroyImmediate(window);
        }
    }
}

using System;
using System.Linq;
using EarthRecovery.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EarthRecovery.Tests
{
    public sealed class SpecialPOITests
    {
        static CityWorldTemplate Template() => AssetDatabase.LoadAssetAtPath<CityWorldTemplate>(CityWorldGeneratorWindow.StandardPath);
        static HumanThingsAssetDatabase Db() => AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>("Assets/CityGeneration/CityAssetDatabase.asset");
        static string Signature(CityWorldPlan p) => string.Join("|", p.specialPOIs.OrderBy(x => x.definition.id).Select(x => x.definition.id + ":" + x.chunk.Id + ":" + x.lotId
            + JsonUtility.ToJson(x.position) + JsonUtility.ToJson(x.rotation)));

        [Test] public void DefinitionsAreExactlyFifteenFixedVisualOnlyPrefabsAndOriginalsArePreserved()
        {
            var db = Template().specialPOIDatabase; Assert.DoesNotThrow(db.Validate);
            Assert.That(db.definitions.Count, Is.EqualTo(15)); Assert.That(db.definitions.Select(d => d.prefab).Distinct().Count(), Is.EqualTo(15));
            foreach (var d in db.definitions)
            {
                Assert.That(d.prefab.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty, d.id);
                Assert.That(d.prefab.transform.Find("Exterior"), Is.Not.Null);
                var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HumanThings/Prefabs/" + d.id + ".prefab");
                Assert.That(original.GetComponent<LocationController>(), Is.Not.Null);
                Assert.That(original, Is.Not.SameAs(d.prefab));
                Assert.That(SpecialPOIAssetBuilder.Measure(d.prefab).size, Is.EqualTo(d.boundsSize));
            }
        }

        [Test] public void TenSeedsAlwaysPlaceFifteenBeforeOrdinaryBuildings()
        {
            foreach (int seed in new[] { 100, 200, 1234, 5678, 16847, 24766, 32685, 40604, 48523, 56442 })
            {
                var p = CityWorldGenerator.Generate(Template(), seed, Db()); var errors = SpecialPOIValidation.ValidatePlan(p);
                Assert.That(errors, Is.Empty, seed + ": " + string.Join("; ", errors)); Assert.That(p.specialPOIs.Count, Is.EqualTo(15));
                Assert.That(p.specialPOIs.GroupBy(x => x.chunk.gridPosition).Max(g => g.Count()), Is.LessThanOrEqualTo(2));
                Assert.That(p.specialPOIs.Select(x => x.chunk.gridPosition).Distinct().Count(), Is.GreaterThanOrEqualTo(8));
                foreach (var poi in p.specialPOIs)
                {
                    var chunk = p.chunkPlans[poi.chunk.gridPosition];
                    Assert.That(chunk.lots.Single(l => l.id == poi.lotId).occupiedBySpecialPOI, Is.True);
                    Assert.That(chunk.placement.placements.Any(v => !v.surface && v.zoneId == poi.lotId), Is.False);
                }
            }
        }

        [Test] public void Seed100RepeatsSeed200MovesAndAppearanceNeverChanges()
        {
            var t = Template(); string before = JsonUtility.ToJson(t.specialPOIDatabase);
            var defsBefore = t.specialPOIDatabase.definitions.Select(JsonUtility.ToJson).ToArray();
            var random = UnityEngine.Random.state;
            var a = CityWorldGenerator.Generate(t, 100, Db()); var b = CityWorldGenerator.Generate(t, 200, Db()); var again = CityWorldGenerator.Generate(t, 100, Db());
            Assert.That(Signature(a), Is.EqualTo(Signature(again))); Assert.That(Signature(a), Is.Not.EqualTo(Signature(b)));
            foreach (var poi in a.specialPOIs)
            {
                var other = b.specialPOIs.Single(x => x.definition.id == poi.definition.id);
                Assert.That(poi.definition.prefab, Is.SameAs(other.definition.prefab)); Assert.That(poi.position, Is.Not.EqualTo(other.position));
            }
            Assert.That(UnityEngine.Random.state, Is.EqualTo(random)); Assert.That(JsonUtility.ToJson(t.specialPOIDatabase), Is.EqualTo(before));
            Assert.That(t.specialPOIDatabase.definitions.Select(JsonUtility.ToJson), Is.EqualTo(defsBefore));
        }

        [Test] public void MissingDuplicateExtraDisabledOrSharedPrefabDefinitionsFail()
        {
            var db = UnityEngine.Object.Instantiate(Template().specialPOIDatabase);
            var first = UnityEngine.Object.Instantiate(db.definitions[0]);
            try
            {
                var original = db.definitions[0]; db.definitions[0] = first;
                first.prefab = null; Assert.Throws<ArgumentException>(db.Validate); first.prefab = original.prefab;
                first.placementEnabled = false; Assert.Throws<ArgumentException>(db.Validate); first.placementEnabled = true;
                first.id = db.definitions[1].id; Assert.Throws<ArgumentException>(db.Validate); first.id = original.id;
                first.prefab = db.definitions[1].prefab; Assert.Throws<ArgumentException>(db.Validate); first.prefab = original.prefab;
                db.definitions.RemoveAt(14); Assert.Throws<ArgumentException>(db.Validate);
                db.definitions.Add(original); db.definitions.Add(original); Assert.Throws<ArgumentException>(db.Validate);
            }
            finally { UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(db); }
        }

        [Test] public void ImpossibleDistancesFailAfterBoundedRetriesAndCancelIsHonored()
        {
            var t = UnityEngine.Object.Instantiate(Template()); var db = UnityEngine.Object.Instantiate(t.specialPOIDatabase);
            var copies = db.definitions.Select(UnityEngine.Object.Instantiate).ToList(); db.definitions = copies; t.specialPOIDatabase = db;
            try
            {
                foreach (var d in copies) d.minDistanceFromOtherPOI = 100000;
                t.specialPOISettings = new SpecialPOISettings { maxRetries = 10, searchBudgetPerRetry = 100, showDebugLocationLabels = false };
                var e = Assert.Throws<InvalidOperationException>(() => CityWorldGenerator.Generate(t, 100, Db()));
                Assert.That(e.Message, Does.Contain("10 bounded retries"));
                foreach (var d in copies) d.minDistanceFromOtherPOI = 0;
                Assert.Throws<OperationCanceledException>(() => CityWorldGenerator.Generate(t, 100, Db(), (_, text) => text.Contains("Reserving")));
                copies[0].footprint = new Vector2(10000, 10000);
                Assert.That(Assert.Throws<InvalidOperationException>(() => CityWorldGenerator.Generate(t, 100, Db())).Message, Does.Contain(copies[0].id));
            }
            finally { foreach (var d in copies) UnityEngine.Object.DestroyImmediate(d); UnityEngine.Object.DestroyImmediate(db); UnityEngine.Object.DestroyImmediate(t); }
        }

        [Test] public void InstancesLabelsQueriesAndUnloadReloadRemainConsistent()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var p = CityWorldGenerator.Generate(Template(), 100, Db()); var world = CityWorldGenerator.Build(p, scene);
                Assert.That(SpecialPOIValidation.ValidateInstances(world), Is.Empty);
                Assert.That(world.GetLocationsByType(LocationType.SpecialPOI).Count(), Is.EqualTo(15));
                Assert.That(world.GetLocationsByTag("special_poi").Count(), Is.EqualTo(15));
                Assert.That(world.baseCampBounds.center.x, Is.EqualTo(world.worldBounds.center.x));
                Assert.That(world.baseCampBounds.center.z, Is.EqualTo(world.worldBounds.center.z));
                Assert.That(world.root.GetComponentsInChildren<POIDebugLabel>().Length, Is.EqualTo(16));
                foreach (var label in world.root.GetComponentsInChildren<POIDebugLabel>())
                    Assert.That(label.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(0));
                POIDebugLabel.SetVisible(world.root, false); Assert.That(world.root.GetComponentsInChildren<POIDebugLabel>().Length, Is.Zero);
                POIDebugLabel.SetVisible(world.root, true); Assert.That(world.root.GetComponentsInChildren<POIDebugLabel>().Length, Is.EqualTo(16));
                var node = p.specialPOIs[0].chunk;
                CityWorldGenerator.UnloadChunk(world, node.gridPosition); Assert.That(world.specialPOIs.Count, Is.LessThan(15));
                CityWorldGenerator.LoadChunk(world, node, p.chunkPlans[node.gridPosition], scene);
                Assert.That(SpecialPOIValidation.ValidateInstances(world), Is.Empty);
                world.specialPOIs[1].instance = world.specialPOIs[0].instance;
                Assert.That(SpecialPOIValidation.ValidateInstances(world), Is.Not.Empty);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test] public void ForgedOccupancyAndDistanceCorruptionAreRejected()
        {
            var p = CityWorldGenerator.Generate(Template(), 100, Db());
            var first = p.specialPOIs[0]; var lot = p.chunkPlans[first.chunk.gridPosition].lots.Single(l => l.id == first.lotId);
            lot.occupied = false; Assert.That(SpecialPOIValidation.ValidatePlan(p), Is.Not.Empty); lot.occupied = true;
            p.specialPOIs[1].bounds = first.bounds; Assert.That(SpecialPOIValidation.ValidatePlan(p), Is.Not.Empty);
        }
    }
}

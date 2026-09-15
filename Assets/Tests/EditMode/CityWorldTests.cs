using System;
using System.Linq;
using EarthRecovery.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EarthRecovery.Tests
{
    public sealed class CityWorldTests
    {
        static HumanThingsAssetDatabase Db() => AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>("Assets/CityGeneration/CityAssetDatabase.asset");
        static CityWorldTemplate Template() => CityWorldGeneratorWindow.CreateStandard();
        static string Signature(CityWorldPlan p) => string.Join("\n", p.nodes.Select(n => JsonUtility.ToJson(n)))
            + string.Join("\n", p.connections.Select(c => JsonUtility.ToJson(c)))
            + string.Join("\n", p.chunkPlans.OrderBy(c => c.Key.x).ThenBy(c => c.Key.y).Select(c => JsonUtility.ToJson(c.Value.config)
                + string.Join("|", c.Value.placement.placements.Select(v => v.asset.assetGuid + JsonUtility.ToJson(v.position) + JsonUtility.ToJson(v.rotation) + JsonUtility.ToJson(v.scale)))));

        [Test] public void TenWorldSeedsPassGeometryAndConnectivity()
        {
            var report = CityWorldValidation.Run(Template(), Db());
            foreach (var r in report.seeds)
            {
                Assert.That(r.passed, Is.True, r.seed + " " + string.Join("; ", r.errors));
                Assert.That(r.generatedChunks, Is.EqualTo(9)); Assert.That(r.reachedChunks, Is.EqualTo(9));
                Assert.That(r.validConnectorPairs, Is.EqualTo(r.connectorPairs)); Assert.That(r.overlapPairs, Is.Zero);
                Assert.That(r.requestedBuildings, Is.GreaterThan(0)); Assert.That(r.placedBuildings, Is.GreaterThanOrEqualTo(9));
            }
        }
        [Test] public void SeedIsDeterministicAndDoesNotMutateTemplatesDatabaseOrUnityRandom()
        {
            var template = Template(); var db = Db(); var random = UnityEngine.Random.state;
            string before = JsonUtility.ToJson(template), dbBefore = JsonUtility.ToJson(db);
            var chunksBefore = template.chunkTemplates.Select(c => JsonUtility.ToJson(c.template)).ToArray();
            var a = CityWorldGenerator.Generate(template, 1234, db);
            var b = CityWorldGenerator.Generate(template, 1234, db);
            Assert.That(Signature(a), Is.EqualTo(Signature(b)));
            var changed = CityWorldGenerator.Generate(template, 5678, db);
            Assert.That(Signature(a), Is.Not.EqualTo(Signature(changed)));
            Assert.That(a.nodes.Select(n => n.chunkTemplate.templateName), Is.Not.EqualTo(changed.nodes.Select(n => n.chunkTemplate.templateName)));
            Assert.That(a.connections.Select(c => c.worldPoint), Is.Not.EqualTo(changed.connections.Select(c => c.worldPoint)));
            Assert.That(JsonUtility.ToJson(template), Is.EqualTo(before)); Assert.That(JsonUtility.ToJson(db), Is.EqualTo(dbBefore));
            Assert.That(template.chunkTemplates.Select(c => JsonUtility.ToJson(c.template)), Is.EqualTo(chunksBefore));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
            Assert.That(a.nodes.Select(n => n.chunkSeed).Distinct().Count(), Is.EqualTo(9));
        }
        [TestCase(4)] [TestCase(5)] public void LargerWorldsKeepConnectedVariableSizedDistricts(int size)
        {
            var template = UnityEngine.Object.Instantiate(Template());
            try
            {
                template.chunkCountXRange = template.chunkCountZRange = new IntRange(size, size);
                template.worldWidthRange = template.worldDepthRange = new FloatRange(size * 180, size * 200);
                var p = CityWorldGenerator.Generate(template, 1234, Db());
                var r = CityWorldValidation.Validate(p);
                Assert.That(r.passed, Is.True, string.Join("; ", r.errors));
                Assert.That(p.nodes.Count, Is.EqualTo(size * size));
                Assert.That(p.nodes.Select(n => n.worldBounds.size).Distinct().Count(), Is.GreaterThan(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(template); }
        }
        [Test] public void LayoutRespectsDistrictRunsPairedPortsAndGlobalArterial()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var p = CityWorldLayout.Generate(Template(), seed);
                Assert.That(p.nodes.Select(n => n.district).Distinct().Count(), Is.GreaterThan(1));
                Assert.That(p.mainRoute.Count, Is.GreaterThanOrEqualTo(3));
                Assert.That(p.nodes.SelectMany(n => n.connectors).Count(c => c.external), Is.EqualTo(2));
                foreach (var n in p.nodes)
                {
                    Assert.That(n.chunkSeed, Is.EqualTo(CityWorldLayout.DeriveChunkSeed(seed, n.gridPosition.x, n.gridPosition.y)));
                    foreach (var port in n.connectors.Where(c => !c.external))
                    {
                        var other = p.nodes.Single(c => c.gridPosition == port.neighbor);
                        var pair = other.connectors.Single(c => c.connectionId == port.connectionId);
                        Assert.That(Vector3.Distance(port.WorldPoint(n.worldBounds), pair.WorldPoint(other.worldBounds)), Is.LessThan(.002f));
                        Assert.That(port.type, Is.EqualTo(pair.type)); Assert.That(port.roadWidth, Is.EqualTo(pair.roadWidth));
                    }
                    foreach (var delta in new[] { Vector2Int.right, Vector2Int.up })
                    {
                        var n1 = p.nodes.FirstOrDefault(c => c.gridPosition == n.gridPosition + delta);
                        var n2 = p.nodes.FirstOrDefault(c => c.gridPosition == n.gridPosition + delta * 2);
                        if (n1 != null && n2 != null) Assert.That(n1.district != n.district || n2.district != n.district, Is.True);
                    }
                }
            }
        }
        [Test] public void SceneQueriesStreamingAndClearKeepUnrelatedObjects()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var p = CityWorldGenerator.Generate(Template(), 1234, Db());
                var unrelated = new GameObject("Unrelated"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(unrelated, scene);
                var world = CityWorldGenerator.Build(p, scene);
                Assert.That(world.chunks.Count, Is.EqualTo(9));
                Assert.That(world.locations.Select(l => l.id).Distinct().Count(), Is.EqualTo(world.locations.Count));
                Assert.That(world.GetLocationsByType(LocationType.Street).Any(), Is.True);
                Assert.That(world.GetLocationsByTag("road").Any(), Is.True);
                foreach (var c in world.chunks)
                {
                    Assert.That(c.city.root.transform.position, Is.EqualTo(c.node.worldBounds.center));
                    Assert.That(c.city.root.GetComponentsInChildren<Transform>().All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), Is.True);
                    foreach (var l in c.city.locations)
                        Assert.That(world.locations.Single(w => w.id == c.node.Id + "/" + l.id).bounds.center, Is.EqualTo(l.bounds.center + c.node.worldBounds.center));
                }
                int locations = world.locations.Count;
                var node = p.nodes[0];
                Assert.Throws<InvalidOperationException>(() => CityWorldGenerator.LoadChunk(world, node, p.chunkPlans[node.gridPosition], scene));
                CityWorldGenerator.UnloadChunk(world, node.gridPosition);
                Assert.That(world.locations.Count, Is.LessThan(locations)); Assert.That(world.chunks.Count, Is.EqualTo(8));
                Assert.Throws<ArgumentException>(() => CityWorldGenerator.LoadChunk(world, node, p.chunkPlans[p.nodes[1].gridPosition], scene));
                CityWorldGenerator.LoadChunk(world, node, p.chunkPlans[node.gridPosition], scene);
                Assert.That(world.locations.Count, Is.EqualTo(locations));
                CitySceneBuilder.Clear(scene); Assert.That(world.root != null, Is.True);
                CityWorldGenerator.Clear(scene); Assert.That(unrelated != null, Is.True);
                Assert.That(CityWorldGenerator.FindWorlds(scene), Is.Empty);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        [Test] public void ValidatorDetectsBrokenPairSurfaceGraphAndOverlap()
        {
            var p = CityWorldGenerator.Generate(Template(), 1234, Db());
            var first = p.nodes[0]; var port = first.connectors.First(c => !c.external);
            port.normalizedPosition += .05f;
            Assert.That(CityWorldValidation.Validate(p).passed, Is.False); port.normalizedPosition -= .05f;
            var chunk = p.chunkPlans[first.gridPosition];
            var surfaces = chunk.placement.placements.Where(v => v.surface).ToArray();
            chunk.placement.placements.RemoveAll(v => v.surface);
            Assert.That(CityWorldValidation.Validate(p).roadFailures, Is.GreaterThan(0));
            chunk.placement.placements.AddRange(surfaces);
            var solid = chunk.placement.placements.First(v => !v.surface);
            chunk.placement.placements.Add(solid);
            Assert.That(CityWorldValidation.Validate(p).overlapPairs, Is.GreaterThan(0));
            p.connections.Clear(); Assert.That(CityWorldValidation.Validate(p).reachedChunks, Is.LessThan(9));
        }
        [Test] public void InvalidInputsAndCancellationAreSafe()
        {
            Assert.Throws<ArgumentNullException>(() => CityWorldLayout.Generate(null, 1));
            var t = UnityEngine.Object.Instantiate(Template());
            try
            {
                Assert.Throws<OperationCanceledException>(() => CityWorldGenerator.Generate(t, 1, Db(), (_, _) => true));
                t.worldWidthRange = new FloatRange(10, 10);
                Assert.Throws<ArgumentException>(() => CityWorldLayout.Generate(t, 1));
                t.worldWidthRange = new FloatRange(540, 600); t.chunkTemplates.Clear();
                Assert.Throws<ArgumentException>(() => CityWorldLayout.Generate(t, 1));
                Assert.Throws<ArgumentException>(() => CityChunkGenerator.Generate(new CityChunkGenerationRequest { template = Template().chunkTemplates[0].template,
                    chunkBounds = new Bounds(Vector3.zero, new Vector3(140, 0, 140)), requiredConnectors = new() { new ChunkConnector { normalizedPosition = float.NaN } } }, Db()));
            }
            finally { UnityEngine.Object.DestroyImmediate(t); }
        }
        [Test] public void MissingArterialRouteIsRejectedAndEditorWindowLoads()
        {
            var p = CityWorldGenerator.Generate(Template(), 1234, Db());
            p.mainRoute.Clear();
            Assert.That(CityWorldValidation.Validate(p).passed, Is.False);
            var window = ScriptableObject.CreateInstance<CityWorldGeneratorWindow>();
            try { Assert.That(window, Is.Not.Null); }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }
    }
}

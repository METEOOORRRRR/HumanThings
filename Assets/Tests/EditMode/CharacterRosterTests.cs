using System.Linq;
using EarthRecovery.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EarthRecovery.Tests
{
    public sealed class CharacterRosterTests
    {
        [Test]
        public void RosterContainsAllCompleteVisualCharacters()
        {
            var catalog = PlayerCharacterCatalog.Load();
            Assert.That(catalog, Is.Not.Null); Assert.DoesNotThrow(catalog.Validate);
            CollectionAssert.AreEquivalent(new[] { PlayerCharacterCatalog.DefaultId, PlayerCharacterCatalog.ToxicBunnyId, PlayerCharacterCatalog.NeonOutriderId }
                .Concat(AdditionalCharacterBuilder.Characters.Select(c => c.id)), catalog.characters.Select(c => c.id));
            foreach (var entry in catalog.characters)
            {
                Assert.That(entry.Prefab.GetComponentsInChildren<Collider>(), Is.Empty);
                Assert.That(entry.Prefab.GetComponent<PlayerAvatar>().animator.applyRootMotion, Is.False);
                Assert.That(PrefabUtility.GetPrefabAssetType(entry.Prefab), Is.EqualTo(PrefabAssetType.Variant));
                var visual = entry.Prefab.GetComponent<HumanThingsCharacterVisual>();
                Assert.That(visual.skin.sharedMaterial, Is.SameAs(visual.humanThingsMaterial));
                Assert.That(visual.humanThingsMaterial.shader.name, Is.EqualTo("HumanThings/Weathered Character"));
                Assert.That(visual.humanThingsMaterial.FindPass("MotionVectors"), Is.GreaterThanOrEqualTo(0));
                Assert.That(visual.skin.skinnedMotionVectors, Is.True);
                Assert.That(visual.humanThingsMaterial.GetFloat("_Saturation"), Is.EqualTo(entry.visualProfile.saturation));
                foreach (string name in new[] { "_BaseMap", "_BumpMap", "_MetallicRoughnessMap", "_Regions", "_WeatherMask" })
                {
                    var texture = visual.humanThingsMaterial.GetTexture(name) as Texture2D;
                    Assert.That(texture, Is.Not.Null); Assert.That(texture.mipmapCount, Is.GreaterThan(1));
                    Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Trilinear));
                }
            }
            Assert.That(catalog.Resolve("missing"), Is.SameAs(catalog.Resolve(PlayerCharacterCatalog.DefaultId)));
            Assert.That(catalog.Resolve(null), Is.SameAs(catalog.Resolve(PlayerCharacterCatalog.DefaultId)));
        }

        [TestCase(PlayerCharacterCatalog.ToxicBunnyId, ToxicBunnyCharacterBuilder.Source, ToxicBunnyCharacterBuilder.OriginalPrefab, ToxicBunnyCharacterBuilder.Output, 19746)]
        [TestCase(PlayerCharacterCatalog.NeonOutriderId, NeonOutriderCharacterBuilder.Source, NeonOutriderCharacterBuilder.OriginalPrefab, NeonOutriderCharacterBuilder.Output, 19740)]
        public void AddedCharacterPreservesOriginalRigAndUsesRunningForSprint(string id, string source, string prefab, string output, int vertices)
        {
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
            var visual = PlayerCharacterCatalog.Load().Resolve(id).Prefab;
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(visual), Is.EqualTo(original));
            var skin = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(skin.sharedMesh, Is.SameAs(original.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh));
            Assert.That(skin.sharedMesh.vertexCount, Is.EqualTo(vertices)); Assert.That(skin.bones.Length, Is.EqualTo(28));
            Assert.That(AssetDatabase.GetAssetPath(visual.GetComponent<HumanThingsCharacterVisual>().originalMaterial), Is.EqualTo(source));
            CollectionAssert.AreEquivalent(new[] { "restpose", "Walking", "Running" },
                AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>().Select(c => c.name));
            var animator = visual.GetComponent<PlayerAvatar>().animator;
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            var tree = (BlendTree)controller.layers[0].stateMachine.states[0].state.motion;
            Assert.That(tree.children.Single(c => c.motion.name == "Sprint").timeScale, Is.EqualTo(1.2f));
            foreach (string name in new[] { "Idle", "Walk", "Run", "Sprint" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(output + "/" + name + ".anim");
                Assert.That(clip.isLooping, Is.True); Assert.That(clip.legacy, Is.False);
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    Assert.That(animator.transform.Find(binding.path), Is.Not.Null, binding.path);
                    if (binding.path.EndsWith("mixamorig:Hips") && (binding.propertyName == "m_LocalPosition.x" || binding.propertyName == "m_LocalPosition.z"))
                        Assert.That(AnimationUtility.GetEditorCurve(clip, binding).keys.Select(k => k.value).Distinct().Count(), Is.EqualTo(1));
                }
            }
        }

        [Test]
        public void RandomAssignmentsCoverAllModelsAndSurviveMissionsAndSnapshots()
        {
            var rules = ScriptableObject.CreateInstance<GameRules>();
            try
            {
                rules.developerSolo = true;
                var catalog = PlayerCharacterCatalog.Load(); var rng = new System.Random(101);
                var rolls = Enumerable.Range(0, 8000).Select(_ => catalog.PickId(rng)).ToArray();
                float expected = rolls.Length / (float)catalog.characters.Length;
                foreach (var entry in catalog.characters) Assert.That(rolls.Count(id => id == entry.id), Is.InRange(expected * .8f, expected * 1.2f));
                var game = new Expedition(rules, characterRandom: new System.Random(101));
                for (ulong i = 0; i < 6; i++) { Assert.That(game.Join(i, "Agent" + i), Is.True); game.Apply(i, new Command { action = "ready", flag = true }); }
                var assigned = game.State.players.Select(p => p.characterId).ToArray();
                Assert.That(assigned.Distinct().Count(), Is.EqualTo(6));
                Assert.That(assigned.All(id => catalog.characters.Any(c => c.id == id)));
                Assert.That(game.Join(9, "Full"), Is.False);
                Assert.That(game.Start(42), Is.True);
                CollectionAssert.AreEqual(assigned, game.State.players.Select(p => p.characterId));
                foreach (ulong id in new ulong[] { 0, 1, 5 })
                {
                    var snapshot = JsonUtility.FromJson<Snapshot>(SnapshotCodec.Decode(SnapshotCodec.Encode(JsonUtility.ToJson(game.ForClient(id)))));
                    Assert.That(SnapshotValidation.Valid(snapshot, id), Is.True);
                    CollectionAssert.AreEqual(assigned, snapshot.players.Select(p => p.characterId));
                    snapshot.players[0].characterId = new string('x', 65);
                    Assert.That(SnapshotValidation.Valid(snapshot, id), Is.False);
                }
                game.ResetLobby();
                CollectionAssert.AreEqual(assigned, game.State.players.Select(p => p.characterId));
                game.Disconnect(2);
                var occupied = game.State.players.ToDictionary(p => p.id, p => p.characterId);
                Assert.That(game.Join(2, "Rejoined"), Is.True);
                Assert.That(occupied.Values, Does.Not.Contain(game.Player(2).characterId));
                Assert.That(occupied.All(p => game.Player(p.Key).characterId == p.Value));
            }
            finally { Object.DestroyImmediate(rules); }
        }

        [Test]
        public void LastFreeCharacterIsUsedAndExhaustionNeverFallsBackToADuplicate()
        {
            var catalog = PlayerCharacterCatalog.Load();
            var ids = catalog.characters.Select(c => c.id).ToArray();
            Assert.That(catalog.PickId(new System.Random(42), ids.Skip(1)), Is.EqualTo(ids[0]));
            Assert.Throws<System.InvalidOperationException>(() => catalog.PickId(new System.Random(42), ids));
        }

        sealed class FirstAvailableRandom : System.Random
        {
            public override int Next(int maxValue) => 0;
        }

        [TestCase(false)] [TestCase(true)]
        public void RepeatedIdenticalRandomRollsCannotDuplicateNormalOrDeveloperParties(bool developerParty)
        {
            var rules = ScriptableObject.CreateInstance<GameRules>();
            try
            {
                var game = new Expedition(rules, characterRandom: new FirstAvailableRandom(), developerParty: developerParty);
                for (ulong id = 0; id < 6; id++)
                {
                    Assert.That(game.Join(id, "Agent"));
                    Assert.That(game.State.players.Select(p => p.characterId).Distinct().Count(), Is.EqualTo(game.State.players.Count));
                }
                var retained = game.State.players.Where(p => p.id != 2).ToDictionary(p => p.id, p => p.characterId);
                string released = game.Player(2).characterId;
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    game.Disconnect(2);
                    Assert.That(game.Join(2, "Rejoined"));
                    Assert.That(game.State.players.Select(p => p.characterId).Distinct().Count(), Is.EqualTo(6));
                    Assert.That(retained.All(p => game.Player(p.Key).characterId == p.Value));
                    Assert.That(game.Player(2).characterId, Is.EqualTo(released), "Leaving releases that character for reuse.");
                }
            }
            finally { Object.DestroyImmediate(rules); }
        }

        [Test]
        public void CleanOutriderUsesSeparateTextureAndRetainsOriginalComparison()
        {
            var entry = PlayerCharacterCatalog.Load().Resolve(PlayerCharacterCatalog.NeonOutriderId);
            var visual = entry.Prefab.GetComponent<HumanThingsCharacterVisual>();
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(NeonOutriderCharacterBuilder.OriginalPrefab)
                .GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;
            Assert.That(visual.originalMaterial, Is.SameAs(original));
            Assert.That(AssetDatabase.GetAssetPath(original), Is.EqualTo(NeonOutriderCharacterBuilder.Source));
            var albedo = visual.humanThingsMaterial.GetTexture("_BaseMap");
            Assert.That(AssetDatabase.GetAssetPath(albedo), Is.EqualTo(NeonOutriderCharacterBuilder.CleanAlbedo));
            Assert.That(albedo.width, Is.EqualTo(4096)); Assert.That(albedo.height, Is.EqualTo(4096));
            Assert.That(visual.humanThingsMaterial.GetVector("_Weather"), Is.EqualTo(Vector4.zero));
            Assert.That(entry.visualProfile.normalStrength, Is.Zero);
            Assert.That(entry.visualProfile.metallicMultiplier, Is.Zero);
        }

        [Test]
        public void FiveConceptCharactersRetainTheirRigAndUseSeparateCleanAlbedos()
        {
            int[] vertices = { 20713, 19651, 19319, 18123, 19010 };
            for (int i = 0; i < AdditionalCharacterBuilder.Characters.Length; i++)
            {
                var character = AdditionalCharacterBuilder.Characters[i];
                AddedCharacterPreservesOriginalRigAndUsesRunningForSprint(character.id, character.Source,
                    character.OriginalPrefab, character.Output, vertices[i]);
                var entry = PlayerCharacterCatalog.Load().Resolve(character.id);
                Assert.That(entry.id, Is.EqualTo(character.id));
                var visual = entry.Prefab.GetComponent<HumanThingsCharacterVisual>();
                var albedo = visual.humanThingsMaterial.GetTexture("_BaseMap");
                Assert.That(AssetDatabase.GetAssetPath(albedo), Is.EqualTo(character.Albedo));
                Assert.That(albedo.width, Is.EqualTo(4096)); Assert.That(albedo.height, Is.EqualTo(4096));
                Assert.That(visual.humanThingsMaterial.GetVector("_Weather"), Is.EqualTo(Vector4.zero));
                Assert.That(entry.visualProfile.normalStrength, Is.Zero);
                Assert.That(entry.visualProfile.metallicMultiplier, Is.Zero);
            }
        }

        [TestCase(0, 1.6f, .65f, .5f, .48f, 0)]
        [TestCase(0, 1.14f, .65f, .5f, .48f, 0)]
        [TestCase(.32f, 1, .65f, .5f, .48f, 0)]
        [TestCase(0, 1.65f, .8f, .8f, .8f, 1)]
        [TestCase(.25f, 1.4f, .8f, .8f, .8f, 2)]
        [TestCase(.12f, .6f, .1f, .1f, .1f, 2)]
        [TestCase(.12f, .12f, .1f, .1f, .1f, 3)]
        [TestCase(.12f, 1.14f, .9f, .7f, .1f, 2)]
        public void NewAtlasSeparatesExposedSkinHairClothAndBoots(float x, float y, float r, float g, float b, int channel)
        {
            var color = ToxicBunnyCharacterBuilder.Classify(new Vector3(x, y, .1f), new Color(r, g, b), .1f);
            Assert.That(color[channel], Is.GreaterThan(.9f));
            Assert.That(color.r + color.g + color.b + color.a, Is.EqualTo(1).Within(.001f));
        }
    }
}

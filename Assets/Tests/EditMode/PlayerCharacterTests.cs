using System.Linq;
using EarthRecovery.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Tests
{
    public sealed class PlayerCharacterTests
    {
        [Test]
        public void SourceRetainsAllSevenAnimationsAndTexturedSkin()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(PlayerCharacterBuilder.Source);
            CollectionAssert.AreEquivalent(new[] { "Running", "Walking", "All_Night_Dance", "Boxing_Practice", "RunFast", "You_Groove", "restpose" },
                assets.OfType<AnimationClip>().Select(c => c.name));
            Assert.That(assets.OfType<Texture2D>().Count(), Is.GreaterThanOrEqualTo(3));
            Assert.That(assets.OfType<Mesh>().Single().vertexCount, Is.EqualTo(19590));
        }

        [Test]
        public void PrefabHasAnAnimatedSkinWithoutExtraPhysics()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCharacterBuilder.PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var avatar = prefab.GetComponent<PlayerAvatar>();
            Assert.That(avatar.animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(avatar.animator.applyRootMotion, Is.False);
            Assert.That(prefab.GetComponentsInChildren<Collider>(), Is.Empty);
            var renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(renderer.bones.Length, Is.EqualTo(27));
            Assert.That(renderer.bones.All(b => b != null), Is.True);
            Assert.That(renderer.sharedMaterial.shader.name, Does.Not.Contain("InternalError"));
        }

        [TestCase("Idle")]
        [TestCase("Walk")]
        [TestCase("Run")]
        [TestCase("Sprint")]
        public void LocomotionClipsLoopAndDoNotTranslateTheRigAcrossTheFloor(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerCharacterBuilder.Output + "/" + name + ".anim");
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.legacy, Is.False);
            Assert.That(clip.length, Is.GreaterThan(.3f));
            Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.True);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCharacterBuilder.PrefabPath);
            var model = prefab.GetComponent<PlayerAvatar>().animator.transform;
            var bindings = AnimationUtility.GetCurveBindings(clip);
            Assert.That(bindings.Length, Is.GreaterThan(100));
            foreach (var binding in bindings)
            {
                Assert.That(model.Find(binding.path), Is.Not.Null, binding.path);
                if (binding.path.EndsWith("mixamorig:Hips") &&
                    (binding.propertyName == "m_LocalPosition.x" || binding.propertyName == "m_LocalPosition.z"))
                {
                    var values = AnimationUtility.GetEditorCurve(clip, binding).keys.Select(k => k.value).ToArray();
                    Assert.That(values.Max() - values.Min(), Is.LessThan(.00001f));
                }
            }
        }

        [TestCase(0, 0)]
        [TestCase(.05f, 0)]
        [TestCase(2, .5f)]
        [TestCase(4, 1)]
        [TestCase(5, 2)]
        [TestCase(6, 3)]
        [TestCase(8, 3)]
        public void ActualSpeedSelectsTheExpectedGait(float speed, float expected)
        {
            Assert.That(PlayerAvatar.Locomotion(speed, 4, 6), Is.EqualTo(expected).Within(.001f));
        }
    }
}

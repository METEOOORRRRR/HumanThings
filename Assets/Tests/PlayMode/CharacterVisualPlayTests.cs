using NUnit.Framework;
using UnityEngine;

namespace EarthRecovery.Tests
{
    public sealed class CharacterVisualPlayTests
    {
        GameObject a, b;
        HumanThingsCharacterVisualProfile profile;

        [TearDown]
        public void Cleanup()
        {
            if (a != null) Object.DestroyImmediate(a);
            if (b != null) Object.DestroyImmediate(b);
            if (profile != null) Object.DestroyImmediate(profile);
        }

        [Test]
        public void ComparisonAndPerCharacterOverrideNeverChangeSharedMaterials()
        {
            var prefab = Resources.Load<HumanThingsCharacterVisualProfile>("HumanThingsCharacterVisualProfile").visualPrefab;
            a = Object.Instantiate(prefab); b = Object.Instantiate(prefab);
            var first = a.GetComponent<HumanThingsCharacterVisual>(); var second = b.GetComponent<HumanThingsCharacterVisual>();
            var original = first.originalMaterial;
            var shared = first.humanThingsMaterial;
            float saturation = shared.GetFloat("_Saturation");
            first.CompareOriginal(true);
            Assert.That(first.skin.sharedMaterial, Is.SameAs(original));
            Assert.That(second.skin.sharedMaterial, Is.SameAs(shared));
            profile = ScriptableObject.CreateInstance<HumanThingsCharacterVisualProfile>(); profile.saturation = .17f; profile.wetness = .7f;
            first.profileOverride = profile; first.Refresh(); first.CompareOriginal(false);
            Assert.That(first.skin.sharedMaterial.GetFloat("_Saturation"), Is.EqualTo(.17f));
            Assert.That(first.skin.sharedMaterial.GetVector("_Weather").w, Is.EqualTo(.7f));
            Assert.That(second.skin.sharedMaterial.GetFloat("_Saturation"), Is.EqualTo(saturation));
            Assert.That(original.shader.name, Is.EqualTo("Shader Graphs/glTF-pbrMetallicRoughness"));
            first.profileOverride = null; first.Refresh();
            Assert.That(first.skin.sharedMaterial, Is.SameAs(shared));
        }

        [Test]
        public void VisualVariantStillAnimatesWithTheExistingController()
        {
            var prefab = Resources.Load<HumanThingsCharacterVisualProfile>("HumanThingsCharacterVisualProfile").visualPrefab;
            a = Object.Instantiate(prefab);
            var avatar = a.GetComponent<PlayerAvatar>();
            avatar.animator.Rebind(); avatar.animator.SetFloat("Gait", 3); avatar.animator.Update(.2f);
            Assert.That(avatar.animator.GetCurrentAnimatorClipInfo(0)[0].clip.name, Is.EqualTo("Sprint"));
            Assert.That(avatar.animator.applyRootMotion, Is.False);
            Assert.That(a.GetComponent<HumanThingsCharacterVisual>().skin.sharedMaterial.shader.isSupported, Is.True);
        }
    }
}

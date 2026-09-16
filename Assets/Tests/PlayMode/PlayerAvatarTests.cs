using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EarthRecovery.Tests
{
    public sealed class PlayerAvatarTests
    {
        GameObject root;
        PlayerAvatar avatar;
        PlayerState player;
        GameRules rules;

        [SetUp]
        public void Setup()
        {
            root = new GameObject("Avatar test agent");
            var prefab = Resources.Load<GameObject>("PlayerCharacter");
            Assert.That(prefab, Is.Not.Null);
            avatar = Object.Instantiate(prefab, root.transform, false).GetComponent<PlayerAvatar>();
            player = new PlayerState(); rules = ScriptableObject.CreateInstance<GameRules>();
            avatar.Present(player, rules, true, .02f);
            avatar.animator.Rebind(); avatar.animator.Update(0);
        }

        [TearDown]
        public void Cleanup() { Object.DestroyImmediate(root); Object.DestroyImmediate(rules); }

        void Move(float speed, int frames = 50)
        {
            for (int i = 0; i < frames; i++)
            {
                root.transform.position += Vector3.forward * speed * .02f;
                avatar.Present(player, rules, true, .02f);
                avatar.animator.Update(.02f);
            }
        }

        [Test]
        public void WalkingAndSprintingAnimateBonesWithoutMovingAgentRoot()
        {
            var foot = avatar.GetComponentsInChildren<Transform>().Single(t => t.name == "mixamorig:LeftFoot");
            Move(4);
            Assert.That(avatar.animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name == "Walk" && c.weight > .9f), Is.True);
            var position = root.transform.position;
            var pose = foot.position;
            avatar.animator.Update(.23f);
            Assert.That(Vector3.Distance(foot.position, pose), Is.GreaterThan(.03f));
            Assert.That(root.transform.position, Is.EqualTo(position));
            Move(6);
            Assert.That(avatar.animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name == "Sprint" && c.weight > .9f), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void StandingCharacterFitsTheExistingControllerAndRestsOnTheFloor()
        {
            var renderer = avatar.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = new Mesh();
            try
            {
                // Compensate for the renderer scale before transforming vertices to world space.
                renderer.BakeMesh(mesh, true);
                var points = mesh.vertices.Select(v => renderer.transform.TransformPoint(v)).ToArray();
                Assert.That(points.Min(v => v.y), Is.EqualTo(0).Within(.04f));
                Assert.That(points.Max(v => v.y), Is.EqualTo(1.8f).Within(.04f));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [TestCase(PlayerCharacterCatalog.DefaultId)]
        [TestCase(PlayerCharacterCatalog.ToxicBunnyId)]
        public void RegisteredCharactersAnimateAtTheSameGameplayScale(string id)
        {
            Object.DestroyImmediate(avatar.gameObject);
            avatar = Object.Instantiate(PlayerCharacterCatalog.Load().Resolve(id).Prefab, root.transform, false).GetComponent<PlayerAvatar>();
            avatar.animator.Rebind(); avatar.animator.Update(0);
            avatar.Present(player, rules, true, .02f);
            StandingCharacterFitsTheExistingControllerAndRestsOnTheFloor();
            WalkingAndSprintingAnimateBonesWithoutMovingAgentRoot();
        }

        [Test]
        public void TeleportsAndInactivePlayersDoNotAnimateAsRunning()
        {
            root.transform.position = Vector3.one * 100;
            avatar.Present(player, rules, true, .02f);
            Assert.That(avatar.Speed, Is.Zero);
            Move(4);
            player.alive = false;
            avatar.Present(player, rules, true, .02f);
            Assert.That(avatar.animator.enabled, Is.False);
            Assert.That(avatar.Speed, Is.Zero);
            player.alive = true;
            avatar.Present(player, rules, false, .02f);
            Assert.That(avatar.animator.enabled, Is.True);
            Assert.That(avatar.animator.GetFloat("Gait"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator EachPlayerOwnsAnIndependentAnimatedRig()
        {
            var guest = Object.Instantiate(Resources.Load<GameObject>("PlayerCharacter"), root.transform, false);
            try
            {
                var other = guest.GetComponent<PlayerAvatar>();
                other.animator.Rebind(); other.animator.Update(0);
                Move(4);
                Assert.That(other.animator.GetFloat("Gait"), Is.Zero);
                Assert.That(avatar.animator.GetFloat("Gait"), Is.GreaterThan(.9f));
                Assert.That(avatar.GetComponentInChildren<SkinnedMeshRenderer>().bones[0],
                    Is.Not.SameAs(other.GetComponentInChildren<SkinnedMeshRenderer>().bones[0]));
                yield return null;
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.DestroyImmediate(guest); }
        }
    }
}

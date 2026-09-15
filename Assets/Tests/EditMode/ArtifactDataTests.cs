using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace EarthRecovery.Tests
{
    public sealed class ArtifactDataTests
    {
        static HumanContentSource Source() => HumanContentSource.Parse(Resources.Load<TextAsset>("HumanThingsSeed").text);
        [Test] public void SourceHasFiveZonesAndExactlyOneOwnerPerArtifact()
        {
            var source = Source();
            foreach (int zone in Enumerable.Range(0, 5)) Assert.That(source.locations.Count(l => l.zone == zone), Is.EqualTo(3));
            CollectionAssert.AreEquivalent(source.artifacts.Select(a => a.id), source.locations.SelectMany(l => l.artifactIds));
            Assert.That(source.artifacts.Sum(a => a.archiveDetails.Length), Is.EqualTo(135));
        }
        [TestCase("duplicate")][TestCase("missing")][TestCase("hint")][TestCase("owner")]
        public void MalformedSourceIsRejected(string problem)
        {
            var source = Source();
            if (problem == "duplicate") source.artifacts[1].id = source.artifacts[0].id;
            if (problem == "missing") source.locations = source.locations.Skip(1).ToArray();
            if (problem == "hint") source.artifacts[0].missionHint = null;
            if (problem == "owner") source.artifacts[0].locationId = source.locations[1].id;
            Assert.Throws<InvalidOperationException>(() => HumanContentSource.Parse(JsonUtility.ToJson(source)));
        }
        [Test] public void EveryArtifactUsesOnlyMissionHintsBeforeRecoveryRegardlessOfDiscovery()
        {
            var rules = ScriptableObject.CreateInstance<GameRules>();
            try
            {
                var game = new Expedition(rules);
                for (ulong id = 0; id < 4; id++) { game.Join(id, "Agent"); game.Player(id).ready = true; }
                Assert.That(game.Start(42));
                foreach (var site in game.State.sites)
                    for (int product = 0; product < 3; product++)
                    {
                        site.product = product; site.discovered = true; site.reveal = RevealLevel.DuringCraft;
                        game.Player(0).knownLocations.Add(site.id);
                        var projected = game.ForClient(0).sites[site.id]; var a = game.Content.Artifact(site);
                        Assert.That(projected.displayName, Is.Empty);
                        CollectionAssert.AreEqual(new[] { a.missionHint.category, a.missionHint.structure, a.missionHint.function, game.Content.locations[site.id].missionLocationHint }, Catalog.MissionLines(projected));
                        site.reveal = RevealLevel.Recovered;
                        Assert.That(game.ForClient(0).sites[site.id].displayName, Is.EqualTo(a.trueNameKo));
                    }
            }
            finally { UnityEngine.Object.DestroyImmediate(rules); }
        }
        [Test] public void LockedArchiveDoesNotResolveNamesOrDetails()
        {
            var entry = new ArchiveEntry { artifactId = "HT_A01_01", trueNameKo = "stale name", archiveDescription = "stale description" };
            entry.observations.Add("existing observation");
            CollectionAssert.AreEqual(new[] { "미식별 관찰 기록", "existing observation" }, Catalog.ArchiveLines(entry));
        }
        [Test] public void LegacyArchiveProgressUsesCurrentTextByIdWithoutChangingSave()
        {
            var entry = JsonUtility.FromJson<ArchiveEntry>("{\"artifactId\":\"HT_A01_01\",\"discovered\":true,\"recoverCount\":2,\"trueNameKo\":\"old\",\"observations\":[\"observed\"]}");
            var before = JsonUtility.ToJson(entry); var a = HumanContent.Load().artifacts.Single(x => x.id == entry.artifactId);
            var lines = Catalog.ArchiveLines(entry);
            Assert.That(lines[0], Does.Contain(a.trueNameKo).And.Contain(a.trueNameEn));
            Assert.That(lines, Does.Contain(a.missionHint.category).And.Contain(a.archiveDescription));
            foreach (var detail in a.archiveDetails) Assert.That(lines, Does.Contain(detail));
            Assert.That(lines, Does.Contain("Mars: " + a.marsComment));
            Assert.That(JsonUtility.ToJson(entry), Is.EqualTo(before));
        }
        [TestCase(0)][TestCase(1)][TestCase(5)]
        public void ArchiveDetailsAreNotLimitedToThreeLines(int count)
        {
            var a = HumanContent.Load().artifacts[0]; var saved = a.archiveDetails;
            try
            {
                a.archiveDetails = Enumerable.Range(0, count).Select(i => "Detail " + i).ToArray();
                var lines = Catalog.ArchiveLines(new ArchiveEntry { artifactId = a.id, discovered = true });
                Assert.That(lines.Length, Is.EqualTo(count + 4));
                foreach (var detail in a.archiveDetails) Assert.That(lines, Does.Contain(detail));
            }
            finally { a.archiveDetails = saved; }
        }
    }
}

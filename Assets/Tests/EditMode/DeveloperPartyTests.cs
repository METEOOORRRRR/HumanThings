using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace EarthRecovery.Tests
{
    public sealed class DeveloperPartyTests
    {
        GameRules rules;
        Expedition game;
        [SetUp] public void Setup()
        {
            rules = ScriptableObject.CreateInstance<GameRules>(); rules.maxPlayers = 4;
            game = new Expedition(rules, developerParty: true);
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(rules);

        [Test] public void SoloJoinCreatesSixDistinctSpawnSlotsWithFiveReadyRegisteredCharacters()
        {
            Assert.That(game.Join(0, "Tester"));
            Assert.That(rules.maxPlayers, Is.EqualTo(6));
            Assert.That(game.State.players.Count, Is.EqualTo(6));
            Assert.That(game.State.players.Select(p => p.id).Distinct().Count(), Is.EqualTo(6));
            Assert.That(game.State.players.Select(p => p.characterId).Distinct().Count(), Is.EqualTo(6));
            Assert.That(game.State.players.Select(p => p.position).Distinct().Count(), Is.EqualTo(6));
            var dummies = game.State.players.Where(p => p.developerDummy).ToArray();
            Assert.That(dummies.Length, Is.EqualTo(5));
            Assert.That(dummies.All(p => p.connected && p.ready && !p.voiceEnabled));
            var ids = PlayerCharacterCatalog.Load().characters.Select(c => c.id).ToArray();
            Assert.That(game.State.players.All(p => ids.Contains(p.characterId)));
            Assert.That(SnapshotValidation.Valid(game.ForClient(0), 0));
            Assert.That(game.Start(42), Is.False, "The real operator must still ready up.");
            game.ValidateAndExecute(0, new Command { action = "ready", flag = true });
            Assert.That(game.Start(42));
            Assert.That(SnapshotValidation.Valid(game.ForClient(0), 0));
        }

        [Test] public void RealConnectionsReplaceStandInsAndLeavingRestoresOnlyTheFreeSlot()
        {
            game.Join(0, "Host");
            for (ulong i = 1; i < 6; i++)
            {
                Assert.That(game.Join(i, "Client"));
                Assert.That(game.State.players.Count, Is.EqualTo(6));
                Assert.That(game.State.players.Select(p => p.characterId).Distinct().Count(), Is.EqualTo(6));
                Assert.That(game.State.players.Count(p => p.developerDummy), Is.EqualTo(5 - (int)i));
            }
            Assert.That(game.Join(6, "Overflow"), Is.False);
            game.Disconnect(3);
            Assert.That(game.State.players.Count, Is.EqualTo(6));
            Assert.That(game.State.players.Single(p => p.developerDummy).ready);
            Assert.That(game.State.players.Select(p => p.characterId).Distinct().Count(), Is.EqualTo(6));
            Assert.That(game.Join(7, "Replacement"));
            Assert.That(game.State.players.Any(p => p.developerDummy), Is.False);
        }

        [Test] public void ResetRestoresAutomaticReadinessAndRetainsCharactersWithoutDuplicates()
        {
            game.Join(0, "Tester");
            var before = game.State.players.ToDictionary(p => p.id, p => p.characterId);
            game.Player(0).ready = true; Assert.That(game.Start(42));
            game.End(Phase.Abandoned, "test"); game.ResetLobby();
            Assert.That(game.State.players.Count, Is.EqualTo(6));
            Assert.That(game.Player(0).ready, Is.False);
            Assert.That(game.State.players.Where(p => p.developerDummy).All(p => p.ready));
            Assert.That(game.State.players.All(p => before[p.id] == p.characterId));
            game.Disconnect(0);
            Assert.That(game.State.players, Is.Empty);
        }

        [Test] public void DummiesDoNotBlockSoloHibernationOrAllDead()
        {
            game.Join(0, "Tester"); game.Player(0).ready = true; Assert.That(game.Start(42));
            Assert.That(game.Alive, Is.EqualTo(1));
            Assert.That(game.ValidateAndExecute(0, new Command { action = "hibernate" }));
            Assert.That(game.State.phase, Is.EqualTo(Phase.Abandoned));
            game.ResetLobby(); game.Player(0).ready = true; Assert.That(game.Start(42));
            game.Kill(game.Player(0), "test"); game.Tick(.1f);
            Assert.That(game.State.phase, Is.EqualTo(Phase.AllDead));
        }

        [Test] public void NormalAndLegacySoloRulesDoNotCreateStandIns()
        {
            var normal = new Expedition(rules);
            normal.Join(0, "Tester");
            Assert.That(normal.State.players.Count, Is.EqualTo(1));
            rules.developerSolo = true;
            var legacy = new Expedition(rules); legacy.Join(0, "Tester");
            Assert.That(legacy.State.players.Count, Is.EqualTo(1));
        }
    }
}

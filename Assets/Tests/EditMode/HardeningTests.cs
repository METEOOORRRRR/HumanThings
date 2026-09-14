using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace EarthRecovery.Tests
{
    public sealed class HardeningTests
    {
        GameRules rules;
        Expedition game;
        [SetUp] public void Setup()
        {
            rules = ScriptableObject.CreateInstance<GameRules>(); rules.minPlayers=2; game = new Expedition(rules);
            game.Join(0, "Camp"); game.Join(1, "Field");
            game.Apply(0, new Command { action = "ready", flag = true });
            game.Apply(1, new Command { action = "ready", flag = true }); game.Start(42);
        }
        [TearDown] public void Cleanup() => UnityEngine.Object.DestroyImmediate(rules);
        [Test] public void RepeatedPickupDropKeepsBoundedStateAndConservesResources()
        {
            var p = game.Player(1); var item = game.State.loot[0]; p.position = item.position;
            for (int i = 0; i < 2000; i++)
            {
                Assert.That(game.Apply(1, new Command { action = "loot", target = item.id }));
                Assert.That(game.Apply(1, new Command { action = "dropMaterial", target = item.material }));
            }
            Assert.That(game.State.loot.Count, Is.EqualTo(120));
            Assert.That(game.State.loot.Count(x => !x.collected) + p.inventory.Sum(), Is.EqualTo(120));
        }
        [Test] public void InvalidTimeDoesNotPoisonState()
        { game.Tick(float.NaN); game.Tick(float.PositiveInfinity); game.Tick(-1); Assert.That(game.State.elapsed, Is.Zero); }
        [Test] public void InvalidRulesAreRepaired()
        {
            rules.skillCycleSeconds = 0; rules.walkSpeed = float.NaN; rules.resourceCapacity = int.MaxValue;
            rules.oxygenSeconds = float.PositiveInfinity; rules.Sanitize();
            Assert.That(rules.skillCycleSeconds, Is.GreaterThan(0)); Assert.That(float.IsFinite(rules.walkSpeed));
            Assert.That(rules.resourceCapacity, Is.EqualTo(30)); Assert.That(rules.oxygenSeconds, Is.EqualTo(1200));
        }
        [Test] public void ExtremeMovementAndInvalidTargetsAreRejected()
        {
            Assert.That(game.Apply(0, new Command { action = "move", move = new Vector2(float.MaxValue, 1) }), Is.False);
            Assert.That(game.Apply(0, new Command { action = "move", yaw = float.NaN }), Is.False);
            Assert.That(game.Apply(0, new Command { action = "upgrade", target = int.MaxValue }), Is.False);
            Assert.That(game.Apply(0, null), Is.False);
        }
        [Test] public void StoredMovementCannotBeMutatedByCaller()
        {
            var c = new Command { action = "move", move = Vector2.one }; game.Apply(0, c); c.move = new Vector2(float.NaN, 0);
            Assert.That(float.IsFinite(game.Inputs[0].move.x));
        }
        [Test] public void DiagonalPuzzleInputCannotSkipWallsOrConsumeCooldown()
        {
            var s = game.State.sites.First(x => x.mission); s.phase = CraftPhase.Puzzle;
            game.Player(1).position = s.position; game.Player(1).viewingSite = s.id; game.Tick(1);
            Assert.That(game.Apply(0, new Command { action = "puzzle", target = s.id, x = 1, y = 1 }), Is.False);
            Assert.That(s.lastPuzzleInput, Is.Zero);
            Assert.That(game.Apply(0, new Command { action = "puzzle", target = s.id, x = 1 }));
        }
        [Test] public void TerminalResultCannotBeOverwrittenByDeathOrEnd()
        {
            game.End(Phase.Success, "done"); game.Kill(game.Player(1), "late"); game.End(Phase.AllDead, "late");
            Assert.That(game.State.phase, Is.EqualTo(Phase.Success)); Assert.That(game.Player(1).alive);
        }
        [Test] public void LobbyResetClearsTransientPlayerState()
        {
            var p = game.Player(0); p.flashlight = true; p.stationary = 99; p.deathReason = "old";
            game.ResetLobby(); Assert.That(p.flashlight, Is.False); Assert.That(p.stationary, Is.Zero); Assert.That(p.deathReason, Is.Empty);
        }
        [Test] public void LobbyRejoinUsesVacantSpawn()
        {
            game.ResetLobby(); game.Join(2, "Third"); game.Disconnect(1); game.Join(3, "New");
            Assert.That(game.State.players.Select(p => p.position).Distinct().Count(), Is.EqualTo(3));
        }
        [TestCase("!!!", "요원")] [TestCase(" <abc> ", "abc")] [TestCase(null, "요원")]
        public void NamesHaveStableNonemptyNormalization(string input, string expected)
        { Assert.That(Expedition.CleanName(input), Is.EqualTo(expected)); }
        [Test] public void ValidSnapshotsForBothRolesPassValidation()
        {
            var s = game.State.sites.First(x => x.mission); game.Player(1).position = s.position; game.Player(1).viewingSite = s.id;
            Assert.That(SnapshotValidation.Valid(game.ForClient(0), 0)); Assert.That(SnapshotValidation.Valid(game.ForClient(1), 1));
        }
        [Test] public void NullListsAndBadIndicesFailValidation()
        {
            var s = game.ForClient(0); s.sites[0].id = 999; Assert.That(SnapshotValidation.Valid(s, 0), Is.False);
            s = game.ForClient(0); s.players[0].inventory = null; Assert.That(SnapshotValidation.Valid(s, 0), Is.False);
            s = game.ForClient(0); s.loot = null; Assert.That(SnapshotValidation.Valid(s, 0), Is.False);
            Assert.That(SnapshotValidation.Valid(null, 0), Is.False);
        }
        [Test] public void DuplicateIdsAndNonfiniteCoordinatesFailValidation()
        {
            var s = game.ForClient(0); s.loot[1].id = s.loot[0].id; Assert.That(SnapshotValidation.Valid(s, 0), Is.False);
            s = game.ForClient(0); s.players[0].position = new Vector3(float.NaN, 0, 0); Assert.That(SnapshotValidation.Valid(s, 0), Is.False);
        }
        [Test] public void CompressionBombIsBounded()
        { Assert.Throws<System.IO.InvalidDataException>(() => SnapshotCodec.Decode(SnapshotCodec.Encode(new string('x', 262145)))); }
    }
}

using System;
using NUnit.Framework;
using UnityEngine;
namespace EarthRecovery.Tests
{
    public sealed class WaitingRoomTests
    {
        GameRules rules; Expedition game;
        [SetUp] public void Setup() { rules = ScriptableObject.CreateInstance<GameRules>(); game = new Expedition(rules); game.Join(0, "호스트"); }
        [TearDown] public void Cleanup() => UnityEngine.Object.DestroyImmediate(rules);
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void LayoutFitsWithoutOverlapping(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var r = WaitingRoom.CardRect(count, i);
                Assert.That(r.xMin, Is.GreaterThanOrEqualTo(200)); Assert.That(r.xMax, Is.LessThanOrEqualTo(1160));
                Assert.That(r.yMin, Is.GreaterThanOrEqualTo(292)); Assert.That(r.yMax, Is.LessThanOrEqualTo(724));
                for (int j = 0; j < i; j++) Assert.That(r.Overlaps(WaitingRoom.CardRect(count, j)), Is.False);
            }
        }
        [Test] public void InvalidLayoutRejected() { Assert.Throws<ArgumentOutOfRangeException>(() => WaitingRoom.CardRect(7, 0)); Assert.Throws<ArgumentOutOfRangeException>(() => WaitingRoom.CardRect(1, 1)); }
        [Test] public void RegionCatalogContainsUniqueIdsAndImageResources()
        {
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var region in ExpeditionRegions.All) { Assert.That(ids.Add(region.id)); Assert.That(Resources.Load<Texture2D>(region.image), Is.Not.Null); Assert.That(region.name, Is.Not.Empty); }
            Assert.That(ExpeditionRegions.Find("unknown"), Is.Null);
        }
        [Test] public void InvalidAdvertisedRegionRejected()
        { var room = new LanRoom { id = Guid.NewGuid().ToString("N"), name = "test", regionId = "unknown", region = null, port = 7777, capacity = 4, players = 1 }; Assert.That(LanRooms.ValidRoom(room), Is.False); }
        [Test] public void ReturningToLobbyKeepsRegion()
        { game.State.regionId = "configured-region"; game.ResetLobby(); Assert.That(game.State.regionId, Is.EqualTo("configured-region")); }
        [Test] public void ChatUsesAuthoritativeSenderAndSanitizesControls()
        { Assert.That(game.Apply(0, new Command { action = "chat", text = "  가\n나\t다  " })); var c = game.State.lobbyChat[0]; Assert.That(c.name, Is.EqualTo("호스트")); Assert.That(c.sender, Is.Zero); Assert.That(c.text, Is.EqualTo("가나다")); }
        [TestCase(null)] [TestCase("")] [TestCase(" \n\t")]
        public void EmptyChatRejected(string text) => Assert.That(game.Apply(0, new Command { action = "chat", text = text }), Is.False);
        [Test] public void OversizedChatRejected() => Assert.That(game.Apply(0, new Command { action = "chat", text = new string('a', 121) }), Is.False);
        [Test] public void ChatFloodRejected() { Assert.That(game.Apply(0, new Command { action = "chat", text = "first" })); Assert.That(game.Apply(0, new Command { action = "chat", text = "second" }), Is.False); }
        [Test] public void UnknownSenderRejected() => Assert.That(game.Apply(99, new Command { action = "chat", text = "test" }), Is.False);
        [Test] public void HistoryBoundedAcrossRejoins()
        { for (int i = 0; i < 30; i++) { game.Disconnect(0); game.Join(0, "호스트"); Assert.That(game.Apply(0, new Command { action = "chat", text = "message" + i })); } Assert.That(game.State.lobbyChat.Count, Is.EqualTo(24)); Assert.That(game.State.lobbyChat[0].sequence, Is.EqualTo(7)); }
        [Test] public void VoiceStateOnlyChangesSender() { game.Join(1, "요원"); Assert.That(game.Apply(1, new Command { action = "voiceState", flag = true })); Assert.That(game.Player(1).voiceEnabled); Assert.That(game.Player(0).voiceEnabled, Is.False); }
        [Test] public void LobbyCommandsRejectedDuringMission()
        { for (ulong i = 1; i < 4; i++) game.Join(i, "요원"); for (ulong i = 0; i < 4; i++) game.Apply(i, new Command { action = "ready", flag = true }); Assert.That(game.Start(42)); Assert.That(game.Apply(0, new Command { action = "chat", text = "test" }), Is.False); Assert.That(game.Apply(0, new Command { action = "voiceState", flag = true }), Is.False); }
    }
}

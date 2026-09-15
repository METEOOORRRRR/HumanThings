using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EarthRecovery.Tests
{
    public sealed class LanConnectionTests
    {
        NetworkSession hostSession, clientSession;
        LanRooms host, client;
        [UnitySetUp] public IEnumerator Setup()
        {
            hostSession = new GameObject("LAN test host").AddComponent<NetworkSession>(); hostSession.PersistArchive = false;
            host = hostSession.gameObject.AddComponent<LanRooms>(); host.Initialize(hostSession);
            clientSession = new GameObject("LAN test client").AddComponent<NetworkSession>(); clientSession.PersistArchive = false;
            client = clientSession.gameObject.AddComponent<LanRooms>(); client.Initialize(clientSession); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            clientSession.Leave(); hostSession.Leave(); yield return new WaitForSecondsRealtime(.3f);
            Object.Destroy(clientSession.gameObject); Object.Destroy(hostSession.gameObject); yield return null;
        }
        [UnityTest] public IEnumerator DeveloperSoloHostCanReadyAndStartThroughSession()
        {
            hostSession.rules.developerSolo = true;
            Assert.That(host.Create("Solo test", 4, false, ""));
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(hostSession.Online);
            Assert.That(hostSession.StartMission(42), Is.False);
            hostSession.Send(new Command { action = "ready", flag = true });
            Assert.That(hostSession.StartMission(42), Is.True);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(hostSession.View.phase, Is.EqualTo(Phase.Expedition));
            Assert.That(hostSession.View.players.Count, Is.EqualTo(1));
            Assert.That(hostSession.rules.minPlayers, Is.EqualTo(4));
        }
        [UnityTest] public IEnumerator NewMissionHintsReplicateToFieldClient()
        {
            hostSession.rules.minPlayers = 2;
            Assert.That(host.Create("Hint test", 4, false, ""));
            yield return new WaitForSecondsRealtime(.5f);
            var room = host.Hosted; room.address = "127.0.0.1";
            Assert.That(client.Join(room, "")); yield return new WaitForSecondsRealtime(1);
            hostSession.Send(new Command { action = "ready", flag = true });
            clientSession.Send(new Command { action = "ready", flag = true });
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(hostSession.StartMission(42));
            hostSession.HostGame.Player(clientSession.LocalId).position = new Vector3(60, 0, 60);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(clientSession.View.phase, Is.EqualTo(Phase.Expedition));
            Assert.That(SnapshotValidation.Valid(clientSession.View, clientSession.LocalId));
            foreach (var site in clientSession.View.sites)
            {
                CollectionAssert.AreEqual(Catalog.MissionLines(hostSession.View.sites[site.id]), Catalog.MissionLines(site));
                Assert.That(site.displayName, Is.Empty);
            }
        }
        [UnityTest] public IEnumerator PublicDiscoveryJoinAndLeave()
        {
            Assert.That(host.Create("공개 검증", 4, true, "")); yield return new WaitForSecondsRealtime(.4f);
            Assert.That(hostSession.Online); client.BeginSearch(); yield return new WaitForSecondsRealtime(.7f);
            var room = client.Rooms.FirstOrDefault(r => r.id == host.Hosted.id); Assert.That(room, Is.Not.Null);
            room.address = "127.0.0.1"; Assert.That(client.Join(room, "")); Assert.That(clientSession.Online, Is.False);
            yield return new WaitForSecondsRealtime(1); Assert.That(clientSession.Online); Assert.That(hostSession.View.players.Count, Is.EqualTo(2));
            hostSession.Leave(); yield return new WaitForSecondsRealtime(1); Assert.That(clientSession.Online, Is.False);
        }
        [UnityTest] public IEnumerator DifferentRegionIsAdvertisedAndReplicatedInsteadOfDefault()
        {
            var original = ExpeditionRegions.All;
            var field = typeof(ExpeditionRegions).GetField("entries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var fixture = new ExpeditionRegion { id = "test-region", name = "검증 지역", title = "검증", description = "검증", image = original[0].image };
            field.SetValue(null, original.Concat(new[] { fixture }).ToArray());
            try
            {
                Assert.That(host.Create("지역 검증", 4, true, "", fixture.id)); yield return new WaitForSecondsRealtime(.4f);
                Assert.That(host.Hosted.regionId, Is.EqualTo(fixture.id)); Assert.That(host.Hosted.region, Is.EqualTo(fixture.name));
                client.BeginSearch(); yield return new WaitForSecondsRealtime(.7f);
                var room = client.Rooms.First(r => r.id == host.Hosted.id); room.address = "127.0.0.1";
                Assert.That(client.Join(room, "")); yield return new WaitForSecondsRealtime(1);
                Assert.That(clientSession.Online); Assert.That(clientSession.View.regionId, Is.EqualTo(fixture.id));
                hostSession.HostGame.ResetLobby(); yield return new WaitForSecondsRealtime(.3f);
                Assert.That(clientSession.View.regionId, Is.EqualTo(fixture.id));
            }
            finally { clientSession.Leave(); hostSession.Leave(); field.SetValue(null, original); }
        }
        [UnityTest] public IEnumerator PrivateRoomHiddenButCodeResolvesAndPasswordIsEnforced()
        {
            Assert.That(host.Create("비공개 검증", 4, false, "123456")); yield return new WaitForSecondsRealtime(.4f);
            client.BeginSearch(); yield return new WaitForSecondsRealtime(.5f); Assert.That(client.Rooms.Any(r => r.id == host.Hosted.id), Is.False);
            client.StopSearch(); LanRoom resolved = null; client.Resolved += r => resolved = r;
            client.Resolve(LanRooms.EncodeCode("127.0.0.1", host.Hosted.port, host.Hosted.id)); yield return new WaitForSecondsRealtime(.5f);
            Assert.That(resolved, Is.Not.Null); Assert.That(client.Join(resolved, "wrong")); yield return new WaitForSecondsRealtime(1);
            Assert.That(clientSession.Online, Is.False); Assert.That(clientSession.CanConnect);
            Assert.That(client.Join(resolved, "123456")); yield return new WaitForSecondsRealtime(1); Assert.That(clientSession.Online);
        }
        [UnityTest] public IEnumerator StaleRoomIdentityRejectedAfterRestart()
        {
            Assert.That(host.Create("이전 방", 4, false, "")); yield return new WaitForSecondsRealtime(.4f);
            var stale = host.Hosted; stale.address = "127.0.0.1";
            hostSession.Leave(); yield return new WaitForSecondsRealtime(.5f);
            Assert.That(host.Create("새 방", 4, false, "")); yield return new WaitForSecondsRealtime(.4f);
            Assert.That(client.Join(stale, "")); yield return new WaitForSecondsRealtime(1); Assert.That(clientSession.Online, Is.False);
        }
    }
}

using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EarthRecovery.Tests
{
    public sealed class WorldTests
    {
        Scene scene;
        NetworkSession session;
        NetworkSession client;
        [UnitySetUp] public IEnumerator Setup()
        {
            scene = SceneManager.CreateScene("EarthRecoveryPhysicsTest"); SceneManager.SetActiveScene(scene);
            var startup = new GameObject("Test bootstrap"); startup.AddComponent<Bootstrap>();
            yield return null;
            session = Object.FindFirstObjectByType<NetworkSession>();
            session.world.Automated = true;
            session.rules.minPlayers=2; session.PersistArchive=false;
            Assert.That(session.Connect(true, "127.0.0.1", 17991));
            yield return null;
            Assert.That(session.HostGame.Join(41, "Test field"));
            session.HostGame.Apply(0, new Command { action = "ready", flag = true });
            session.HostGame.Apply(41, new Command { action = "ready", flag = true });
            Assert.That(session.StartMission(99));
            yield return null; yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (client != null)
            {
                client.Leave();
                float deadline = Time.realtimeSinceStartup + 4;
                while (!client.CanConnect && Time.realtimeSinceStartup < deadline) yield return null;
                Object.Destroy(client.gameObject);
            }
            if (session != null) session.Leave();
            // NetworkManager persists its root; destroy that test root explicitly.
            if (session != null) Object.Destroy(session.gameObject);
            yield return null;
            yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest] public IEnumerator ThirdPersonShowsLocalBodyAndFollowsBehind()
        {
            yield return null;
            var body = GameObject.Find("Agent " + session.LocalId);
            Assert.That(body, Is.Not.Null);
            Assert.That(body.GetComponentsInChildren<Renderer>().All(r => r.enabled));
            var focus = body.transform.position + Vector3.up * 1.45f;
            Assert.That(Vector3.Distance(session.world.eye.transform.position, focus), Is.GreaterThan(1));
            Assert.That(Vector3.Dot(session.world.eye.transform.forward, (focus-session.world.eye.transform.position).normalized), Is.GreaterThan(.99f));
        }
        [UnityTest] public IEnumerator CameraBoomIgnoresTriggersButStopsBeforeSolidWalls()
        {
            var focus = new Vector3(0, 30, 0);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer = 8;
            wall.transform.position = focus + Vector3.back * 2;
            wall.transform.localScale = new Vector3(10, 10, .4f);
            var collider = wall.GetComponent<Collider>(); collider.isTrigger = true;
            Physics.SyncTransforms();
            Assert.That(Vector3.Distance(focus, WorldView.ResolveCameraPosition(focus, Quaternion.identity)), Is.EqualTo(3.6f).Within(.01f));
            collider.isTrigger = false; Physics.SyncTransforms();
            var camera = WorldView.ResolveCameraPosition(focus, Quaternion.identity);
            Assert.That(Vector3.Distance(focus, camera), Is.InRange(1.5f, 1.6f));
            Assert.That(Physics.CheckSphere(camera, .2f, 1 << 8, QueryTriggerInteraction.Ignore), Is.False);
            Object.Destroy(wall); yield return null;
        }
        [UnityTest] public IEnumerator CameraStaysAboveFloorAndInsideCampAtCloseWalls()
        {
            foreach (var at in new[] { new Vector3(0, 0, -6.4f), new Vector3(6.4f, 0, 0), new Vector3(-6.4f, 0, 0), new Vector3(0, 0, 6.4f) })
                foreach (float angle in new[] { 0f, 90f, 180f, 270f })
                    foreach (float tilt in new[] { -30f, 18f, 65f })
                    {
                        var focus = at + Vector3.up * 1.45f;
                        var camera = WorldView.ResolveCameraPosition(focus, Quaternion.Euler(tilt, angle, 0));
                        Assert.That(camera.y, Is.GreaterThan(.15f));
                        Assert.That(Physics.CheckSphere(camera, .19f, 1 << 8, QueryTriggerInteraction.Ignore), Is.False, camera.ToString());
                    }
            yield return null;
        }
        [UnityTest] public IEnumerator SpectatorCameraFollowsSurvivorWithoutErrors()
        {
            session.HostGame.Kill(session.HostGame.Player(session.LocalId), "Camera test");
            float deadline = Time.realtimeSinceStartup + 2;
            while (session.LocalPlayer.alive && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(session.LocalPlayer.alive, Is.False);
            yield return null; yield return null;
            var target = session.HostGame.Player(41);
            Assert.That(Vector3.Distance(session.world.eye.transform.position, target.position), Is.LessThan(6));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator AllFifteenConsolePopupsOpenWithoutModules()
        {
            var player=session.HostGame.Player(session.LocalId);
            var body=GameObject.Find("Agent "+session.LocalId);
            var controller=body.GetComponent<CharacterController>();
            foreach(var s in session.HostGame.State.sites)
            {
                controller.enabled=false;body.transform.position=s.position;player.position=s.position;controller.enabled=true;
                yield return new WaitForSeconds(.2f);
                Assert.That(session.world.TryOpenFacilityConsole(),Is.True,Catalog.Sites[s.id]);
                Assert.That(session.world.hud.SiteOpen,Is.EqualTo(s.id));
                Assert.That(player.modules,Is.Empty);session.world.hud.ClosePanels();
            }
        }
        [UnityTest] public IEnumerator AllMonstersPatrolWithoutAnyPlayerOutsideCamp()
        {
            var game=session.HostGame;
            foreach(var m in game.State.monsters) m.nextMemory=float.MaxValue;
            var start=game.State.monsters.ToDictionary(m=>m.id,m=>m.position);
            yield return new WaitForSeconds(8);
            foreach(var m in game.State.monsters)
            {
                Assert.That(Vector3.Distance(start[m.id],m.position),Is.GreaterThan(2),m.kind.ToString());
                Assert.That(WorldGeometry.Safe(game.State,m.position),Is.False);
                Assert.That(m.alert,Is.Not.EqualTo(AlertState.Chase));
            }
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator PatrolRecoversFromUnreachableDestination()
        {
            var game=session.HostGame;var m=game.State.monsters.First(x=>x.kind==MonsterKind.Sound);
            m.nextMemory=float.MaxValue;
            yield return new WaitForSeconds(1);
            m.destination=new Vector3(game.State.mapSize.x,0,game.State.mapSize.y);m.alert=AlertState.Return;
            yield return new WaitForSeconds(8);
            Assert.That(session.Online,Is.True);
            Assert.That(Mathf.Abs(m.destination.x),Is.LessThan(game.State.mapSize.x / 2));
            Assert.That(Mathf.Abs(m.destination.z),Is.LessThan(game.State.mapSize.y / 2));
            Assert.That(m.alert,Is.EqualTo(AlertState.Idle));
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator MissingRoomNeverBecomesOnlineAndEventuallyReturnsToJoinScreen()
        {
            client=new GameObject("Missing room client").AddComponent<NetworkSession>();
            Assert.That(client.Connect(false,"127.0.0.1",18991));
            Assert.That(client.IsConnecting);Assert.That(client.Online,Is.False);Assert.That(client.LocalPlayer,Is.Null);
            float deadline=Time.realtimeSinceStartup+NetworkSession.ConnectionTimeoutSeconds+3;
            while(!client.CanConnect && Time.realtimeSinceStartup<deadline)
            {
                Assert.That(client.Online,Is.False);Assert.That(client.LocalPlayer,Is.Null);
                client.Send(new Command{action="ready",flag=true});
                yield return null;
            }
            Assert.That(client.CanConnect);Assert.That(client.IsConnecting,Is.False);
            Assert.That(client.Status,Does.Contain("방"));LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator PendingJoinCanBeCancelledThenReconnected()
        {
            client=new GameObject("Pending join client").AddComponent<NetworkSession>();
            Assert.That(client.Connect(false,"127.0.0.1",18992));
            Assert.That(client.Online,Is.False);Assert.That(client.Connect(false,"127.0.0.1",17991),Is.False);
            client.Leave();
            float deadline=Time.realtimeSinceStartup+4;
            while(!client.CanConnect && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(client.CanConnect);session.HostGame.ResetLobby();
            Assert.That(client.Connect(false,"127.0.0.1",17991));Assert.That(client.Online,Is.False);
            deadline=Time.realtimeSinceStartup+5;
            while(!client.Online && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(client.Online);Assert.That(client.IsConnecting,Is.False);Assert.That(client.LocalPlayer,Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator RejectedJoinNeverShowsOnlineRoom()
        {
            client=new GameObject("Rejected join client").AddComponent<NetworkSession>();
            Assert.That(client.Connect(false,"127.0.0.1",17991));
            float deadline=Time.realtimeSinceStartup+5;
            while(!client.CanConnect && Time.realtimeSinceStartup<deadline)
            { Assert.That(client.Online,Is.False);yield return null; }
            Assert.That(client.CanConnect);Assert.That(client.LocalPlayer,Is.Null);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator GeneratedWorldSignsUseAllFifteenApprovedNames()
        {
            var signs=Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Select(t=>t.text)
                .Concat(Object.FindObjectsByType<TMPro.TextMeshPro>(FindObjectsSortMode.None).Select(t=>t.text.Trim('[',']'))).ToArray();
            foreach(var name in Catalog.Sites) Assert.That(signs,Does.Contain(name));
            foreach(var old in new[]{"병원/약국","학교/교실","인쇄소/소형 공장","식당/주방용품점"})
                Assert.That(signs,Does.Not.Contain(old));
            yield return null;
        }
        [UnityTest] public IEnumerator SafeInteriorsExcludedAndAllEntrancesConnected()
        {
            var state = session.HostGame.State;
            Assert.That(NavMesh.SamplePosition(Vector3.zero, out _, .2f, NavMesh.AllAreas), Is.False);
            foreach (var site in state.sites)
            {
                Assert.That(NavMesh.SamplePosition(site.position, out _, .2f, NavMesh.AllAreas), Is.False, Catalog.Sites[site.id]);
                Assert.That(NavMesh.SamplePosition(WorldGeometry.Entrance(site), out var entrance, 2, NavMesh.AllAreas));
                Assert.That(NavMesh.SamplePosition(new Vector3(0, 0, -10), out var start, 2, NavMesh.AllAreas));
                var path = new NavMeshPath(); NavMesh.CalculatePath(start.position, entrance.position, NavMesh.AllAreas, path);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), Catalog.Sites[site.id]);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator LeaveIsImmediateIdempotentAndCanRehostRepeatedly()
        {
            for (int cycle = 0; cycle < 3; cycle++)
            {
                session.world.hud.CampOpen = true;
                session.Leave(); session.Leave();
                Assert.That(session.Online, Is.False);
                Assert.That(session.IsHost, Is.False);
                Assert.That(session.HostGame, Is.Null);
                Assert.That(session.CanConnect, Is.False);
                Assert.That(session.Connect(true, "127.0.0.1", 17991), Is.False);
                Assert.That(session.world.hud.CampOpen, Is.False);
                session.Send(new Command { action = "ready", flag = true });
                session.SendVoice(new byte[800]);
                float deadline = Time.realtimeSinceStartup + 4;
                while (!session.CanConnect && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(session.CanConnect, Is.True);
                Assert.That(session.Connect(true, "127.0.0.1", 17991), Is.True);
                yield return null;
                Assert.That(session.LocalPlayer, Is.Not.Null);
                Assert.That(session.View.phase, Is.EqualTo(Phase.Lobby));
            }
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator TransportShutdownClearsStateAndAllowsRehost()
        {
            session.Manager.Shutdown();
            Assert.That(session.Online, Is.False);
            Assert.That(session.CanConnect, Is.False);
            float deadline = Time.realtimeSinceStartup + 4;
            while (!session.CanConnect && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(session.CanConnect, Is.True);
            Assert.That(session.HostGame, Is.Null);
            Assert.That(session.View.players, Is.Empty);
            Assert.That(session.Connect(true, "127.0.0.1", 17991));
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator HostLeavingDisconnectsClientAndBothCanReconnect()
        {
            session.HostGame.ResetLobby();
            client = new GameObject("Lifecycle client").AddComponent<NetworkSession>();
            Assert.That(client.Connect(false, "127.0.0.1", 17991));
            float deadline = Time.realtimeSinceStartup + 5;
            while (client.LocalPlayer == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(client.LocalPlayer, Is.Not.Null);
            session.Leave();
            deadline = Time.realtimeSinceStartup + 5;
            while ((!session.CanConnect || !client.CanConnect) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(session.CanConnect);
            Assert.That(client.CanConnect);
            Assert.That(client.View.players, Is.Empty);
            Assert.That(session.Connect(true, "127.0.0.1", 17991));
            Assert.That(client.Connect(false, "127.0.0.1", 17991));
            deadline = Time.realtimeSinceStartup + 5;
            while (client.LocalPlayer == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(client.LocalPlayer, Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator ServerMovementCannotPassThroughCampWalls()
        {
            var game = session.HostGame; game.State.monsters.Clear();
            game.Apply(0, new Command { action = "move", move = Vector2.left, yaw = 0 });
            for (int i = 0; i < 250; i++) session.world.HostStep(game, .02f);
            Assert.That(game.Player(0).position.x, Is.GreaterThan(-6.6f));
            Assert.That(game.Player(0).position.x, Is.LessThan(-5.5f));
            Assert.That(game.Player(0).alive);
            yield return null;
        }
        [UnityTest] public IEnumerator SoundAggroWaitsOutsideWorkshop()
        {
            var game = session.HostGame;
            var site = game.State.sites[0];
            var monster = game.State.monsters.First(m => m.kind == MonsterKind.Sound);
            monster.position = WorldGeometry.Entrance(site);
            game.Noise(site.position, 30);
            Assert.That(monster.aggroUntil, Is.GreaterThan(game.State.elapsed));
            Assert.That(WorldGeometry.Inside(site, monster.destination), Is.False);
            Assert.That(monster.destination, Is.EqualTo(WorldGeometry.Entrance(site)));
            yield return null;
        }
        [UnityTest] public IEnumerator InvalidEndpointDoesNotStartTransport()
        {
            session.Leave();
            float deadline = Time.realtimeSinceStartup + 4;
            while (!session.CanConnect && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(session.Connect(false, "not an address", 7777), Is.False);
            Assert.That(session.Connect(false, "127.0.0.1", 0), Is.False);
            Assert.That(session.CanConnect); session.Send(null);
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator MalformedCommandIsIgnoredAndInvalidSnapshotEndsClient()
        {
            session.HostGame.ResetLobby();
            client = new GameObject("Malformed snapshot client").AddComponent<NetworkSession>();
            Assert.That(client.Connect(false, "127.0.0.1", 17991));
            float deadline = Time.realtimeSinceStartup + 5;
            while (client.LocalPlayer == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(client.LocalPlayer, Is.Not.Null);
            using (var writer = new Unity.Netcode.FastBufferWriter(8, Unity.Collections.Allocator.Temp))
            {
                writer.WriteValueSafe(int.MaxValue);
                client.Manager.CustomMessagingManager.SendNamedMessage("command", Unity.Netcode.NetworkManager.ServerClientId, writer);
            }
            yield return null; yield return null;
            Assert.That(session.Online); Assert.That(client.Online);
            using (var writer = new Unity.Netcode.FastBufferWriter(8, Unity.Collections.Allocator.Temp))
            {
                writer.WriteValueSafe(50000);
                session.Manager.CustomMessagingManager.SendNamedMessage("snapshot", client.LocalId, writer);
            }
            deadline = Time.realtimeSinceStartup + 5;
            while (!client.CanConnect && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(client.CanConnect); Assert.That(client.View.players, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }
    }
}

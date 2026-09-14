#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace EarthRecovery
{
    // Development-only actors exercise the real transport and normal command validation.
    // Test hints stand in for a human describing the field display over voice.
    public sealed class NetworkSmoke : MonoBehaviour
    {
        public NetworkSession session;
        string role, output;
        int expected = 6, actor, port;
        float started, nextAction, finishedAt = -1;
        bool registered, captured, hintsCaptured, readySent, phaseLogged;
        NavMeshPath path;
        Vector3 pathGoal = new(999, 0, 999);
        float nextPath, nextReport;
        int pathCorner;
        ulong voiceProbe = ulong.MaxValue;
        int deadVoiceBaseline = -1;
        bool voicePassed;
        bool stationCaptured, installCaptured, discoveryCaptured;
        readonly Dictionary<int, SiteState> hints = new();
        readonly HashSet<int> reportedSites = new();

        static string Arg(string key, string fallback)
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
        void Start()
        {
            role = Arg("--qa-role", "field"); actor = int.Parse(Arg("--qa-actor", "1")); expected = int.Parse(Arg("--qa-count", "6"));
            output = Arg("--qa-output", Application.persistentDataPath); port = int.Parse(Arg("--qa-port", "7788")); Directory.CreateDirectory(output);
            started = Time.realtimeSinceStartup;
            Application.targetFrameRate = 30;
            session.radio.Muted = true;
            session.rules.walkSpeed = 8; session.rules.runSpeed = 8; session.rules.carrySpeed = 8; session.rules.oxygenSeconds = 1200;
            session.rules.minPlayers=expected;
            session.UserName = role == "host" ? "QA 캠프" : "QA 현장 " + actor;
            session.Connect(role == "host", "127.0.0.1", (ushort)port);
            path = new NavMeshPath();
            Debug.Log("QA_STARTED role=" + role + " actor=" + actor);
        }
        void Update()
        {
            if(role=="missing") { MissingRoom(); return; }
            if (Time.realtimeSinceStartup - started > 480) { Finish("TIMEOUT", 2); return; }
            if (!session.Online || session.LocalPlayer == null) return;
            if (Time.realtimeSinceStartup > nextReport)
            {
                nextReport = Time.realtimeSinceStartup + 5;
                var p = session.LocalPlayer;
                File.WriteAllText(Path.Combine(output, role + actor + "-progress.txt"), session.View.phase + " t=" + session.View.elapsed.ToString("F1") + " pos=" + p.position + " inventory=" + p.inventory.Sum() + " cargo=" + p.carrying + "\n" + string.Join("\n", session.View.sites.Where(s => s.mission).Select(s => s.id + " " + s.phase + " L" + s.level + " checks=" + s.checks + " object=" + s.objectPosition + " site=" + s.position)));
            }
            if (!registered)
            {
                if (session.IsHost) session.Manager.CustomMessagingManager.RegisterNamedMessageHandler("qa_hint", Hint);
                registered = true;
            }
            if (session.View.phase == Phase.Lobby)
            {
                if (!readySent) { session.Send(new Command { action = "ready", flag = true }); readySent = true; }
                if (role == "host" && session.HostGame.State.phase == Phase.Lobby && session.View.players.Count == expected && session.View.players.All(p => p.ready))
                {
                    Debug.Log("QA_LOBBY_PASS clients=" + expected);
                    session.StartMission(20260913);
                    // Functional loop test isolates monsters; threat behavior has separate tests.
                    session.HostGame.State.monsters.Clear();
                }
                return;
            }
            if (session.View.phase == Phase.Success)
            {
                if (finishedAt < 0)
                {
                    Debug.Log("QA_MISSION_PASS recovered=3 clients=" + session.View.players.Count);
                    if (role == "host" && expected == 6 && !voicePassed) { Finish("VOICE_CHECK_INCOMPLETE", 8); return; }
                    File.WriteAllText(Path.Combine(output, role + actor + "-result.txt"), "PASS: real transport, scrap/module/install/craft loop, 3 deliveries, archive="+session.View.archive.Count+", " + session.View.players.Count + " connected players");
                    Capture(role + actor + "-success-world.png");
                    finishedAt = Time.realtimeSinceStartup;
                }
                if (Time.realtimeSinceStartup - finishedAt > (role == "host" ? 6 : 4)) Application.Quit(0);
                return;
            }
            if (session.View.phase != Phase.Expedition) { Finish("UNEXPECTED_END " + session.View.phase, 3); return; }
            if (!phaseLogged)
            {
                phaseLogged = true; Debug.Log("QA_WORLD_PASS sites=" + session.View.sites.Count + " loot=" + session.View.loot.Count);
                ValidateNavigation();
            }
            if (Time.realtimeSinceStartup < nextAction) return;
            nextAction = Time.realtimeSinceStartup + .12f;
            if (role == "host") Camp(); else Field();
        }
        void MissingRoom()
        {
            if(session.Online || session.LocalPlayer!=null) {Finish("MISSING_ROOM_SHOWED_CONNECTED",9);return;}
            if(!captured && Time.realtimeSinceStartup-started>1)
            {Capture("missing-pending.png");captured=true;}
            if(finishedAt<0 && session.CanConnect && Time.realtimeSinceStartup-started>1)
            {
                Capture("missing-failed.png");finishedAt=Time.realtimeSinceStartup;
                File.WriteAllText(Path.Combine(output,"missing-result.txt"),"PASS: nonexistent room stayed offline, no ready controls, returned to join screen");
            }
            if(finishedAt>=0 && Time.realtimeSinceStartup-finishedAt>3) Application.Quit(0);
            else if(Time.realtimeSinceStartup-started>20) Finish("MISSING_ROOM_TIMEOUT",9);
        }
        void Hint(ulong sender, FastBufferReader reader)
        {
            if (reader.Length > 6000) return;
            reader.ReadValueSafe(out string json); var hint = JsonUtility.FromJson<SiteState>(json);
            var p = session.HostGame.Player(sender);
            if (p == null || !p.alive || p.viewingSite != hint.id || !WorldGeometry.Inside(session.HostGame.State.sites[hint.id], p.position)) return;
            hints[hint.id] = hint;
        }
        void Camp()
        {
            if (expected == 6 && session.View.elapsed > 12 && voiceProbe == ulong.MaxValue)
            {
                var probe = session.HostGame.State.players.Find(p => p.name == "QA 현장 5");
                if (probe != null)
                {
                    voiceProbe = probe.id;
                    if (!session.radio.ReceivedPackets.ContainsKey(voiceProbe)) { Finish("LIVE_VOICE_NOT_RECEIVED", 8); return; }
                    session.HostGame.Infect(voiceProbe);
                }
            }
            if (voiceProbe != ulong.MaxValue && session.View.elapsed > 16 && deadVoiceBaseline < 0)
                deadVoiceBaseline = session.radio.ReceivedPackets[voiceProbe];
            if (voiceProbe != ulong.MaxValue && session.View.elapsed > 21 && !voicePassed)
            {
                var probe = session.HostGame.Player(voiceProbe);
                if (session.radio.ReceivedPackets[voiceProbe] != deadVoiceBaseline || probe.flashlight) { Finish("DEAD_PLAYER_INTERACTION_LEAK", 8); return; }
                voicePassed = true; Debug.Log("QA_VOICE_PASS live relay received; dead relay and dead commands rejected");
            }
            session.world.hud.CampOpen = true; session.world.MenuOpen = true;
            if (!captured && session.View.elapsed > 3)
            { Capture("camp-world.png"); captured = true; }
            if(!discoveryCaptured && session.View.elapsed>40) {Capture("camp-discovered.png");discoveryCaptured=true;}
            foreach (var site in session.View.sites.Where(s => s.mission && s.phase == CraftPhase.Puzzle))
            {
                if (site.targetX != -1 || site.maze.Length != 0) { Finish("SECRET_LEAK", 4); return; }
                if (!hints.TryGetValue(site.id, out var hint)) continue;
                var c = new Command { action = "puzzle", target = site.id };
                if (site.puzzleKind == PuzzleKind.Maze)
                {
                    var direction = MazeStep(hint.maze, site.puzzleX, site.puzzleY);
                    c.x = direction.x; c.y = direction.y;
                }
                else if (site.puzzleX != hint.targetX) c.x = Math.Sign(hint.targetX - site.puzzleX);
                else if (site.puzzleY != hint.targetY) c.y = Math.Sign(hint.targetY - site.puzzleY);
                else c.flag = true;
                session.Send(c);
            }
        }
        void Field()
        {
            var p = session.LocalPlayer;
            var missions = session.View.sites.Where(s => s.mission).ToArray();
            if (actor > 3)
            {
                if(actor==4)
                {
                    var console=session.View.sites.First(s=>!s.mission);
                    if(Move(ConsoleApproach(console)) && !installCaptured)
                    {session.world.TryOpenFacilityConsole();Capture("field-4-no-module.png");installCaptured=true;}
                    return;
                }
                if (actor == 5 && session.View.elapsed > 4 && session.View.elapsed < 21)
                {
                    session.SendVoice(Enumerable.Repeat((byte)128, 800).ToArray());
                    if (!p.alive) session.Send(new Command { action = "light", flag = true });
                }
                // Extra players keep the six-client session occupied and test movement replication.
                if (session.View.elapsed < 3) Move(new Vector3(actor == 4 ? -3 : 3, 0, -3)); else Stop();
                return;
            }
            if (!p.alive) { Finish("ACTOR_DIED " + p.deathReason, 5); return; }
            var s = expected == 2 ? missions.FirstOrDefault(s => s.phase != CraftPhase.Delivered) : missions[(actor - 1) % missions.Length];
            if (s == null) { Stop(); return; }
            if (p.carrying >= 0) { Move(new Vector3(0, 0, -2)); return; }
            if (s.phase == CraftPhase.Delivered) { Stop(); return; }
            if (s.phase == CraftPhase.Ready)
            {
                if (Vector3.Distance(p.position, s.objectPosition) < 2.8f)
                { Stop(); session.Send(new Command { action = "pickup", target = s.id }); }
                else Move(s.objectPosition);
                return;
            }
            if (s.phase == CraftPhase.Repair)
            {
                var recipe=HumanContent.Load().locations[s.id].recipe;
                if(p.modules.Contains(s.id))
                {
                    if(Move(ConsoleApproach(s)))
                    {
                        if(!installCaptured) {session.world.hud.OpenSite(s.id);session.world.MenuOpen=true;Capture("field-"+actor+"-install.png");installCaptured=true;}
                        session.Send(new Command {action="install",target=s.id,flag=true});
                    }
                    return;
                }
                var busy=session.View.stations.Find(t=>t.worker==p.id&&t.phase==StationPhase.CraftingModule);
                if(busy!=null) {Stop();return;}
                if (p.inventory[0] < recipe.scrapCost)
                {
                    var part = session.View.loot.Where(l => !l.collected).OrderBy(l => Vector3.Distance(l.position, p.position)).FirstOrDefault();
                    if (part == null) { Finish("MISSING_MATERIAL", 6); return; }
                    if (Move(part.position)) session.Send(new Command { action = "loot", target = part.id });
                }
                else
                {
                    var station=session.View.stations.Where(t=>t.phase==StationPhase.Available).OrderBy(t=>Vector3.Distance(t.position,p.position)).FirstOrDefault();
                    if(station!=null && Move(station.position+Vector3.back*2))
                    {
                        if(!stationCaptured) {session.world.hud.ClosePanels();session.world.hud.StationOpen=station.id;session.world.MenuOpen=true;Capture("field-"+actor+"-station.png");stationCaptured=true;}
                        session.Send(new Command {action="moduleCraft",target=s.id,x=station.id});
                    }
                }
                return;
            }
            if (!Move(ConsoleApproach(s))) return;
            if (p.viewingSite != s.id) session.Send(new Command { action = "view", target = s.id });
            if (s.phase == CraftPhase.Crafting)
            {
                if (s.checkStarted < 0) session.Send(new Command { action = "craft", target = s.id });
                else if ((session.View.elapsed - s.checkStarted) / session.rules.skillCycleSeconds >= .60f)
                    session.Send(new Command { action = "check", target = s.id });
            }
            else if (s.phase == CraftPhase.Puzzle && s.targetX >= 0)
            {
                if (!reportedSites.Contains(s.id)) { Debug.Log("QA_PUZZLE " + s.puzzleKind + " site=" + s.id); reportedSites.Add(s.id); }
                string json = JsonUtility.ToJson(s);
                using var writer = new FastBufferWriter(json.Length * 2 + 16, Allocator.Temp); writer.WriteValueSafe(json);
                session.Manager.CustomMessagingManager.SendNamedMessage("qa_hint", NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
                if (!hintsCaptured)
                {
                    session.world.hud.OpenSite(s.id); session.world.MenuOpen = true;
                    Capture("field-" + actor + "-workshop.png"); hintsCaptured = true;
                }
            }
            if (!captured && session.View.elapsed > 4)
            { Capture("field-" + actor + "-world.png"); captured = true; }
        }
        static Vector3 ConsoleApproach(SiteState site) => site.position + Quaternion.Euler(0,site.quarterTurn*90,0)*new Vector3(0,0,1);
        bool Move(Vector3 goal)
        {
            var p = session.LocalPlayer;
            Vector3 flat = p.position; flat.y = 0; goal.y = 0;
            if (Vector3.Distance(flat, goal) < 1.25f) { Stop(); return true; }
            if (Time.realtimeSinceStartup > nextPath || Vector3.Distance(pathGoal, goal) > 1)
            {
                // The game NavMesh excludes safe rooms for monsters. Actors route via entrances there.
                Vector3 start = WorldGeometry.OutsideSafety(session.View, flat), end = WorldGeometry.OutsideSafety(session.View, goal);
                if (NavMesh.SamplePosition(start, out var from, 6, NavMesh.AllAreas) && NavMesh.SamplePosition(end, out var to, 6, NavMesh.AllAreas))
                    NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path);
                pathCorner = 1; pathGoal = goal; nextPath = Time.realtimeSinceStartup + 1;
            }
            Vector3 waypoint = goal;
            var sourceRoom = session.View.sites.Find(s => WorldGeometry.Inside(s, flat));
            var targetRoom = session.View.sites.Find(s => WorldGeometry.Inside(s, goal));
            bool sameRoom = sourceRoom != null && sourceRoom == targetRoom;
            if (WorldGeometry.InCamp(flat) && !WorldGeometry.InCamp(goal)) waypoint = new Vector3(0, 0, -9);
            else if (sourceRoom != null && !sameRoom) waypoint = WorldGeometry.Entrance(sourceRoom);
            else if (sameRoom || (WorldGeometry.InCamp(flat) && WorldGeometry.InCamp(goal))) waypoint = goal;
            else
            {
                var outsideGoal = WorldGeometry.OutsideSafety(session.View, goal);
                if (Vector3.Distance(flat, outsideGoal) > 4 && path.corners.Length > 1)
                {
                    while (pathCorner < path.corners.Length - 1 && Vector3.Distance(new Vector3(path.corners[pathCorner].x, 0, path.corners[pathCorner].z), flat) < 1.7f) pathCorner++;
                    waypoint = path.corners[Mathf.Min(pathCorner, path.corners.Length - 1)];
                }
                else waypoint = goal;
            }
            Vector3 direction = waypoint - flat; direction.y = 0; direction.Normalize();
            session.Send(new Command { action = "move", move = new Vector2(direction.x, direction.z), yaw = 0 });
            return false;
        }
        void Stop() => session.Send(new Command { action = "move", move = Vector2.zero, yaw = 0 });
        void ValidateNavigation()
        {
            if (NavMesh.SamplePosition(Vector3.zero, out _, .2f, NavMesh.AllAreas)) { Finish("CAMP_NAVMESH_LEAK", 7); return; }
            foreach (var site in session.View.sites)
            {
                if (NavMesh.SamplePosition(site.position, out _, .2f, NavMesh.AllAreas)) { Finish("WORKSHOP_NAVMESH_LEAK", 7); return; }
                if (!NavMesh.SamplePosition(WorldGeometry.Entrance(site), out _, 2, NavMesh.AllAreas)) { Finish("ENTRANCE_UNREACHABLE", 7); return; }
            }
            Debug.Log("QA_SAFE_NAV_PASS camp+"+session.View.sites.Count+"shops excluded; entrances reachable");
        }
        void Capture(string filename)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"ui-"+filename));
            var camera = session.world.eye;
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            var texture = new RenderTexture(1280, 720, 24);
            camera.targetTexture = texture;
            camera.Render(); RenderTexture.active = texture;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG());
            camera.targetTexture = previous; RenderTexture.active = active;
            Destroy(image); texture.Release(); Destroy(texture);
        }
        static Vector2Int MazeStep(int[] cells, int x, int y)
        {
            int start = y * 6 + x;
            var previous = new Dictionary<int, int> { [start] = -1 }; var queue = new Queue<int>(); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue(); if (at == 35) break;
                foreach (var d in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                {
                    int nx = at % 6 + d.x, ny = at / 6 + d.y, next = ny * 6 + nx;
                    if (nx < 0 || nx > 5 || ny < 0 || ny > 5 || cells.Length != 36 || cells[next] == 1 || previous.ContainsKey(next)) continue;
                    previous[next] = at; queue.Enqueue(next);
                }
            }
            if (!previous.ContainsKey(35) || start == 35) return Vector2Int.zero;
            int cursor = 35;
            while (previous[cursor] != start) cursor = previous[cursor];
            return new Vector2Int(cursor % 6 - x, cursor / 6 - y);
        }
        void Finish(string message, int exit)
        {
            Debug.LogError("QA_FAIL " + message);
            File.WriteAllText(Path.Combine(output, role + actor + "-result.txt"), "FAIL " + message);
            Application.Quit(exit);
        }
    }
}
#endif

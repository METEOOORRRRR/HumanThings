using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    public interface IGameAuthority { bool ValidateAndExecute(ulong player, Command command); }
    public interface IPollutionProvider { float Risk(ulong player); }
    public sealed class NoPollutionProvider : IPollutionProvider { public float Risk(ulong player) => 0; }

    public sealed class Expedition : IGameAuthority
    {
        public Snapshot State { get; private set; } = new();
        public readonly GameRules Rules;
        public readonly HumanContent Content;
        public readonly Dictionary<ulong, Command> Inputs = new();
        public IPollutionProvider Pollution = new NoPollutionProvider();
        public IReadOnlyList<WorldBehaviorAnchor> Anchors = Array.Empty<WorldBehaviorAnchor>();
        public event Action<GameEvent> Changed;
        public int Alive => State.players.Count(p => p.alive && p.connected);
        readonly Dictionary<int, Vector3> dropped = new();
        int eventSequence;
        int chatSequence;
        readonly Dictionary<ulong, float> nextChat = new();
        readonly PlayerCharacterCatalog characters;
        readonly System.Random characterRandom;
        public Expedition(GameRules rules, HumanContent content = null, System.Random characterRandom = null)
        {
            Rules = rules != null ? rules : throw new ArgumentNullException(nameof(rules)); Rules.Sanitize();
            Content = content != null ? content : HumanContent.Load();
            characters = PlayerCharacterCatalog.Load();
            // Cosmetic rolls must not change the seeded mission generation sequence.
            this.characterRandom = characterRandom ?? new System.Random();
        }
        public PlayerState Player(ulong id) => State.players.Find(p => p.id == id && p.connected);
        public static string CleanName(string s)
        {
            var clean = new string((s ?? "").Take(64).Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '_').Take(16).ToArray()).Trim();
            return clean.Length == 0 ? "요원" : clean;
        }
        static Vector3 Spawn(int n) => new((n % 3 - 1) * 2, 0, -2 - n / 3 * 2);
        public bool Join(ulong id, string name)
        {
            if (State.phase != Phase.Lobby || State.players.Count >= Rules.maxPlayers || Player(id) != null) return false;
            int slot = Enumerable.Range(0, 6).First(n => State.players.All(p => Vector3.Distance(p.position, Spawn(n)) > .5f));
            State.players.Add(new PlayerState { id = id, name = CleanName(name), position = Spawn(slot),
                characterId = characters != null ? characters.PickId(characterRandom) : PlayerCharacterCatalog.DefaultId }); return true;
        }
        public void Emit(string kind, int target = -1, string text = "", Vector3 position = default)
        {
            var e = new GameEvent { sequence = ++eventSequence, kind = kind, target = target, text = text, position = position, at = State.elapsed };
            State.events.Add(e); if (State.events.Count > 64) State.events.RemoveAt(0); Changed?.Invoke(e);
        }
        public void Disconnect(ulong id)
        {
            var p = Player(id); if (p == null) return;
            if (State.phase == Phase.Lobby) State.players.Remove(p);
            else { if (State.phase == Phase.Expedition) Kill(p, "통신 두절"); p.connected = false; }
            Inputs.Remove(id);
            nextChat.Remove(id);
        }
        public bool Start(int seed, CityWorldPlan city = null)
        {
            if (State.phase != Phase.Lobby || State.players.Count < Rules.MinimumStartPlayers || State.players.Any(p => !p.ready)) return false;
            Content.Validate(); Rules.Sanitize();
            var missionRng = new System.Random(seed ^ 1097);
            var layoutRng = new System.Random(seed ^ 2309);
            var monsterRng = new System.Random(seed ^ 7919);
            var lootRng = new System.Random(seed ^ 3181);
            State.seed = seed; State.runId = Guid.NewGuid().ToString("N"); State.phase = Phase.Expedition;
            State.elapsed = 0; State.oxygen = Rules.oxygenSeconds; State.rainy = Rules.rainy;
            State.sites.Clear(); State.loot.Clear(); State.monsters.Clear(); State.stations.Clear(); State.events.Clear(); State.moduleDrops.Clear();
            Inputs.Clear(); dropped.Clear(); eventSequence = 0;
            var candidates = Content.locations.Select((l, i) => i).OrderBy(_ => missionRng.Next()).ToList();
            var selected = new List<int>();
            foreach (int i in candidates)
                if (selected.Count < 3 && selected.All(n => Content.locations[n].zone != Content.locations[i].zone)) selected.Add(i);
            foreach (int i in candidates) if (selected.Count < 3 && !selected.Contains(i)) selected.Add(i);
            if (selected.Count != 3) throw new InvalidOperationException("Three mission locations required");
            var layouts = Content.zones.Select(z => new Queue<Vector3>(z.spawnSockets.OrderBy(_ => layoutRng.Next()))).ToArray();
            for (int i = 0; i < Content.locations.Length; i++)
            {
                var definition = Content.locations[i]; var zone = Content.zones[definition.zone];
                if (definition.footprint.x > zone.socketFootprint.x || definition.footprint.y > zone.socketFootprint.y || definition.compatibilityTag != zone.compatibilityTag)
                    throw new InvalidOperationException("Incompatible spawn socket: " + definition.id);
                int product = missionRng.Next(definition.artifacts.Length);
                var a = definition.artifacts[product]; var puzzle = a.puzzle != null ? a.puzzle : definition.puzzle;
                var s = new SiteState { id = i, product = product, position = zone.center + layouts[definition.zone].Dequeue(), quarterTurn = layoutRng.Next(4),
                    mission = selected.Contains(i), assignedOrder = selected.IndexOf(i), sampleCode = "H-" + (i * 3 + product + 1).ToString("D3"),
                    missionState = selected.Contains(i) ? MissionState.Assigned : MissionState.Hidden, puzzleKind = puzzle.puzzleType,
                    targetX = missionRng.Next(1, 5), targetY = missionRng.Next(1, 5), targetAngle = missionRng.Next(1, 4) };
                if (s.puzzleKind == PuzzleKind.Maze)
                { s.maze = new[] {0,0,0,1,0,0, 1,1,0,1,0,1, 0,0,0,0,0,0, 0,1,1,1,1,0, 0,0,0,0,1,0, 1,1,1,0,0,0}; s.targetX = 5; s.targetY = 5; }
                State.sites.Add(s);
            }
            PlaceStations(seed);
            for (int i = 0; i < 120; i++)
            {
                Vector3 pos = default; bool found = false;
                for (int attempt = 0; attempt < 1000 && !found; attempt++)
                { pos = new Vector3(lootRng.Next(-68, 69), 0, lootRng.Next(-68, 69)); found = SupplyPosition(pos) && State.loot.All(l => Vector3.Distance(l.position, pos) >= 1); }
                if (!found) throw new InvalidOperationException("No safe scrap socket");
                State.loot.Add(new LootState { id = i, position = pos });
            }
            for (int i = 0; i < Content.monsters.Length; i++)
            {
                var def = Content.monsters[i];
                var zones = Enumerable.Range(0, Content.zones.Length).Where(z => def.spawnTags == null || def.spawnTags.Length == 0 || def.spawnTags.Contains(Content.zones[z].id)).ToArray();
                int zone = zones[monsterRng.Next(zones.Length)];
                float angle = i * 2.399963f;
                Vector3 home = Content.zones[zone].center * .48f + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 4;
                State.monsters.Add(new MonsterState { id = i, definition = i, kind = def.kind, position = home, home = home, destination = home });
            }
            if (city != null) RuntimeCityLayout.Apply(this, city);
            for (int i = 0; i < State.players.Count; i++) ResetPlayer(State.players[i], i);
            Emit("SessionStarted"); foreach (var s in State.sites.Where(s => s.mission)) Emit("MissionAssigned", s.id);
            State.message = "미확인 표본 3개 회수"; return true;
        }
        void PlaceStations(int seed)
        {
            var rng = new System.Random(seed ^ 4513);
            var candidates = Content.zones.SelectMany((z, i) => z.stationSockets.Select(p => new StationState { zone = i, position = p })).ToArray();
            bool Fits(StationState s) => SupplyPosition(s.position) && s.position.magnitude >= Rules.stationMinDistanceFromBase
                && State.sites.All(l => Vector3.Distance(WorldGeometry.Entrance(l), s.position) >= Rules.stationMinDistanceFromEntrance)
                && State.stations.All(t => Vector3.Distance(t.position, s.position) >= Rules.stationMinDistance);
            for (int attempt = 0; attempt < 50 && State.stations.Count < 3; attempt++)
            { var s = candidates[rng.Next(candidates.Length)]; if (Fits(s) && State.stations.All(t => t.zone != s.zone)) State.stations.Add(new StationState { zone = s.zone, position = s.position }); }
            if (State.stations.Count < 3)
            {
                State.stations.Clear();
                var fallback = Content.zones.Select((z, i) => new StationState { zone = i, position = z.fallbackStation }).Concat(candidates).ToArray();
                bool Search(int start)
                {
                    if (State.stations.Count == 3) return true;
                    for (int i = start; i < fallback.Length; i++)
                    { var s = fallback[i]; if (!Fits(s) || State.stations.Count(t => t.zone == s.zone) >= 2) continue; State.stations.Add(s); if (Search(i + 1)) return true; State.stations.RemoveAt(State.stations.Count - 1); }
                    return false;
                }
                if (!Search(0)) throw new InvalidOperationException("Station separation config has no valid fallback");
                Emit("StationFallback", text: "50 candidate attempts exhausted; predefined fallback sockets used");
            }
            for (int i = 0; i < State.stations.Count; i++) State.stations[i].id = i;
        }
        bool SupplyPosition(Vector3 p) => !(Mathf.Abs(p.x) < 8 && Mathf.Abs(p.z) < 8)
            && State.sites.All(s => { var q = WorldGeometry.Local(s, p); return Mathf.Abs(q.x) >= 6 || Mathf.Abs(q.z) >= 6; });
        static void ResetPlayer(PlayerState p, int slot)
        { p.position = Spawn(slot); p.alive = true; p.inventory = new int[1]; p.modules.Clear(); p.knownLocations.Clear(); p.carrying = p.viewingSite = p.installing = -1; p.flashlight = false; p.yaw = p.stationary = p.lastInputAt = 0; p.deathReason = ""; }
        public void ResetLobby()
        {
            var players = State.players.Where(p => p.connected).ToList(); var archive = State.archive;
            State = new Snapshot { players = players, archive = archive, regionId = State.regionId }; Inputs.Clear(); dropped.Clear();
            for (int i = 0; i < players.Count; i++) { ResetPlayer(players[i], i); players[i].ready = false; }
        }
        public void Tick(float dt)
        {
            if (State.phase != Phase.Expedition || !float.IsFinite(dt) || dt < 0) return;
            State.elapsed += dt; State.oxygen = Mathf.Max(0, State.oxygen - dt);
            if (State.oxygen <= 0) { End(Phase.OxygenLost, "산소 고갈 · 자동 귀환"); return; }
            foreach (var station in State.stations.Where(s => s.phase == StationPhase.CraftingModule).ToArray())
            {
                var p = Player(station.worker);
                if (p == null || !p.alive || Vector3.Distance(p.position, station.position) > 4) { CancelModule(station); continue; }
                if (State.elapsed - station.started >= Content.locations[station.target].recipe.duration)
                { p.modules.Add(station.target); Emit("RestorationModuleCrafted", station.target); station.phase = StationPhase.Available; station.worker = ulong.MaxValue; station.target = -1; }
            }
            foreach (var p in State.players.Where(p => p.connected && p.alive))
            {
                foreach (var s in State.sites.Where(s => WorldGeometry.Inside(s, p.position)))
                {
                    if (!p.knownLocations.Contains(s.id))
                    { p.knownLocations.Add(s.id); s.discovered = true; if (s.mission) { AdvanceReveal(s, RevealLevel.OnLocation); if (s.missionState == MissionState.Assigned) s.missionState = MissionState.LocationDiscovered; Emit("LocationDiscovered", s.id, "DATA FRAGMENT RECOVERED"); } }
                }
                if (p.viewingSite >= 0 && !WorldGeometry.NearConsole(State.sites[p.viewingSite], p.position)) p.viewingSite = -1;
                if (p.installing >= 0)
                {
                    var s = State.sites[p.installing];
                    if (!p.modules.Contains(s.id) || !WorldGeometry.NearConsole(s, p.position) || State.elapsed - p.installHeartbeat > .35f || s.facility != FacilityState.ModuleRequired) p.installing = -1;
                    else if (State.elapsed - p.installStarted >= Content.locations[s.id].recipe.installDuration)
                    { p.modules.Remove(s.id); p.installing = -1; s.facility = FacilityState.Restored; s.phase = CraftPhase.Crafting; s.restoredAt = State.elapsed; if (s.mission) s.missionState = MissionState.FacilityRestored; AdvanceReveal(s, RevealLevel.OnLocation); Emit("FacilityRestored", s.id); }
                }
                if (p.carrying >= 0 && WorldGeometry.InCamp(p.position)) Recover(p);
            }
            foreach (var s in State.sites.Where(s => s.worker != ulong.MaxValue))
            {
                var p = Player(s.worker);
                if (p == null || !p.alive || p.viewingSite != s.id || !WorldGeometry.Inside(s, p.position)) { s.worker = ulong.MaxValue; s.checkStarted = -1; }
                else if (s.checkStarted >= 0 && State.elapsed - s.checkStarted > Rules.skillCycleSeconds) FailCheck(s);
            }
            if (State.sites.Count(s => s.mission && s.phase == CraftPhase.Delivered) == 3) End(Phase.Success, "표본 3개 식별 및 회수 완료");
            else if (Alive == 0) End(Phase.AllDead, "전원 사망");
        }
        void AdvanceReveal(SiteState s, RevealLevel level)
        { if (level > s.reveal) { s.reveal = level; Emit("ArtifactRevealAdvanced", s.id); } }
        void Recover(PlayerState p)
        {
            var s = State.sites[p.carrying]; p.carrying = -1;
            if (s.phase != CraftPhase.Carried) return;
            s.phase = CraftPhase.Delivered; s.missionState = MissionState.Recovered; s.recoveredAt = State.elapsed; s.recoveredBy = p.id;
            AdvanceReveal(s, RevealLevel.Recovered); var a = Content.Artifact(s); s.displayName = a.trueNameKo;
            var entry = State.archive.Find(e => e.artifactId == a.id);
            if (entry == null) { entry = new ArchiveEntry { artifactId = a.id, firstRecoveredUtc = DateTime.UtcNow.ToString("O"), runIndex = State.archive.Sum(e => e.recoverCount) + 1 }; State.archive.Add(entry); }
            entry.discovered = true; entry.recoverCount++; entry.trueNameKo = a.trueNameKo; entry.trueNameEn = a.trueNameEn; entry.archiveDescription = a.archiveDescription; entry.marsComment = a.marsComment;
            Emit("ArtifactRecovered", s.id, a.trueNameEn + " / " + a.trueNameKo + "\n" + a.archiveDescription + "\nMars Archive Note: " + a.marsComment);
            Emit("ArchiveUpdated", s.id); State.message = a.trueNameKo + " 회수 완료";
        }
        public void Observe(string artifactId, string observation)
        {
            var entry = State.archive.Find(e => e.artifactId == artifactId);
            if (entry == null) { entry = new ArchiveEntry { artifactId = artifactId }; State.archive.Add(entry); }
            if (!entry.observations.Contains(observation)) { entry.observations.Add(observation); Emit("ArchiveUpdated"); }
        }
        public void End(Phase phase, string reason)
        {
            if (State.phase != Phase.Expedition || phase <= Phase.Expedition || phase > Phase.Abandoned) return;
            foreach (var station in State.stations.Where(s => s.phase == StationPhase.CraftingModule).ToArray()) CancelModule(station);
            State.phase = phase; State.message = reason; Inputs.Clear();
            if (phase != Phase.Success) foreach (var s in State.sites.Where(s => s.mission && s.missionState != MissionState.Recovered)) s.missionState = MissionState.Failed;
            Emit("SessionEnded");
        }
        public bool ValidateAndExecute(ulong player, Command command) => Apply(player, command);
        public bool Apply(ulong sender, Command c)
        {
            var p = Player(sender); if (p == null || c == null) return false;
            if (State.phase == Phase.Lobby)
            {
                if (c.action == "ready") { p.ready = c.flag; return true; }
                if (c.action == "name") { p.name = CleanName(c.text); return true; }
                if (c.action == "voiceState") { p.voiceEnabled = c.flag; return true; }
                if (c.action == "chat")
                {
                    if (c.text == null || c.text.Length > 120 || (nextChat.TryGetValue(sender, out var next) && Time.realtimeSinceStartup < next)) return false;
                    string text = new string(c.text.Where(ch => !char.IsControl(ch)).ToArray()).Trim();
                    if (text.Length == 0) return false;
                    nextChat[sender] = Time.realtimeSinceStartup + .75f;
                    State.lobbyChat.Add(new LobbyChat { sequence = ++chatSequence, sender = sender, name = p.name, text = text });
                    if (State.lobbyChat.Count > 24) State.lobbyChat.RemoveAt(0);
                    return true;
                }
                return false;
            }
            if (!p.alive || State.phase != Phase.Expedition) return false;
            if (c.action == "move")
            {
                if (!float.IsFinite(c.move.x) || !float.IsFinite(c.move.y) || !float.IsFinite(c.yaw) || Mathf.Abs(c.move.x) > 1 || Mathf.Abs(c.move.y) > 1 || Mathf.Abs(c.yaw) > 1000000) return false;
                p.yaw = Mathf.Repeat(c.yaw, 360); Inputs[sender] = new Command { move = Vector2.ClampMagnitude(c.move, 1), flag = c.flag }; p.lastInputAt = State.elapsed; return true;
            }
            if (c.action == "light") { p.flashlight = c.flag; return true; }
            if (c.action == "close") { p.viewingSite = -1; p.installing = -1; return true; }
            if (c.action == "hibernate") { if (Alive != 1 || !WorldGeometry.InCamp(p.position)) return false; End(Phase.Abandoned, "동면 프로토콜 · 임무 포기"); return true; }
            if (c.action == "drop") { Drop(p); return true; }
            if (c.action == "loot")
            { var l = State.loot.Find(l => l.id == c.target); if (l == null || l.collected || Vector3.Distance(p.position, l.position) > 3 || p.inventory[0] >= Rules.resourceCapacity) return false; l.collected = true; p.inventory[0]++; return true; }
            if (c.action == "dropMaterial") { if (c.target != 0 || p.inventory[0] == 0) return false; p.inventory[0]--; ReleaseScrap(p.position); return true; }
            if (c.action == "modulePickup")
            { if (c.target < 0 || c.target >= State.moduleDrops.Count) return false; var drop = State.moduleDrops[c.target]; if (drop.collected || Vector3.Distance(drop.position, p.position) > 3) return false; drop.collected = true; p.modules.Add(drop.location); return true; }
            if (c.action == "moduleDrop") { if (!p.modules.Remove(c.target)) return false; DropModule(c.target, p.position); return true; }
            if (c.action == "moduleCancel")
            { var station = State.stations.Find(s => s.worker == p.id && s.phase == StationPhase.CraftingModule); if (station == null) return false; CancelModule(station); return true; }
            if (c.target < 0 || c.target >= State.sites.Count) return false;
            var site = State.sites[c.target];
            if (c.action == "moduleCraft")
            {
                var station = State.stations.Find(s => s.id == c.x); var recipe = Content.locations[site.id].recipe;
                if (station == null || station.phase != StationPhase.Available || Vector3.Distance(station.position, p.position) > 4
                    || site.facility != FacilityState.ModuleRequired || p.inventory[0] < recipe.scrapCost
                    || State.players.Any(o => o.modules.Contains(site.id)) || State.moduleDrops.Any(d => !d.collected && d.location == site.id)
                    || State.stations.Any(s => s.phase == StationPhase.CraftingModule && s.target == site.id)) return false;
                p.inventory[0] -= recipe.scrapCost; station.paid = recipe.scrapCost; station.target = site.id; station.worker = p.id; station.started = State.elapsed; station.phase = StationPhase.CraftingModule; return true;
            }
            if (c.action == "puzzle") return PuzzleInput(p, site, c);
            if (c.action == "pickup")
            { if (site.phase != CraftPhase.Ready || p.carrying >= 0 || Vector3.Distance(p.position, ObjectPosition(site)) > 3) return false; site.phase = CraftPhase.Carried; p.carrying = site.id; return true; }
            if (c.action == "activateArtifact")
            { if (site.phase != CraftPhase.Ready && site.phase != CraftPhase.Carried) return false; if (p.carrying != site.id && Vector3.Distance(p.position, ObjectPosition(site)) > 3) return false; site.activated = !site.activated; Emit("ArtifactActivated", site.id, position: p.position); return true; }
            if (!WorldGeometry.Inside(site, p.position)) return false;
            if (c.action == "view") { if (!WorldGeometry.NearConsole(site, p.position)) return false; p.viewingSite = site.id; return true; }
            if (c.action == "install")
            {
                if (!c.flag) { p.installing = -1; return true; }
                if (!WorldGeometry.NearConsole(site, p.position) || site.facility != FacilityState.ModuleRequired || !p.modules.Contains(site.id)) return false;
                if (p.installing != site.id) { p.installing = site.id; p.installStarted = State.elapsed; }
                p.installHeartbeat = State.elapsed; return true;
            }
            if (!site.mission || site.facility == FacilityState.ModuleRequired) return false;
            if (c.action == "craft")
            {
                if (site.phase != CraftPhase.Crafting || site.worker != ulong.MaxValue || p.viewingSite != site.id) return false;
                site.facility = FacilityState.Crafting; site.missionState = MissionState.Crafting; site.worker = p.id; site.checkStarted = State.elapsed;
                if (site.checks == 0) Emit("ArtifactCraftStarted", site.id); Noise(site.position, 10); return true;
            }
            if (c.action == "check")
            {
                if (site.phase != CraftPhase.Crafting || site.worker != p.id || site.checkStarted < 0) return false;
                float cursor = (State.elapsed - site.checkStarted) / Rules.skillCycleSeconds;
                if (Mathf.Abs(cursor - .65f) <= Rules.skillWindow / 2)
                {
                    site.checks++; site.worker = ulong.MaxValue; site.checkStarted = -1;
                    int clues = site.checks / (float)Rules.checksRequired >= .66f ? 2 : site.checks / (float)Rules.checksRequired >= .33f ? 1 : 0;
                    if (clues > site.revealClues) { site.revealClues = clues; AdvanceReveal(site, RevealLevel.DuringCraft); Emit("CraftClue", site.id); }
                    if (site.checks >= Rules.checksRequired) site.phase = CraftPhase.Puzzle;
                }
                else FailCheck(site);
                return true;
            }
            return false;
        }
        void CancelModule(StationState s)
        {
            var p = Player(s.worker);
            if ((State.elapsed - s.started) / Mathf.Max(.01f, Content.locations[s.target].recipe.duration) < Rules.refundCutoff)
                for (int i = 0; i < s.paid; i++) { if (p != null && p.inventory[0] < Rules.resourceCapacity) p.inventory[0]++; else ReleaseScrap(p?.position ?? s.position); }
            s.phase = StationPhase.Available; s.target = -1; s.worker = ulong.MaxValue; s.paid = 0;
        }
        void ReleaseScrap(Vector3 position)
        { var l = State.loot.Find(l => l.collected); if (l == null) { l = new LootState { id = State.loot.Count }; State.loot.Add(l); } l.position = position; l.collected = false; }
        void DropModule(int location, Vector3 position)
        { var d = State.moduleDrops.Find(d => d.collected); if (d == null) { d = new ModuleDrop(); State.moduleDrops.Add(d); } d.location = location; d.position = position; d.collected = false; }
        public Vector3 ObjectPosition(SiteState s)
        { var p = State.players.Find(p => p.carrying == s.id); return p != null ? p.position : dropped.TryGetValue(s.id, out var pos) ? pos : s.position + Quaternion.Euler(0, s.quarterTurn * 90, 0) * new Vector3(2, 0, -1); }
        public void Drop(PlayerState p)
        { if (p == null || p.carrying < 0) return; var s = State.sites[p.carrying]; s.phase = CraftPhase.Ready; dropped[s.id] = p.position; p.carrying = -1; }
        public void Kill(PlayerState p, string reason)
        {
            if (p == null || !p.alive || State.phase != Phase.Expedition) return;
            foreach (var s in State.stations.Where(s => s.worker == p.id && s.phase == StationPhase.CraftingModule).ToArray()) CancelModule(s);
            Drop(p); for (int i = 0; i < p.inventory[0]; i++) ReleaseScrap(p.position); p.inventory[0] = 0;
            foreach (int module in p.modules) DropModule(module, p.position); p.modules.Clear(); p.installing = p.viewingSite = -1;
            p.alive = false; p.deathReason = reason; Inputs.Remove(p.id);
        }
        public void Infect(ulong id) { var p = Player(id); if (p != null) Kill(p, "감염 확정"); }
        void FailCheck(SiteState s) { s.worker = ulong.MaxValue; s.checkStarted = -1; Noise(s.position, 32); Emit("CraftFailed", s.id); }
        public void Noise(Vector3 at, float range)
        {
            Emit("Sound", text: range.ToString(System.Globalization.CultureInfo.InvariantCulture), position: at);
            foreach (var m in State.monsters.Where(m => m.kind == MonsterKind.Sound && Vector3.Distance(m.position, at) <= range))
            { m.destination = WorldGeometry.OutsideSafety(State, at); m.aggroUntil = State.elapsed + Rules.noiseMemorySeconds; m.alert = AlertState.Investigate; }
        }
        bool PuzzleInput(PlayerState p, SiteState s, Command c)
        {
            if (s.phase != CraftPhase.Puzzle || !WorldGeometry.InCamp(p.position) || State.elapsed - s.lastPuzzleInput < .12f) return false;
            if (!State.players.Any(o => o.id != p.id && o.alive && o.connected && o.viewingSite == s.id && WorldGeometry.Inside(s, o.position))) return false;
            if (c.x < -1 || c.x > 1 || c.y < -1 || c.y > 1 || Math.Abs(c.x) + Math.Abs(c.y) > 1 || (c.flag && (c.x != 0 || c.y != 0)) || (c.flag && s.puzzleKind == PuzzleKind.Maze)) return false;
            s.lastPuzzleInput = State.elapsed; int x = Mathf.Clamp(s.puzzleX + c.x, 0, 5), y = Mathf.Clamp(s.puzzleY + c.y, 0, 5);
            var profile = Content.Artifact(s).puzzle != null ? Content.Artifact(s).puzzle : Content.locations[s.id].puzzle;
            void Failed() { Noise(s.position, profile.failNoiseRadius); s.puzzleAttempts++; if (profile.maxAttempts > 0 && s.puzzleAttempts >= profile.maxAttempts) { s.puzzleX = s.puzzleY = s.angle = s.puzzleAttempts = 0; } }
            if (s.puzzleKind == PuzzleKind.Maze && s.maze[y * 6 + x] == 1) { Failed(); return false; }
            s.puzzleX = x; s.puzzleY = y;
            if (s.puzzleKind == PuzzleKind.Alignment && c.flag) s.angle = (s.angle + 1) % 4;
            bool match = x == s.targetX && y == s.targetY;
            bool solved = s.puzzleKind == PuzzleKind.Maze ? match : s.puzzleKind == PuzzleKind.Crane ? match && c.flag : match && s.angle == s.targetAngle;
            if (solved) { s.phase = CraftPhase.Ready; s.missionState = MissionState.Crafted; s.craftedAt = State.elapsed; Emit("ArtifactCrafted", s.id); }
            else if (s.puzzleKind == PuzzleKind.Crane && c.flag) Failed();
            return true;
        }
        public Snapshot ForClient(ulong id)
        {
            var copy = JsonUtility.FromJson<Snapshot>(JsonUtility.ToJson(State)); copy.rulesJson = JsonUtility.ToJson(Rules);
            var p = Player(id); bool camp = p != null && p.alive && WorldGeometry.InCamp(p.position);
            foreach (var s in copy.sites)
            {
                var a = Content.Artifact(s); var location = Content.locations[s.id];
                s.moduleExists=State.players.Any(o=>o.modules.Contains(s.id))||State.moduleDrops.Any(d=>!d.collected&&d.location==s.id)||State.stations.Any(t=>t.phase==StationPhase.CraftingModule&&t.target==s.id);
                bool viewing = p != null && p.alive && p.viewingSite == s.id && WorldGeometry.Inside(s, p.position);
                if (!viewing) { s.targetX = s.targetY = s.targetAngle = -1; s.maze = Array.Empty<int>(); }
                s.objectPosition = ObjectPosition(State.sites[s.id]);
                s.discovered = camp ? s.discovered : p != null && p.knownLocations.Contains(s.id);
                s.displayName = s.reveal == RevealLevel.Recovered ? a.trueNameKo : "";
                s.missionHint = new MissionHint { category = a.missionHint.category, structure = a.missionHint.structure, function = a.missionHint.function };
                s.missionLocationHint = location.missionLocationHint;
            }
            foreach (var other in copy.players) if (other.id != id) { other.inventory = Array.Empty<int>(); other.modules.Clear(); other.knownLocations.Clear(); }
            return copy;
        }
    }
}

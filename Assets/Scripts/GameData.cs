using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    public enum Phase { Lobby, Expedition, Success, OxygenLost, AllDead, Abandoned }
    public enum CraftPhase { Repair, Crafting, Puzzle, Ready, Carried, Delivered }
    public enum PuzzleKind { Alignment, Crane, Maze }
    public enum MonsterKind { Sound, Light, Patrol, Stillness, Echo, Pollution }
    public enum MissionState { Hidden, Assigned, LocationDiscovered, FacilityRestored, Crafting, Crafted, Recovered, Failed }
    public enum RevealLevel { Assignment, OnLocation, DuringCraft, Recovered }
    public enum FacilityState { ModuleRequired, Restored, Crafting }
    public enum StationPhase { Available, CraftingModule, Disabled }

    [CreateAssetMenu(menuName = "Earth Recovery/Rules")]
    public sealed class GameRules : ScriptableObject
    {
        [Range(2, 6)] public int maxPlayers = 6;
        [Range(60, 1740)] public float oxygenSeconds = 1200;
        public float walkSpeed = 4, runSpeed = 6, carrySpeed = 3;
        public int resourceCapacity = 12;
        public int checksRequired = 4;
        public float skillCycleSeconds = 2.4f;
        [Range(.05f, .5f)] public float skillWindow = .22f;
        public float monsterSpeed = 3.1f;
        public float monsterSenseRange = 18;
        public float noiseMemorySeconds = 14;
        public float stillnessSeconds = 12;
        public int minPlayers = 4;
        public bool developerSolo;
        public int MinimumStartPlayers
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (developerSolo) return 1;
#endif
                return minPlayers;
            }
        }
        public float stationMinDistance = 24, stationMinDistanceFromBase = 15, stationMinDistanceFromEntrance = 8;
        public float refundCutoff = .5f, observationDistance = 12, recoveryToastSeconds = 3.5f;
        public bool retainArchiveKnowledge, rainy, debugAllModules;
        public void Sanitize()
        {
            maxPlayers = Mathf.Clamp(maxPlayers, 2, 6);
            minPlayers = Mathf.Clamp(minPlayers, 2, maxPlayers);
            stationMinDistance = Bound(stationMinDistance, 1, 60, 24);
            stationMinDistanceFromBase = Bound(stationMinDistanceFromBase, 8, 30, 15);
            stationMinDistanceFromEntrance = Bound(stationMinDistanceFromEntrance, 1, 12, 8);
            refundCutoff = Bound(refundCutoff, 0, 1, .5f);
            observationDistance = Bound(observationDistance, 1, 30, 12);
            resourceCapacity = Mathf.Clamp(resourceCapacity, 1, 30);
            checksRequired = Mathf.Clamp(checksRequired, 1, 20);
            oxygenSeconds = Bound(oxygenSeconds, 60, 1740, 1200);
            walkSpeed = Bound(walkSpeed, .1f, 12, 4);
            runSpeed = Bound(runSpeed, .1f, 12, 6);
            carrySpeed = Bound(carrySpeed, .1f, 12, 3);
            skillCycleSeconds = Bound(skillCycleSeconds, .2f, 30, 2.4f);
            skillWindow = Bound(skillWindow, .05f, .5f, .22f);
            monsterSpeed = Bound(monsterSpeed, .1f, 12, 3.1f);
            monsterSenseRange = Bound(monsterSenseRange, 1, 150, 18);
            noiseMemorySeconds = Bound(noiseMemorySeconds, 0, 120, 14);
            stillnessSeconds = Bound(stillnessSeconds, .1f, 120, 12);
        }
        static float Bound(float value, float min, float max, float fallback) => float.IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;
    }

    public static class Catalog
    {
        public static string[] Sites => HumanContent.Load().locations.Select(l => l.displayNameKnown).ToArray();
        public static string[] Districts => HumanContent.Load().zones.Select(z => z.displayName).ToArray();
        public static int[] District => HumanContent.Load().locations.Select(l => l.zone).ToArray();
        public static string Material(int id) => "고철";
        public static string Product(SiteState site) => string.IsNullOrEmpty(site.displayName) ? "표본 " + site.sampleCode + " / ???" : site.displayName;
        public static string MapName(SiteState site) => site.discovered ? Sites[site.id] : "??";
        public static string MissionDetails(SiteState site) => string.Join("\n", MissionLines(site));
        public static string[] MissionLines(SiteState site) => new[] { site.missionHint.category, site.missionHint.structure, site.missionHint.function, site.missionLocationHint };
        public static string[] ArchiveLines(ArchiveEntry entry)
        {
            if (!entry.discovered) return new[] { "미식별 관찰 기록" }.Concat(entry.observations).ToArray();
            // Save files own progress; the ID resolves the current text without migrating old saves.
            var artifact = HumanContent.Load().artifacts.FirstOrDefault(a => a.id == entry.artifactId);
            var lines = new List<string> { (artifact != null ? artifact.trueNameEn + " / " + artifact.trueNameKo : entry.trueNameEn + " / " + entry.trueNameKo) + " · " + entry.recoverCount + "회" };
            if (artifact != null)
            {
                lines.Add(artifact.missionHint.category);
                lines.AddRange(artifact.archiveDetails);
            }
            lines.Add(artifact != null ? artifact.archiveDescription : entry.archiveDescription);
            lines.Add("Mars: " + (artifact != null ? artifact.marsComment : entry.marsComment));
            lines.AddRange(entry.observations);
            return lines.ToArray();
        }
        public static Vector3 DistrictCenter(int district)
        {
            return HumanContent.Load().zones[district].center;
        }
    }

    [Serializable] public sealed class PlayerState
    {
        public ulong id;
        public string name;
        public string characterId = PlayerCharacterCatalog.DefaultId;
        public Vector3 position;
        public float yaw;
        public bool alive = true, connected = true, ready, flashlight, voiceEnabled;
        public int carrying = -1;
        public int[] inventory = new int[1];
        public List<int> modules = new(), knownLocations = new();
        public int installing = -1;
        public float installStarted, installHeartbeat;
        public int viewingSite = -1;
        public float stationary, lastInputAt;
        public string deathReason = "";
    }
    [Serializable] public sealed class SiteState
    {
        public int id, product, level, checks;
        public Vector3 position, objectPosition;
        public int quarterTurn;
        public bool mission;
        public CraftPhase phase;
        public float checkStarted = -1;
        public ulong worker = ulong.MaxValue;
        public int puzzleX, puzzleY, angle, targetX, targetY, targetAngle;
        public PuzzleKind puzzleKind;
        public int[] maze = Array.Empty<int>();
        public float lastPuzzleInput;
        public FacilityState facility;
        public MissionState missionState;
        public RevealLevel reveal;
        public int assignedOrder, revealClues, puzzleAttempts;
        public bool discovered, activated;
        public bool moduleExists;
        public string sampleCode = "", displayName = "", missionLocationHint = "";
        public MissionHint missionHint = new();
        public float assignedAt, restoredAt = -1, craftedAt = -1, recoveredAt = -1;
        public ulong recoveredBy = ulong.MaxValue;
    }
    [Serializable] public sealed class LootState
    {
        public int id, material;
        public Vector3 position;
        public bool collected;
    }
    [Serializable] public sealed class MonsterState
    {
        public int id, definition;
        public MonsterKind kind;
        public Vector3 position, home, destination;
        public float aggroUntil;
        public ulong target = ulong.MaxValue;
        public AlertState alert;
        public float stateUntil, nextPerception, nextMemory, lastSeen, lightExposure;
        public int memoryCount, routeIndex, anchor = -1;
        public string behavior = "";
        public bool protectedMemory;
        public List<string> usedReactions = new();
    }
    [Serializable] public sealed class Snapshot
    {
        public string regionId = ExpeditionRegions.DefaultId;
        public Phase phase;
        public int seed;
        public bool cityWorld;
        public Vector2 mapSize = new(150, 150);
        public float oxygen, elapsed;
        public List<PlayerState> players = new();
        public List<SiteState> sites = new();
        public List<LootState> loot = new();
        public List<MonsterState> monsters = new();
        public string message = "";
        public string rulesJson = "";
        public string runId = "";
        public bool rainy;
        public List<StationState> stations = new();
        public List<ModuleDrop> moduleDrops = new();
        public List<ArchiveEntry> archive = new();
        public List<GameEvent> events = new();
        public List<LobbyChat> lobbyChat = new();
    }
    [Serializable] public sealed class LobbyChat { public int sequence; public ulong sender; public string name, text; }
    [Serializable] public sealed class StationState
    {
        public int id, zone, target = -1, paid;
        public Vector3 position;
        public StationPhase phase;
        public ulong worker = ulong.MaxValue;
        public float started;
    }
    [Serializable] public sealed class ModuleDrop { public int location; public Vector3 position; public bool collected; }
    [Serializable] public sealed class ArchiveEntry
    {
        public string artifactId, trueNameKo, trueNameEn, archiveDescription, marsComment, firstRecoveredUtc;
        public bool discovered;
        public int recoverCount, runIndex;
        public List<string> observations = new();
    }
    [Serializable] public sealed class GameEvent { public int sequence, target; public string kind, text; public Vector3 position; public float at; }
    [Serializable] public sealed class Command
    {
        public string action;
        public int target, x, y;
        public float yaw;
        public Vector2 move;
        public bool flag;
        public string text;
    }

    public static class WorldGeometry
    {
        public const float CampRadius = 8;
        public static bool InCamp(Vector3 p) => Mathf.Abs(p.x) < 7 && Mathf.Abs(p.z) < 7;
        public static Vector3 Local(SiteState s, Vector3 p) => Quaternion.Euler(0, -s.quarterTurn * 90, 0) * (p - s.position);
        public static bool Inside(SiteState s, Vector3 p)
        {
            var q = Local(s, p);
            return Mathf.Abs(q.x) < 5 && Mathf.Abs(q.z) < 5;
        }
        public static bool Safe(Snapshot s, Vector3 p) => InCamp(p) || s.sites.Exists(x => Inside(x, p));
        public static bool NearConsole(SiteState s, Vector3 p)
        {
            var console = HumanContent.Load().locations[s.id].prefab.GetComponent<LocationController>().console.localPosition;
            var local = Local(s, p); local.y = console.y;
            return Inside(s, p) && Vector3.Distance(local, console) <= 2.5f;
        }
        public static Vector3 Entrance(SiteState s) => s.position + Quaternion.Euler(0, s.quarterTurn * 90, 0) * new Vector3(0, 0, -7);
        public static Vector3 OutsideSafety(Snapshot state, Vector3 p)
        {
            if (InCamp(p)) return new Vector3(0, 0, -9);
            var site = state.sites.Find(s => Inside(s, p));
            return site == null ? p : Entrance(site);
        }
    }
}

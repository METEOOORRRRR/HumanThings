using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    public static class SnapshotValidation
    {
        static bool Position(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z)
            && Mathf.Abs(p.x) <= 200 && Mathf.Abs(p.y) <= 200 && Mathf.Abs(p.z) <= 200;
        static bool Text(string value, int limit) => value != null && value.Length <= limit;
        public static bool Valid(Snapshot s, ulong localId)
        {
            if (s == null || s.phase < Phase.Lobby || s.phase > Phase.Abandoned || !float.IsFinite(s.oxygen)
                || s.oxygen < 0 || s.oxygen > 1740 || !float.IsFinite(s.elapsed) || s.elapsed < 0 || s.elapsed > 100000
                || !Text(s.message, 256) || !Text(s.rulesJson, 4096)) return false;
            if (s.players == null || s.sites == null || s.loot == null || s.monsters == null
                || s.players.Count > 6 || s.loot.Count > 512 || s.monsters.Count > 32
                || s.sites.Count != (s.phase == Phase.Lobby ? 0 : HumanContent.Load().locations.Length)) return false;
            if(s.stations==null || s.stations.Count!=(s.phase==Phase.Lobby?0:3) || s.moduleDrops==null || s.moduleDrops.Count>128 || s.archive==null || s.archive.Count>512 || s.events==null || s.events.Count>64) return false;
            if(s.stations.Any(t=>t==null||!Position(t.position)||t.id<0||t.id>=3||t.target< -1||t.target>=s.sites.Count||!float.IsFinite(t.started)||t.phase<StationPhase.Available||t.phase>StationPhase.Disabled)) return false;
            if(s.moduleDrops.Any(d=>d==null||!Position(d.position)||d.location<0||d.location>=s.sites.Count)) return false;
            if(s.archive.Any(e=>e==null||!Text(e.artifactId,64)||e.observations==null||e.observations.Count>64||e.recoverCount<0)) return false;
            if(s.events.Any(e=>e==null||!Text(e.kind,64)||!Text(e.text,12000)||!Position(e.position)||!float.IsFinite(e.at))) return false;
            if (s.players.Any(p => p == null || !Text(p.name, 32) || !Text(p.deathReason, 256) || !Position(p.position)
                || !float.IsFinite(p.yaw) || !float.IsFinite(p.stationary) || !float.IsFinite(p.lastInputAt)
                || p.inventory == null || p.inventory.Length != (p.id == localId ? 1 : 0)
                || p.modules==null||p.modules.Count>128||p.modules.Any(i=>i<0||i>=s.sites.Count)||p.knownLocations==null||p.knownLocations.Any(i=>i<0||i>=s.sites.Count)
                || p.installing< -1||p.installing>=s.sites.Count||!float.IsFinite(p.installStarted)||!float.IsFinite(p.installHeartbeat)
                || p.inventory.Any(n => n < 0 || n > 30) || p.inventory.Sum() > 30
                || p.carrying < -1 || p.carrying >= s.sites.Count || p.viewingSite < -1 || p.viewingSite >= s.sites.Count)) return false;
            if (s.players.Select(p => p.id).Distinct().Count() != s.players.Count) return false;
            for (int i = 0; i < s.sites.Count; i++)
            {
                var x = s.sites[i];
                if (x == null || x.id != i || x.product < 0 || x.product > 2 || x.level < 0 || x.level > 3
                    || x.phase < CraftPhase.Repair || x.phase > CraftPhase.Delivered || x.puzzleKind < PuzzleKind.Alignment || x.puzzleKind > PuzzleKind.Maze
                    || x.quarterTurn < 0 || x.quarterTurn > 3 || !Position(x.position) || !Position(x.objectPosition)
                    || x.checks < 0 || x.checks > 20 || !float.IsFinite(x.checkStarted) || !float.IsFinite(x.lastPuzzleInput)
                    || x.puzzleX < 0 || x.puzzleX > 5 || x.puzzleY < 0 || x.puzzleY > 5 || x.angle < 0 || x.angle > 3
                    || x.targetX < -1 || x.targetX > 5 || x.targetY < -1 || x.targetY > 5 || x.targetAngle < -1 || x.targetAngle > 3
                    || x.maze == null || (x.maze.Length != 0 && x.maze.Length != 36) || x.maze.Any(n => n != 0 && n != 1)) return false;
                if (x.targetX >= 0 && (x.targetY < 0 || x.targetAngle < 0 || (x.puzzleKind == PuzzleKind.Maze && x.maze.Length != 36))) return false;
                if(!Text(x.sampleCode,32)||!Text(x.displayName,128)||!Text(x.category,128)||!Text(x.materialHint,256)||!Text(x.functionHint,256)||!Text(x.facilityHint,512)||!Text(x.clueText,1024)
                    || x.reveal<RevealLevel.Assignment||x.reveal>RevealLevel.Recovered||x.revealClues<0||x.revealClues>2||x.facility<FacilityState.ModuleRequired||x.facility>FacilityState.Crafting) return false;
            }
            if (s.phase != Phase.Lobby && s.sites.Count(x => x.mission) != 3) return false;
            if (s.loot.Any(x => x == null || x.id < 0 || x.id >= 512 || x.material != 0 || !Position(x.position))
                || s.loot.Select(x => x.id).Distinct().Count() != s.loot.Count) return false;
            return !s.monsters.Any(x => x == null || x.id < 0 || x.id >= 32 || x.kind < MonsterKind.Sound || x.kind > MonsterKind.Pollution || x.definition<0||x.definition>=HumanContent.Load().monsters.Length
                || !Position(x.position) || !Position(x.home) || !Position(x.destination) || !float.IsFinite(x.aggroUntil))
                && s.monsters.Select(x => x.id).Distinct().Count() == s.monsters.Count;
        }
    }
}

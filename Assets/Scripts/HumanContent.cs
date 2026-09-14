using System;
using System.Linq;
using UnityEngine;
namespace EarthRecovery
{
    public sealed class HumanContent : ScriptableObject
    {
        public ZoneDefinition[] zones;
        public LocationDefinition[] locations;
        public ArtifactDefinition[] artifacts;
        public MonsterDefinition[] monsters;
        public GameObject stationPrefab, scrapPrefab, recoveryPrefab;
        public static HumanContent Load() => Resources.Load<HumanContent>("HumanContent");
        public ArtifactDefinition Artifact(SiteState site) => locations[site.id].artifacts[site.product];
        public void Validate()
        {
            if (zones == null || locations == null || artifacts == null || monsters == null) throw new InvalidOperationException("Content missing");
            if (artifacts.Select(a => a.id).Distinct().Count() != artifacts.Length) throw new InvalidOperationException("Duplicate artifact ID");
            foreach (var z in zones)
                if (z.locations.Length != 3 || z.spawnSockets.Length < 3 || z.locations.Any(l => !locations.Contains(l))) throw new InvalidOperationException("Zone mapping");
            foreach (var l in locations)
                if (l.artifacts.Length != 3 || l.prefab == null || l.recipe == null || l.puzzle == null || l.artifacts.Any(a => a.locationId != l.id || !artifacts.Contains(a))) throw new InvalidOperationException("Location mapping: " + l.id);
            foreach(var l in locations)
                if(l.recipe.scrapCost<1||l.recipe.scrapCost>30||!float.IsFinite(l.recipe.duration)||l.recipe.duration<=0||!float.IsFinite(l.recipe.installDuration)||l.recipe.installDuration<=0)throw new InvalidOperationException("Invalid recipe: "+l.id);
            foreach (var a in artifacts)
                if (a.worldPrefab == null || string.IsNullOrEmpty(a.archiveDescription) || a.level2Clues.Length < 2) throw new InvalidOperationException("Artifact incomplete: " + a.id);
            foreach(var m in monsters)
                if(m.prefab==null||m.prefab.GetComponent<MonsterBrain>()==null||m.memories==null||m.memories.Length==0||m.reactions==null||!float.IsFinite(m.perceptionTick)||m.perceptionTick<=0)throw new InvalidOperationException("Monster incomplete: "+m.id);
        }
    }
}

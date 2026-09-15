using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery.Editor
{
    [Serializable] public sealed class CityPlacementScore
    {
        public int requested, placed;
        public float Rate => requested == 0 ? 1 : (float)placed / requested;
        public void Add(CityPlacementScore other) { requested += other.requested; placed += other.placed; }
        public override string ToString() => requested == 0 ? "N/A (0 requested)" : placed + "/" + requested + " (" + Rate.ToString("P1") + ")";
    }
    [Serializable] public sealed class CitySeedValidation
    {
        public int seed, roads, lots, emptyLots, overlapPairs, connectionFailures;
        public CityPlacementScore buildings = new(), vehicles = new(), props = new(), transit = new(), requiredPOIs = new();
        public List<string> warnings = new();
        public string error;
    }
    [Serializable] public sealed class CityTemplateValidation
    {
        public string templateName, templatePath;
        public int tuningRounds;
        public List<string> adjustments = new();
        public List<CitySeedValidation> seeds = new();
        public CityPlacementScore buildings = new(), vehicles = new(), props = new(), requiredPOIs = new();
        public bool passed;
        public static CityTemplateValidation Run(CityLayoutTemplate template, HumanThingsAssetDatabase db, int seedCount = 10, bool holdout = false)
        {
            if (seedCount < 10 || seedCount > 100) throw new ArgumentOutOfRangeException(nameof(seedCount));
            var r = new CityTemplateValidation { templateName = template.templateName };
            for (int i = 0; i < seedCount; i++)
            {
                int seed = holdout ? 1000003 + i * 7919 : 1009 + i * 7919;
                var s = new CitySeedValidation { seed = seed }; r.seeds.Add(s);
                try
                {
                    var p = ProceduralCityGenerator.Generate(template, seed, db);
                    s.roads = p.roads.Count; s.lots = p.lots.Count;
                    var solid = p.placement.placements.Where(v => !v.surface).ToArray();
                    s.emptyLots = s.lots - solid.Where(v => v.asset.category == AssetCategory.Building).Select(v => v.zoneId).Distinct().Count();
                    foreach (var q in p.compiled.requests.Where(q => !q.surface))
                    {
                        int placed = solid.Count(v => v.requestId == q.id);
                        var score = q.query.category == AssetCategory.Building ? s.buildings : q.group == "Vehicles" ? s.vehicles : q.group == "StreetProps" ? s.props : s.transit;
                        score.requested += q.count; score.placed += placed;
                        if (q.required) { s.requiredPOIs.requested += q.count; s.requiredPOIs.placed += placed; }
                    }
                    for (int x = 0; x < solid.Length; x++) for (int y = x + 1; y < solid.Length; y++)
                        if (HumanThingsPlacementSolver.Overlaps(solid[x].footprint, solid[y].footprint)) s.overlapPairs++;
                    s.connectionFailures = p.placement.warnings.Count(w => w.StartsWith("INVALID:", StringComparison.Ordinal));
                    s.warnings = p.placement.warnings.Distinct().ToList();
                }
                catch (Exception e) { s.error = e.Message; }
                r.buildings.Add(s.buildings); r.vehicles.Add(s.vehicles); r.props.Add(s.props); r.requiredPOIs.Add(s.requiredPOIs);
            }
            r.passed = r.seeds.All(s => s.error == null && s.roads > 0 && s.lots > 0 && s.overlapPairs == 0 && s.connectionFailures == 0)
                && r.buildings.requested > 0 && r.buildings.Rate >= .8f && r.vehicles.Rate >= .8f && r.props.Rate >= .8f && r.requiredPOIs.Rate >= .95f;
            return r;
        }
    }
}

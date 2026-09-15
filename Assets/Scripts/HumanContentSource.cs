using System;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    [Serializable] public sealed class HumanContentSource
    {
        [Serializable] public sealed class Location
        {
            public string id, displayNameKnown, missionLocationHint, moduleName;
            public int zone;
            public string[] artifactIds;
        }
        [Serializable] public sealed class Artifact
        {
            public string id, locationId, trueNameKo, trueNameEn, archiveDescription, marsComment;
            public MissionHint missionHint;
            public string[] archiveDetails;
        }
        public Location[] locations;
        public Artifact[] artifacts;

        public static bool ValidHint(MissionHint hint) => hint != null
            && !string.IsNullOrWhiteSpace(hint.category) && hint.category.Length <= 128
            && !string.IsNullOrWhiteSpace(hint.structure) && hint.structure.Length <= 512
            && !string.IsNullOrWhiteSpace(hint.function) && hint.function.Length <= 512;

        public static HumanContentSource Parse(string json)
        {
            var source = JsonUtility.FromJson<HumanContentSource>(json);
            if (source?.locations == null || source.artifacts == null || source.locations.Length != 15 || source.artifacts.Length != 45
                || source.locations.Any(l => l == null || string.IsNullOrWhiteSpace(l.id))
                || source.artifacts.Any(a => a == null || string.IsNullOrWhiteSpace(a.id))
                || source.locations.Select(l => l.id).Distinct().Count() != 15
                || source.artifacts.Select(a => a.id).Distinct().Count() != 45)
                throw new InvalidOperationException("Content source requires 15 unique locations and 45 unique artifacts");
            foreach (var l in source.locations)
                if (l.zone < 0 || l.zone > 4 || string.IsNullOrWhiteSpace(l.displayNameKnown)
                    || string.IsNullOrWhiteSpace(l.missionLocationHint) || l.missionLocationHint.Length > 512
                    || l.artifactIds == null || l.artifactIds.Length != 3 || l.artifactIds.Distinct().Count() != 3
                    || l.artifactIds.Any(id => !source.artifacts.Any(a => a.id == id && a.locationId == l.id)))
                    throw new InvalidOperationException("Invalid source location: " + l.id);
            if (Enumerable.Range(0, 5).Any(z => source.locations.Count(l => l.zone == z) != 3)
                || source.locations.SelectMany(l => l.artifactIds).Distinct().Count() != 45)
                throw new InvalidOperationException("Invalid source zone or artifact ownership");
            foreach (var a in source.artifacts)
                if (!ValidHint(a.missionHint) || a.archiveDetails == null || a.archiveDetails.Any(string.IsNullOrWhiteSpace)
                    || string.IsNullOrWhiteSpace(a.trueNameKo) || string.IsNullOrWhiteSpace(a.trueNameEn)
                    || string.IsNullOrWhiteSpace(a.archiveDescription) || string.IsNullOrWhiteSpace(a.marsComment))
                    throw new InvalidOperationException("Invalid source artifact: " + a.id);
            return source;
        }
    }
}

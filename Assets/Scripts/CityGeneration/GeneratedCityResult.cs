using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    [Serializable] public sealed class GeneratedLocation
    {
        public string id;
        public LocationType type;
        public Bounds bounds;
        public List<string> tags = new();
        public GameObject root;
    }
    [Serializable] public sealed class GeneratedCityObject
    {
        public string id, assetGuid, lotId;
        public Bounds bounds;
        public GameObject root;
    }
    [Serializable] public sealed class GeneratedCityResult
    {
        public int seed;
        public CityLayoutTemplate template;
        public RuntimeCityGenerationConfig config;
        public GameObject root;
        public List<GeneratedRoad> roads = new();
        public List<GeneratedLot> lots = new();
        public List<GeneratedCityObject> buildings = new(), vehicles = new(), props = new();
        public List<GeneratedLocation> locations = new();
        public List<string> warnings = new();
        public IReadOnlyList<GeneratedLocation> GetGeneratedLocations() => locations.AsReadOnly();
        public IEnumerable<GeneratedLocation> GetLocationsByType(LocationType type) => locations.Where(l => l.type == type);
        public IEnumerable<GeneratedLocation> GetLocationsByTag(string tag) => locations.Where(l => l.tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)));
        public GeneratedLocation GetRandomLocationByType(LocationType type, System.Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var matches = GetLocationsByType(type).OrderBy(l => l.id, StringComparer.Ordinal).ToArray();
            return matches.Length == 0 ? null : matches[rng.Next(matches.Length)];
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    public enum ConnectorSide { North, South, East, West }
    public enum ConnectorType { MainRoad, SideRoad, Alley }

    [Serializable] public sealed class ChunkConnector
    {
        public string connectionId;
        public ConnectorSide side;
        public float normalizedPosition, roadWidth, sidewalkWidth;
        public ConnectorType type;
        public Vector2Int neighbor;
        public bool external;
        public Vector3 LocalPoint(Bounds bounds)
        {
            float x = Mathf.Lerp(-bounds.extents.x, bounds.extents.x, normalizedPosition);
            float z = Mathf.Lerp(-bounds.extents.z, bounds.extents.z, normalizedPosition);
            return side switch
            {
                ConnectorSide.North => new Vector3(x, 0, bounds.extents.z),
                ConnectorSide.South => new Vector3(x, 0, -bounds.extents.z),
                ConnectorSide.East => new Vector3(bounds.extents.x, 0, z),
                _ => new Vector3(-bounds.extents.x, 0, z)
            };
        }
        public Vector3 WorldPoint(Bounds bounds) => bounds.center + LocalPoint(bounds);
    }

    public sealed class CityChunkGenerationRequest
    {
        public CityLayoutTemplate template;
        public int seed;
        public Bounds chunkBounds;
        public List<ChunkConnector> requiredConnectors = new();
        public float sidewalkWidth = 6;
        public Bounds? baseCampReservation;
    }

    [Serializable] public sealed class CityChunkNode
    {
        public Vector2Int gridPosition;
        public Bounds worldBounds;
        public CityDistrictKind district;
        public CityLayoutTemplate chunkTemplate;
        public int chunkSeed;
        public bool required = true;
        public List<ChunkConnector> connectors = new();
        [NonSerialized] public GeneratedCityChunk generatedResult;
        public string Id => "Chunk_" + gridPosition.x + "_" + gridPosition.y;
    }

    [Serializable] public sealed class GeneratedRoadConnection
    {
        public string id;
        public Vector2Int a, b;
        public ConnectorType type;
        public Vector3 worldPoint;
        public float roadWidth, sidewalkWidth;
        public bool globalMainRoute;
    }

    public sealed class CityWorldPlan
    {
        public int seed, countX, countZ;
        public CityWorldTemplate template;
        public Bounds bounds;
        public Vector2Int startChunk;
        public List<CityChunkNode> nodes = new();
        public List<GeneratedRoadConnection> connections = new();
        public List<Vector2Int> mainRoute = new();
        public Dictionary<Vector2Int, ProceduralCityPlan> chunkPlans = new();
        public List<SpecialPOIPlacement> specialPOIs = new();
        public float baseCampClearRadius, minPOIDistance;
        public int maxPOIsPerChunk, poiAttempts;
        public Bounds baseCampBounds;
    }

    [Serializable] public sealed class GeneratedCityChunk
    {
        public CityChunkNode node;
        public GeneratedCityResult city;
    }

    [Serializable] public sealed class GeneratedWorldResult
    {
        public int worldSeed;
        public CityWorldTemplate worldTemplate;
        public Bounds worldBounds;
        public GameObject root;
        public List<GeneratedCityChunk> chunks = new();
        public List<GeneratedRoadConnection> globalRoads = new();
        public List<GeneratedSpecialPOI> specialPOIs = new();
        public GameObject baseCampInstance;
        public Bounds baseCampBounds;
        public float baseCampClearRadius, minPOIDistance;
        // Bounds and IDs are world-space and globally unique; chunk results stay local.
        public List<GeneratedLocation> locations = new();
        public IReadOnlyList<GeneratedLocation> GetAllLocations() => locations.AsReadOnly();
        public IEnumerable<GeneratedLocation> GetLocationsByType(LocationType type) => locations.Where(l => l.type == type);
        public IEnumerable<GeneratedLocation> GetLocationsByTag(string tag) => locations.Where(l => l.tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)));
        public void RefreshLocations()
        {
            locations.Clear();
            locations.AddRange(specialPOIs.Where(p => p.instance != null).Select(p => p.location));
            foreach (var chunk in chunks.Where(c => c.city?.root != null))
                foreach (var location in chunk.city.locations)
                {
                    var b = location.bounds; b.center += chunk.node.worldBounds.center;
                    locations.Add(new GeneratedLocation { id = chunk.node.Id + "/" + location.id, bounds = b,
                        type = location.type, root = location.root, tags = location.tags.Concat(new[] { chunk.node.Id, chunk.node.district.ToString() }).Distinct().ToList() });
                }
        }
    }
}

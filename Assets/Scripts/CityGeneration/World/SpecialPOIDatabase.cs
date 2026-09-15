using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    [CreateAssetMenu(menuName = "HumanThings/Special POI Database")]
    public sealed class SpecialPOIDatabase : ScriptableObject
    {
        // Canonical IDs are a schema contract, not mission/objective selections.
        public static readonly string[] RequiredIds = { "COM_MUSIC_STORE", "COM_RESTAURANT", "COM_ELECTRONICS", "RES_TOY_STORE", "RES_CLOTHING",
            "RES_CONVENIENCE", "IND_TOOL_SHOP", "IND_AUTO_SHOP", "IND_PRINT_SHOP", "RES_HOSPITAL", "RES_LAB", "RES_SIGNAL_STATION", "CUL_LIBRARY", "CUL_SCHOOL", "CUL_MUSEUM" };
        public List<SpecialPOIDefinition> definitions = new();
        public void Validate()
        {
            if (definitions == null || definitions.Count != 15 || definitions.Any(d => d == null)
                || definitions.Select(d => d.id).Distinct(StringComparer.Ordinal).Count() != 15
                || !RequiredIds.All(id => definitions.Any(d => d.id == id))) throw new ArgumentException("Special POI Database must contain exactly the 15 canonical IDs, once each.");
            if (definitions.Any(d => d.prefab == null) || definitions.Select(d => d.prefab).Distinct().Count() != 15)
                throw new ArgumentException("Each Special POI needs its own non-null, fixed prefab reference.");
            foreach (var d in definitions)
                if (!d.placementEnabled || string.IsNullOrWhiteSpace(d.displayName) || !Finite(d.boundsCenter) || !Positive(d.boundsSize)
                    || !float.IsFinite(d.footprint.x) || !float.IsFinite(d.footprint.y) || d.footprint.x < d.boundsSize.x || d.footprint.y < d.boundsSize.z
                    || !Finite(d.localForward) || new Vector2(d.localForward.x, d.localForward.z).sqrMagnitude < .001f
                    || !float.IsFinite(d.minDistanceFromOtherPOI) || d.minDistanceFromOtherPOI < 0 || !float.IsFinite(d.minDistanceFromBaseCamp) || d.minDistanceFromBaseCamp < 0)
                    throw new ArgumentException("Invalid or disabled required Special POI: " + d.id);
        }
        public static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        public static bool Positive(Vector3 v) => Finite(v) && v.x > 0 && v.y > 0 && v.z > 0;
    }

    [Serializable] public sealed class SpecialPOISettings
    {
        [Min(0)] public float footprintDistanceFactor = 2;
        [Range(0, 1)] public float chunkDistanceRatio = .2f;
        [Range(1, 10)] public int maxRetries = 10;
        [Range(100, 100000)] public int searchBudgetPerRetry = 10000;
        public bool showDebugLocationLabels = true;
        public TMPro.TMP_FontAsset labelFont;
    }

    [Serializable] public sealed class SpecialPOIPlacement
    {
        public SpecialPOIDefinition definition;
        public CityChunkNode chunk;
        public string lotId;
        public Bounds bounds;
        public Vector3 position;
        public Quaternion rotation;
    }

    [Serializable] public sealed class GeneratedSpecialPOI
    {
        public SpecialPOIDefinition definition;
        public string id, displayName;
        public GameObject instance;
        public Bounds bounds;
        public CityChunkNode chunk;
        public GeneratedLocation location;
    }
}

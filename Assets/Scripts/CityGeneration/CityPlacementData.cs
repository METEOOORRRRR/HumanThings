using System.Collections.Generic;
using UnityEngine;

namespace EarthRecovery
{
    public enum VehiclePlacementMode { Parked, Roadside, Random }
    public sealed class CityPlacementRequest
    {
        public string id, group;
        public LocationType locationType;
        public CityAssetQuery query;
        public List<HumanThingsPlacementZone> zones = new();
        public int count, priority;
        public bool required, surface, junction;
        public float spacing;
        public VehiclePlacementMode vehicleMode;
    }
    public sealed class CompiledCityLayout
    {
        public int seed;
        public float sidewalkWidth;
        public List<HumanThingsPlacementZone> zones = new();
        public List<CityPlacementRequest> requests = new();
        public List<string> warnings = new();
    }
    public sealed class HumanThingsPlacement
    {
        public HumanThingsAssetEntry asset;
        public string group, zoneId, requestId;
        public Vector3 position, scale = Vector3.one;
        public Quaternion rotation;
        public Bounds footprint;
        public bool surface;
    }
    public sealed class HumanThingsBlockPlan
    {
        public readonly List<HumanThingsPlacementZone> zones = new();
        public readonly List<HumanThingsPlacement> placements = new();
        public readonly List<Bounds> entrances = new();
        public readonly List<Bounds> reserved = new();
        public readonly List<string> warnings = new();
    }
    public static class HumanThingsAssetEligibility
    {
        public static bool Usable(HumanThingsAssetEntry e) => e != null && e.prefab != null && e.placementVersion > 0 && e.placementEnabled
            && Finite(e.boundsCenter) && Finite(e.boundsSize) && e.boundsSize.x > .01f && e.boundsSize.z > .01f && e.boundsSize.y >= 0
            && Finite(e.prefab.transform.localScale) && Mathf.Abs(e.prefab.transform.localScale.x * e.prefab.transform.localScale.y * e.prefab.transform.localScale.z) > .000001f
            && Finite(e.localForward) && new Vector2(e.localForward.x, e.localForward.z).sqrMagnitude > .0001f
            && float.IsFinite(e.footprintWidth) && e.footprintWidth > 0 && float.IsFinite(e.footprintDepth) && e.footprintDepth > 0 && float.IsFinite(e.groundOffset);
        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}

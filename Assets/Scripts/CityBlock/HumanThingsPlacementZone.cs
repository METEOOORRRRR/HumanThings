using System;
using UnityEngine;

namespace EarthRecovery
{
    public enum PlacementZoneKind { RoadZone, SidewalkZone, BuildingLot }

    [Serializable]
    public sealed class HumanThingsPlacementZone
    {
        public string id;
        public PlacementZoneKind kind;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector2 size;
        public Vector3 roadDirection;
        public Bounds Bounds => new Bounds(position, new Vector3(size.x, .1f, size.y));
        public bool Contains(Bounds b, float margin = 0)
        {
            var inv = Quaternion.Inverse(rotation);
            for (int i = 0; i < 4; i++)
            {
                var p = inv * (new Vector3((i & 1) == 0 ? b.min.x : b.max.x, position.y, (i & 2) == 0 ? b.min.z : b.max.z) - position);
                if (Mathf.Abs(p.x) > size.x / 2 - margin + .001f || Mathf.Abs(p.z) > size.y / 2 - margin + .001f) return false;
            }
            return true;
        }
    }
}

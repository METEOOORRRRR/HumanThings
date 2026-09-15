using System.Collections.Generic;
using UnityEngine;

namespace EarthRecovery
{
    [DisallowMultipleComponent]
    public sealed class HumanThingsGeneratedBlock : MonoBehaviour
    {
        public List<HumanThingsPlacementZone> zones = new();
        public List<Bounds> footprints = new();
        public List<Bounds> entrances = new();
        public int seed;
    }
}

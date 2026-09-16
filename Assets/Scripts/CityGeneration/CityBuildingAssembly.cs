using UnityEngine;

namespace EarthRecovery
{
    // Approval belongs to this assembled prefab, never to its source modules.
    public sealed class CityBuildingAssembly : MonoBehaviour
    {
        public string family;
        public bool reviewed;
        public float[] joints;
    }
}

using UnityEngine;
namespace EarthRecovery
{
    public sealed class LocationController : MonoBehaviour
    {
        public LocationDefinition definition;
        public Transform entrance, console, artifactSpawn;
        public Light powerLight;
        public void Present(SiteState state) { if (powerLight != null) powerLight.enabled = state.facility != FacilityState.ModuleRequired; }
    }
}

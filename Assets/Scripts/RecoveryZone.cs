using UnityEngine;
namespace EarthRecovery
{
    // Recovery is validated by Expedition.Tick, never by a client's physics callback.
    public sealed class RecoveryZone : MonoBehaviour { public string authorityZoneId = "BaseRecovery"; }
}

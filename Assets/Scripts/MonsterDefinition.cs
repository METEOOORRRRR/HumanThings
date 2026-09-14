using System;
using UnityEngine;
namespace EarthRecovery
{
    public enum AlertState { Idle, Investigate, Suspicious, Chase, MemoryBehavior, Stunned, Return }
    public enum ReactionTrigger { Seen, Heard, PlacedNearby, Activated, Flash, Equipped }
    [Serializable] public sealed class MemoryRule
    {
        public string id, requiredWorldTag, animationId, optionalAudioCue;
        public float probability = .25f, duration = 6, cooldown = 60, minDistance = 2, maxDistance = 18;
        public int sessionCap = 3;
    }
    [Serializable] public sealed class MonsterReactionRule
    {
        public string monsterId, artifactId, behavior, observationId;
        public ReactionTrigger trigger;
        public float radius = 12, duration = 6.2f, probability = 1;
        public bool overrideChase = true, once = true;
        public string[] contextTags = Array.Empty<string>();
    }
    [CreateAssetMenu(menuName = "Human Things/Monster")]
    public sealed class MonsterDefinition : ScriptableObject
    {
        public string id, displayCode;
        public MonsterKind kind;
        public GameObject prefab;
        public float patrolSpeed = 1.6f, chaseSpeed = 3.1f, senseRange = 18, perceptionTick = .2f, memoryCheckInterval = 3;
        public float memoryGlobalCooldown = 60, investigateTimeout = 8, lostTargetGrace = 2.5f, lightSuspicion = .6f, lightChase = 1.5f;
        public int memorySessionMax = 3;
        public MemoryRule[] memories;
        public MonsterReactionRule[] reactions;
        public string[] spawnTags, routeTags, legacyLines;
        public AudioClip[] legacyClips;
    }
}

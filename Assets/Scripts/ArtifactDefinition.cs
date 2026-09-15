using System;
using UnityEngine;
namespace EarthRecovery
{
    [Serializable] public sealed class CarryProfile { public float speedMultiplier = 1; public bool twoHanded; public float noiseRadius; }
    [Serializable] public sealed class MissionHint
    {
        public string category = "", structure = "", function = "";
    }
    [CreateAssetMenu(menuName = "Human Things/Artifact")]
    public sealed class ArtifactDefinition : ScriptableObject
    {
        public string id, locationId, trueNameKo, trueNameEn;
        public MissionHint missionHint = new();
        public string[] archiveDetails = Array.Empty<string>();
        [TextArea] public string archiveDescription, marsComment;
        public GameObject worldPrefab;
        public CarryProfile carryProfile = new();
        public PuzzleProfile puzzle;
        public MonsterReactionRule[] reactions = Array.Empty<MonsterReactionRule>();
    }
}

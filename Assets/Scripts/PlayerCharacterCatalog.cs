using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    [CreateAssetMenu(menuName = "HumanThings/Player Character Catalog")]
    public sealed class PlayerCharacterCatalog : ScriptableObject
    {
        public const string DefaultId = "neon-vanguard";
        public const string ToxicBunnyId = "toxic-bunny-r31";
        public const string NeonOutriderId = "neon-outrider";
        public const string TomorrowSentinelId = "tomorrows-sentinel";
        public const string AshenSentinelId = "ashen-sentinel";
        public const string NovaGhostScoutId = "nova-ghost-scout";
        public const string CrimsonReclaimerId = "crimson-reclaimer";
        public const string WastelandSentinelId = "wasteland-sentinel";

        [Serializable]
        public sealed class Entry
        {
            public string id;
            public string displayName;
            public HumanThingsCharacterVisualProfile visualProfile;
            public GameObject Prefab => visualProfile != null ? visualProfile.visualPrefab : null;
        }

        public Entry[] characters = Array.Empty<Entry>();
        public static PlayerCharacterCatalog Load() => Resources.Load<PlayerCharacterCatalog>("PlayerCharacterCatalog");

        public Entry Resolve(string id) => characters.FirstOrDefault(c => Usable(c) && c.id == id)
            ?? characters.FirstOrDefault(c => Usable(c) && c.id == DefaultId)
            ?? characters.FirstOrDefault(Usable);

        public string PickId(System.Random random, IEnumerable<string> occupiedIds = null)
        {
            var occupied = new HashSet<string>(occupiedIds ?? Array.Empty<string>());
            var available = characters.Where(c => Usable(c) && !occupied.Contains(c.id)).Select(c => c.id).Distinct().ToArray();
            if (available.Length == 0) throw new InvalidOperationException("No unassigned player character is available.");
            return available[random.Next(available.Length)];
        }

        static bool Usable(Entry entry) => entry != null && !string.IsNullOrWhiteSpace(entry.id) && entry.Prefab != null;

        public void Validate()
        {
            if (characters == null || characters.Length == 0 || characters.Any(c => !Usable(c) || c.id.Length > 64)
                || characters.Select(c => c.id).Distinct().Count() != characters.Length
                || !characters.Any(c => c.id == DefaultId))
                throw new InvalidOperationException("Player characters need unique IDs, visual prefabs and the default character.");
            foreach (var entry in characters)
                if (entry.Prefab.GetComponent<PlayerAvatar>() == null || entry.Prefab.GetComponent<HumanThingsCharacterVisual>() == null)
                    throw new InvalidOperationException("Incomplete gameplay character: " + entry.id);
        }
    }
}

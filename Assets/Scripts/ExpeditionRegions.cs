using System;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    [Serializable] public sealed class ExpeditionRegion
    {
        public string id, name, title, description, image;
    }
    public static class ExpeditionRegions
    {
        public const string DefaultId = "seoul";
        [Serializable] sealed class Catalog { public ExpeditionRegion[] regions; }
        static ExpeditionRegion[] entries;
        public static ExpeditionRegion[] All => entries ??= JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("ExpeditionRegions").text).regions;
        public static ExpeditionRegion Find(string id) => All.FirstOrDefault(r => r.id == id);
        public static Sprite CreateSprite(string id)
        {
            var region = Find(id) ?? throw new ArgumentException("Unknown expedition region", nameof(id));
            var texture = Resources.Load<Texture2D>(region.image);
            if (texture == null) throw new InvalidOperationException("Missing region image: " + region.image);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
        }
    }
}

using System.Collections.Generic;
using System.Linq;

namespace EarthRecovery
{
    public sealed class CityAssetQuery
    {
        public AssetCategory category;
        public string[] subCategories;
        public List<string> tags = new();
        public PlacementSurface surface;
        public int preferredFloors;
        public bool preferCorner;
        public string Label => category + "/" + (subCategories == null ? "Any" : string.Join("|", subCategories));
    }
    public static class CityLayoutAssets
    {
        public static List<HumanThingsAssetEntry> Resolve(HumanThingsAssetDatabase db, CityAssetQuery q) => db.FindAssets(q.category, tags: q.tags)
            .Where(e => HumanThingsAssetEligibility.Usable(e) && !e.againstWall && (e.placementSurface == q.surface || e.placementSurface == PlacementSurface.Any)
                && (q.subCategories == null || q.subCategories.Contains(e.subCategory)))
            .OrderBy(e => q.subCategories == null ? 0 : System.Array.IndexOf(q.subCategories, e.subCategory))
            .ThenBy(e => q.category == AssetCategory.Road && e.cornerCompatible ? 1 : 0).ToList();
        public static CityAssetQuery Building(BuildingUsage usage, bool corner = false) => new()
        {
            category = AssetCategory.Building, surface = PlacementSurface.Ground, preferCorner = corner,
            subCategories = usage switch
            {
                BuildingUsage.Residential => new[] { "Apartment" }, BuildingUsage.Commercial => new[] { "Shop", "Commercial" },
                BuildingUsage.Office => new[] { "Office" }, BuildingUsage.Public => new[] { "Public" },
                _ => null
            }
        };
    }
}

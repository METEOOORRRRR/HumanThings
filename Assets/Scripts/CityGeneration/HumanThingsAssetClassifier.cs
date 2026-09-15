using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EarthRecovery
{
    public static class HumanThingsAssetClassifier
    {
        sealed class Rule
        {
            public readonly AssetCategory category;
            public readonly string subCategory, koreanTag;
            public readonly string[] keywords;
            public Rule(AssetCategory category, string subCategory, string koreanTag, params string[] keywords)
            { this.category = category; this.subCategory = subCategory; this.koreanTag = koreanTag; this.keywords = keywords.Select(Normalize).ToArray(); }
            public bool Matches(string text) => keywords.Any(k => (" " + text + " ").Contains(" " + k + " "));
        }

        // Specific objects precede broad types. Name matches take precedence over directory matches.
        static readonly Rule[] Rules =
        {
            new(AssetCategory.Transit, "BusStop", "버스정류장", "busstop", "bus stop", "bus shelter"),
            new(AssetCategory.Transit, "SubwayEntrance", "지하철입구", "subway", "metro entrance", "metro station"),
            new(AssetCategory.StreetProp, "TrafficLight", "신호등", "trafficlight", "traffic light", "traffic signal", "light pole cross lights", "light pole cross button"),
            new(AssetCategory.StreetProp, "Sign", "표지판", "sign", "signs", "signage", "billboard"),
            new(AssetCategory.Structure, "FireEscape", "비상계단", "fireescape", "fire escape"),
            new(AssetCategory.Vehicle, "Emergency", "긴급차량", "ambulance", "firetruck", "fire truck", "police car", "car police", "car ambo", "police vehicle", "emergency vehicle"),
            new(AssetCategory.Structure, "Rooftop", "옥상", "rooftop", "roof top", "roof"),
            new(AssetCategory.Structure, "Fence", "울타리", "fence", "fencing"),
            new(AssetCategory.Structure, "Stair", "계단", "stair", "stairs", "staircase", "stairway", "steps"),
            new(AssetCategory.Road, "Sidewalk", "인도", "sidewalk", "footpath", "pavement", "curb", "kerb", "path corner", "path straight", "path junction", "path t"),
            new(AssetCategory.Road, "Bridge", "다리", "bridge", "overpass"),
            new(AssetCategory.Road, "Intersection", "교차로", "intersection", "junction", "crossroad", "crossroads", "road cross", "road t", "road 4 way", "road 3 way", "road 4way", "road 3way"),
            new(AssetCategory.Road, "Corner", "도로코너", "road corner", "road turn", "road curve", "road bend"),
            new(AssetCategory.Road, "Straight", "직선도로", "road straight", "road long", "road short"),
            new(AssetCategory.Vehicle, "Bus", "버스", "bus", "buses", "coach"),
            new(AssetCategory.Vehicle, "Truck", "트럭", "truck", "lorry", "pickup"),
            new(AssetCategory.Vehicle, "Van", "밴", "van", "minivan"),
            new(AssetCategory.Vehicle, "Car", "자동차", "car", "cars", "sedan", "taxi", "suv", "hatchback"),
            new(AssetCategory.StreetProp, "Trash", "쓰레기통", "trash", "trashbin", "bin", "garbage", "dumpster", "rubbish", "skip"),
            new(AssetCategory.StreetProp, "Lamp", "가로등", "lamp", "lightpole", "light pole", "streetlight", "street light", "lamp post", "lamppost"),
            new(AssetCategory.StreetProp, "Bench", "벤치", "bench", "benches"),
            new(AssetCategory.StreetProp, "Barrier", "차단물", "barrier", "barricade", "bollard", "traffic cone", "cone", "street divider"),
            new(AssetCategory.StreetProp, "Utility", "거리설비", "utility", "hydrant", "manhole", "mailbox", "postbox", "power pole", "utility pole", "telephone", "transformer", "parking meter", "aircon", "atm", "pipe", "vents", "power box", "power cables", "security camera", "sat dish", "water tower"),
            new(AssetCategory.Nature, "Tree", "나무", "tree", "trees", "palm"),
            new(AssetCategory.Nature, "Bush", "덤불", "bush", "bushes", "hedge", "shrub"),
            new(AssetCategory.Nature, "Plant", "식물", "plant", "plants", "flower", "flowers", "grass", "planter"),
            new(AssetCategory.Building, "Apartment", "아파트", "apartment", "apartments", "residential", "condo"),
            new(AssetCategory.Building, "Office", "사무실", "office", "offices"),
            new(AssetCategory.Building, "Shop", "상점", "shop", "store", "shops", "stores", "restaurant", "cafe", "supermarket"),
            new(AssetCategory.Building, "Public", "공공건물", "hospital", "school", "library", "museum", "town hall", "city hall", "police station", "fire station"),
            new(AssetCategory.Building, "Commercial", "상업건물", "commercial", "mall", "hotel", "bank"),
            new(AssetCategory.Transit, "Other", "대중교통", "transit", "station"),
            new(AssetCategory.Structure, "Other", "구조물", "structure", "wall", "railing", "ladder"),
            new(AssetCategory.Building, "Other", "건물", "building", "buildings", "house", "houses", "warehouse", "skyscraper"),
            new(AssetCategory.Road, "Other", "도로", "road", "roads", "street", "highway", "asphalt"),
            new(AssetCategory.Vehicle, "Other", "차량", "vehicle", "vehicles", "motorcycle", "bike", "bicycle", "trailer"),
            new(AssetCategory.StreetProp, "Other", "거리소품", "streetprop", "street prop", "street furniture"),
            new(AssetCategory.Nature, "Other", "자연", "nature", "vegetation", "rock", "rocks"),
        };

        public static string Normalize(string value)
        {
            value = Regex.Replace(value ?? "", @"([A-Z]+)([A-Z][a-z])", "$1 $2");
            value = Regex.Replace(value, @"([a-z])([A-Z])", "$1 $2");
            value = Regex.Replace(value, @"([\p{L}])([0-9])", "$1 $2");
            value = Regex.Replace(value, @"([0-9])([\p{L}])", "$1 $2");
            return Regex.Replace(value.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
        }

        public static List<string> ParseTags(string value) => (value ?? "").Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        public static HumanThingsAssetEntry Classify(string displayName, string assetPath)
        {
            var name = Normalize(displayName); var path = Normalize(assetPath);
            var matches = Rules.Where(r => r.Matches(name)).ToArray();
            var pathMatches = Rules.Where(r => r.Matches(path)).ToArray();
            var primary = matches.FirstOrDefault() ?? pathMatches.FirstOrDefault();
            var entry = new HumanThingsAssetEntry { displayName = displayName ?? "", assetPath = assetPath ?? "" };
            if (primary != null) { entry.category = primary.category; entry.subCategory = primary.subCategory; }
            var tags = new List<string> { entry.category.ToString().ToLowerInvariant() };
            foreach (var rule in matches.Concat(pathMatches))
            {
                tags.Add(rule.category.ToString().ToLowerInvariant()); tags.Add(rule.subCategory.ToLowerInvariant()); tags.Add(rule.koreanTag);
            }
            string[] categoryKorean = { "건물", "도로", "차량", "거리소품", "대중교통", "자연", "구조물", "미분류" };
            tags.Add(categoryKorean[(int)entry.category]);
            entry.tags = tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return entry;
        }
    }
}

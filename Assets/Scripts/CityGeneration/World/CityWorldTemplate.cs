using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarthRecovery
{
    public enum CityDistrictKind { Commercial, Residential, Mixed, Downtown, Transit, Backstreet, Office }

    [Serializable] public sealed class CityChunkTemplateChoice
    {
        public CityDistrictKind district;
        public CityLayoutTemplate template;
        [Min(0)] public float weight = 1;
    }

    [Serializable] public sealed class CityDistrictAdjacency
    {
        public CityDistrictKind a, b;
        [Min(0)] public float multiplier = 2;
    }

    [CreateAssetMenu(menuName = "HumanThings/City World Template", fileName = "CityWorldTemplate")]
    public sealed class CityWorldTemplate : ScriptableObject
    {
        public string templateName = "StandardCity";
        [TextArea] public string description = "Connected districts populated by existing measured city templates.";
        public FloatRange worldWidthRange = new(540, 600), worldDepthRange = new(540, 600);
        public IntRange chunkCountXRange = new(3, 3), chunkCountZRange = new(3, 3);
        public List<CityChunkTemplateChoice> chunkTemplates = new();
        public List<CityDistrictAdjacency> adjacency = new()
        {
            new() { a = CityDistrictKind.Downtown, b = CityDistrictKind.Commercial },
            new() { a = CityDistrictKind.Downtown, b = CityDistrictKind.Office },
            new() { a = CityDistrictKind.Transit, b = CityDistrictKind.Commercial },
            new() { a = CityDistrictKind.Transit, b = CityDistrictKind.Downtown },
            new() { a = CityDistrictKind.Residential, b = CityDistrictKind.Mixed },
            new() { a = CityDistrictKind.Residential, b = CityDistrictKind.Backstreet },
            new() { a = CityDistrictKind.Backstreet, b = CityDistrictKind.Commercial }
        };
        [Range(0, 1)] public float extraConnectionChance = .22f;
        [Min(1)] public int maxSameDistrictRun = 2;
        public float mainRoadWidth = 14, sideRoadWidth = 8, sidewalkWidth = 6;
        public SpecialPOIDatabase specialPOIDatabase;
        public BaseCampDefinition baseCamp;
        public SpecialPOISettings specialPOISettings = new();
        [Range(.05f, .3f)] public float columnVariation = .12f;
    }
}

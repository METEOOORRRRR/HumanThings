using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarthRecovery
{
    [Serializable] public sealed class RuntimeCityGenerationConfig
    {
        public int seed;
        public DistrictType districtType;
        public float mapWidth, mapDepth, roadWidth, blockSize, sidewalkWidth, buildingDensity;
        public float commercialRatio, residentialRatio, officeRatio, publicRatio, streetPropDensity;
        public int mainRoadCount, sideRoadCount, alleyCount, intersectionCount;
        public int shopCount, subwayEntranceCount, busStopCount, publicBuildingCount, parkedVehicleCount;
        public List<string> warnings = new();
    }
    public static class CityGenerationConfigBuilder
    {
        public static RuntimeCityGenerationConfig Build(CityLayoutTemplate template, int seed)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (template.map == null || template.roads == null || template.buildings == null || template.pois == null || template.vehicles == null || template.props == null)
                throw new ArgumentException("Template rule groups must not be null.");
            if (!Enum.IsDefined(typeof(DistrictType), template.districtType)) throw new ArgumentException("Unknown district type.");
            var c = new RuntimeCityGenerationConfig { seed = seed, districtType = template.districtType };
            var rng = new System.Random(seed);
            float F(FloatRange range, float min, float max, string label)
            {
                float sampled = range.Sample(rng), value = Mathf.Clamp(sampled, min, max);
                if (value != sampled) c.warnings.Add(label + ": sampled value clamped to safe limits.");
                return value;
            }
            int I(IntRange range, int min, int max, string label)
            {
                int sampled = range.Sample(rng), value = Mathf.Clamp(sampled, min, max);
                if (value != sampled) c.warnings.Add(label + ": sampled count clamped to safe limits.");
                return value;
            }
            c.mapWidth = F(template.map.width, 100, 500, "Map width"); c.mapDepth = F(template.map.depth, 100, 500, "Map depth");
            c.mainRoadCount = I(template.roads.mainRoadCount, 1, 3, "Main roads"); c.sideRoadCount = I(template.roads.sideRoadCount, 0, 8, "Side roads");
            c.alleyCount = I(template.roads.alleyCount, 0, 10, "Alleys"); c.intersectionCount = I(template.roads.intersectionCount, 0, 12, "Intersections");
            c.roadWidth = F(template.roads.width, 6, 30, "Road width"); c.blockSize = F(template.roads.blockSize, 16, 70, "Block size");
            if (!float.IsFinite(template.roads.sidewalkWidth)) throw new ArgumentException("Sidewalk width must be finite.");
            c.sidewalkWidth = Mathf.Clamp(template.roads.sidewalkWidth, 2, 12);
            c.buildingDensity = F(template.buildings.density, 0, 1, "Building density");
            c.commercialRatio = F(template.buildings.commercialRatio, 0, 1, "Commercial ratio");
            c.residentialRatio = F(template.buildings.residentialRatio, 0, 1, "Residential ratio");
            c.officeRatio = F(template.buildings.officeRatio, 0, 1, "Office ratio"); c.publicRatio = F(template.buildings.publicRatio, 0, 1, "Public ratio");
            float sum = c.commercialRatio + c.residentialRatio + c.officeRatio + c.publicRatio;
            if (sum <= 0) throw new ArgumentException("At least one building usage ratio must be positive.");
            c.commercialRatio /= sum; c.residentialRatio /= sum; c.officeRatio /= sum; c.publicRatio /= sum;
            c.shopCount = I(template.pois.shopCount, 0, 12, "Shops"); c.subwayEntranceCount = I(template.pois.subwayEntranceCount, 0, 8, "Subways");
            c.busStopCount = I(template.pois.busStopCount, 0, 8, "Bus stops"); c.publicBuildingCount = I(template.pois.publicBuildingCount, 0, 8, "Public buildings");
            c.parkedVehicleCount = I(template.vehicles.parkedCount, 0, 60, "Vehicles"); c.streetPropDensity = F(template.props.density, 0, 1, "Props");
            if (c.mainRoadCount > 1 && c.sideRoadCount == 0)
            { c.sideRoadCount = 1; c.warnings.Add("One connector added so parallel main roads remain connected."); }
            return c;
        }
    }
}

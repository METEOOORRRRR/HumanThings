using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarthRecovery
{
    public enum AssetCategory { Building, Road, Vehicle, StreetProp, Transit, Nature, Structure, Unknown }
    public enum PlacementSurface { Any, Ground, Road, Sidewalk }

    [Serializable]
    public sealed class HumanThingsAssetEntry
    {
        public string assetGuid = "";
        public string displayName = "";
        public string assetPath = "";
        public GameObject prefab;
        public AssetCategory category = AssetCategory.Unknown;
        public string subCategory = "";
        public List<string> tags = new();
        public int placementVersion;
        public Vector3 boundsSize, boundsCenter;
        public PlacementSurface placementSurface;
        public bool frontFacesRoad, cornerCompatible, againstWall, canRotate90, allowRandomRotation;
        public float footprintWidth, footprintDepth, groundOffset;
        public Vector3 localForward = Vector3.forward;
        public bool placementEnabled = true;
        public List<GameObject> companionPrefabs = new();

        public HumanThingsAssetEntry Copy()
        {
            var copy = (HumanThingsAssetEntry)MemberwiseClone();
            copy.tags = new List<string>(tags ?? new List<string>());
            copy.companionPrefabs = new List<GameObject>(companionPrefabs ?? new List<GameObject>());
            return copy;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarthRecovery
{
    public sealed class HumanThingsRuinSurfaces : MonoBehaviour
    {
        readonly Dictionary<Renderer,Material[]> originals=new();
        readonly Dictionary<(Material,bool),Material> copies=new();
        public bool IsApplied=>originals.Count>0;

        public void Apply(HumanThingsRuinProfile profile)
        {
            Restore();
            var world=GetComponentInChildren<GeneratedWorldRoot>();
            if(profile==null || profile.shader==null || world?.result==null) return;
            // Only ordinary city chunk roots: POIs and camp are separate world children.
            foreach(var chunk in world.result.chunks)
            foreach(var r in chunk.city.root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(r.GetComponent<TMPro.TMP_Text>()!=null || r.GetComponent<TextMesh>()!=null) continue;
                var name=r.name.ToLowerInvariant();
                if(name.Contains("tree") || name.Contains("bush") || name.Contains("grass") || name.Contains("ivy")) continue;
                var source=r.sharedMaterials; var target=(Material[])source.Clone(); bool changed=false;
                for(int i=0;i<source.Length;i++)
                {
                    var m=source[i];
                    if(m==null || m.shader.name!="HumanThings/Weathered Environment") continue;
                    bool metal=m.name.StartsWith("HT_Metal_",StringComparison.Ordinal) || m.name.StartsWith("HT_Vehicle_",StringComparison.Ordinal);
                    if(!copies.TryGetValue((m,metal),out var copy))
                    {
                        copy=new Material(m){shader=profile.shader,name="Ruins_"+m.name,hideFlags=HideFlags.DontSave};
                        copy.SetFloat("_Cracks",metal?profile.cracks*.15f:profile.cracks);
                        copy.SetFloat("_Scorch",m.name.StartsWith("HT_Road_",StringComparison.Ordinal)?profile.scorch*.55f:profile.scorch);
                        copy.SetFloat("_DamageScale",profile.scale);
                        copies.Add((m,metal),copy);
                    }
                    target[i]=copy; changed=true;
                }
                if(changed){originals.Add(r,source);r.sharedMaterials=target;}
            }
        }
        [ContextMenu("Restore Existing Surfaces")]
        public void Restore()
        {
            foreach(var pair in originals) if(pair.Key!=null) pair.Key.sharedMaterials=pair.Value;
            originals.Clear();
            foreach(var m in copies.Values) if(Application.isPlaying) Destroy(m); else DestroyImmediate(m);
            copies.Clear();
        }
        [ContextMenu("Apply Ruined Surfaces")]
        public void ApplyDefault()=>Apply(Resources.Load<HumanThingsRuinProfile>("HumanThingsRuinProfile"));
        void OnDisable()=>Restore();
        void OnDestroy()=>Restore();
    }
}

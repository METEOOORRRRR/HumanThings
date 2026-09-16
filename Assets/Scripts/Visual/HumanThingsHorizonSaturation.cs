using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace EarthRecovery
{
    // Reuse URP's RenderGraph-compatible fullscreen pass, restricted to treated cameras.
    public sealed class HumanThingsHorizonSaturation : FullScreenPassRendererFeature
    {
        static readonly Dictionary<Camera,HumanThingsVisualProfile> cameras=new();
        static event System.Action<Camera> removed;
        readonly Dictionary<Camera,Material> instances=new();

        public static void Register(Camera camera,HumanThingsVisualProfile profile) => cameras[camera]=profile;
        public static void Unregister(Camera camera)
        {
            if(ReferenceEquals(camera,null)) return;
            cameras.Remove(camera); removed?.Invoke(camera);
        }
        public static bool Registered(Camera camera) => !ReferenceEquals(camera,null) && cameras.ContainsKey(camera);
        public override void Create()
        {
            base.Create(); removed-=ReleaseCamera; removed+=ReleaseCamera;
        }
        void ReleaseCamera(Camera camera)
        {
            if(!instances.TryGetValue(camera,out var material))return;
            if(material!=null) {if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
            instances.Remove(camera);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData renderingData)
        {
            var camera=renderingData.cameraData.camera;
            if(passMaterial==null || !cameras.TryGetValue(camera,out var profile) || profile==null || !profile.duskSky || profile.horizonSaturation<=0) return;
            if(!instances.TryGetValue(camera,out var material) || material==null)
            {
                material=new Material(passMaterial) {name="Horizon saturation instance",hideFlags=HideFlags.HideAndDontSave};
                instances[camera]=material;
            }
            material.SetVector("_SunDirection",-(Quaternion.Euler(profile.lightRotation)*Vector3.forward));
            material.SetFloat("_GlowHeight",profile.afterglowHeight);
            material.SetFloat("_GlowWidth",profile.afterglowWidth);
            material.SetFloat("_Amount",profile.horizonSaturation);
            material.SetFloat("_GlowEnabled",profile.afterglowStrength>0?1:0);
            // The base pass captures this camera's material; keep the serialized asset untouched.
            var source=passMaterial;
            try { passMaterial=material; base.AddRenderPasses(renderer,ref renderingData); }
            finally { passMaterial=source; }
        }
        protected override void Dispose(bool disposing)
        {
            removed-=ReleaseCamera;
            foreach(var material in instances.Values)
                if(material!=null) {if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
            instances.Clear(); base.Dispose(disposing);
        }
    }
}

using UnityEngine;
namespace EarthRecovery
{
    public sealed class ArtifactRuntime : MonoBehaviour
    {
        public ArtifactDefinition definition;
        public int missionLocation;
        public bool recoveryDetectable = true;
        AudioSource audioSource;
        AudioClip tone;
        bool activated;
        Light flash;
        float flashUntil;
        public void Present(SiteState state)
        {
            if(definition==null)return;
            if(definition.id=="HT_A03_03")
            {
                if(audioSource==null)
                {
                    audioSource=gameObject.AddComponent<AudioSource>();audioSource.spatialBlend=1;audioSource.maxDistance=15;audioSource.volume=.12f;audioSource.loop=true;
                    tone=AudioClip.Create("Restored radio calibration melody",8000,1,8000,false);var samples=new float[8000];
                    for(int i=0;i<samples.Length;i++)samples[i]=Mathf.Sin(i*2*Mathf.PI*(i<4000?440:523.25f)/8000)*.2f;
                    tone.SetData(samples,0);audioSource.clip=tone;
                }
                if(state.activated&&!audioSource.isPlaying)audioSource.Play();if(!state.activated&&audioSource.isPlaying)audioSource.Stop();
            }
            if(definition.id=="HT_A03_02"&&state.activated!=activated)
            {if(flash==null){flash=gameObject.AddComponent<Light>();flash.range=12;flash.intensity=5;}flashUntil=Time.time+.15f;}
            if(flash!=null)flash.enabled=Time.time<flashUntil;
            if(definition.id=="HT_B02_03")transform.localScale=state.activated?new Vector3(2,.4f,2):Vector3.one;
            activated=state.activated;
        }
        void OnDestroy(){if(tone!=null)Destroy(tone);}
    }
}

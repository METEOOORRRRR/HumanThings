Shader "HumanThings/Dusk Sky"
{
    Properties
    {
        _Zenith("Zenith",Color)=(.14,.18,.28,1)
        _Middle("Middle",Color)=(.22,.19,.27,1)
        _Horizon("Horizon",Color)=(.22,.24,.29,1)
        _Lower("Lower",Color)=(.12,.15,.205,1)
        _Glow("Afterglow",Color)=(.42,.19,.12,1)
        _Cloud("Cloud",Color)=(.09,.115,.17,1)
        _Brightness("Brightness",Float)=1
        _GlowStrength("Afterglow Strength",Float)=.35
        _GlowHeight("Afterglow Height",Float)=.035
        _GlowWidth("Afterglow Width",Float)=40
        _CloudCoverage("Cloud Coverage",Float)=.5
        _CloudOpacity("Cloud Opacity",Float)=.6
        _SunDirection("Sun Direction",Vector)=(0,-.07,1,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Zenith,_Middle,_Horizon,_Lower,_Glow,_Cloud,_SunDirection;
            float _Brightness,_GlowStrength,_GlowHeight,_GlowWidth,_CloudCoverage,_CloudOpacity;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.direction=v.positionOS.xyz; return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 d=normalize(i.direction); float h=max(0,d.y);
                float3 sky=lerp(_Horizon.rgb,_Middle.rgb,smoothstep(0,.12,h));
                sky=lerp(sky,_Zenith.rgb,smoothstep(.08,.48,h));
                float2 horizontal=d.xz/max(length(d.xz),.0001);
                float2 sunset=_SunDirection.xz/max(length(_SunDirection.xz),.0001);
                float angular=smoothstep(cos(radians(_GlowWidth)),1,dot(horizontal,sunset));
                float glow=angular*exp(-pow((h-.012)/max(.01,_GlowHeight),2))*_GlowStrength;
                sky+=_Glow.rgb*glow;
                // Fixed world-direction clouds: no parallax, solar disc or bright silver lining.
                float2 uv=d.xz/(h+.28)*float2(3.2,7.5);
                float n=Noise(uv)*.52+Noise(uv*2.03+17)*.26+Noise(uv*4.1-9)*.14+Noise(uv*8.2+5)*.08;
                float clouds=smoothstep(1-_CloudCoverage,1-_CloudCoverage+.18,n)*_CloudOpacity*smoothstep(.005,.035,h);
                sky=lerp(sky,_Cloud.rgb+_Glow.rgb*glow*.12,clouds);
                sky=lerp(_Lower.rgb,sky,smoothstep(-.07,.015,d.y));
                return half4(sky*_Brightness,1);
            }
            ENDHLSL
        }
    }
}

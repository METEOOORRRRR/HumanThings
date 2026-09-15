Shader "HumanThings/Weathered Environment"
{
    Properties
    {
        _BaseMap("Original Palette", 2D) = "white" {}
        _BaseColor("Original Tint", Color) = (1,1,1,1)
        _DetailMap("Packed Surface Detail", 2D) = "gray" {}
        _PaletteTint("Surface Tint", Color) = (0.8,0.84,0.86,1)
        [HDR] _AmbientSky("Diffuse Sky", Color) = (0.4,0.44,0.46,1)
        [HDR] _AmbientEquator("Diffuse Horizon", Color) = (0.28,0.3,0.31,1)
        [HDR] _AmbientGround("Diffuse Ground", Color) = (0.13,0.14,0.15,1)
        _Saturation("Saturation", Range(0,1)) = 0.3
        _Brightness("Brightness", Range(0.2,1.5)) = 0.85
        _ValueFloor("Minimum Surface Reflectance", Range(0,0.15)) = 0.045
        _Dirt("Grime", Range(0,1)) = 0.3
        _DetailStrength("Fine Detail", Range(0,1)) = 0.15
        _DetailNormal("Detail Normal", Range(0,0.2)) = 0.03
        _Wetness("Wet Patches", Range(0,1)) = 0.2
        _Rust("Rust", Range(0,1)) = 0
        _Smoothness("Dry Smoothness", Range(0,1)) = 0.12
        _Metallic("Metallic", Range(0,1)) = 0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.01
        _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST, _BaseColor, _PaletteTint, _AmbientSky, _AmbientEquator, _AmbientGround;
        float _Saturation, _Brightness, _ValueFloor, _Dirt, _DetailStrength, _DetailNormal, _Wetness, _Rust, _Smoothness, _Metallic, _Cutoff, _Cull;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; float fog:TEXCOORD3; float height:TEXCOORD4; UNITY_VERTEX_OUTPUT_STEREO };
        Varyings Vert(Attributes v)
        {
            Varyings o=(Varyings)0; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
            o.positionCS=p.positionCS; o.positionWS=p.positionWS; o.normalWS=TransformObjectToWorldNormal(v.normalOS);
            o.uv=TRANSFORM_TEX(v.uv,_BaseMap); o.fog=ComputeFogFactor(o.positionCS.z);
            o.height=p.positionWS.y-TransformObjectToWorld(float3(0,0,0)).y; return o;
        }
        half4 Base(Varyings i) { half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor; clip(c.a-_Cutoff); return c; }
        half4 Detail(float3 p, float3 n)
        {
            float3 w=pow(abs(n),4); w/=max(dot(w,1),.001);
            return SAMPLE_TEXTURE2D(_DetailMap,sampler_DetailMap,p.zy*.35)*w.x
                + SAMPLE_TEXTURE2D(_DetailMap,sampler_DetailMap,p.xz*.35)*w.y
                + SAMPLE_TEXTURE2D(_DetailMap,sampler_DetailMap,p.xy*.35)*w.z;
        }
        half4 Frag(Varyings i):SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            half3 n=normalize(i.normalWS); half4 d=Detail(i.positionWS,n);
            half4 base=Base(i); half l=dot(base.rgb,half3(.2126,.7152,.0722));
            half3 color=lerp(l.xxx,base.rgb,_Saturation)*_PaletteTint.rgb*_Brightness;
            color=max(color,_ValueFloor.xxx);
            half wall=1-saturate(n.y); half bottom=1-smoothstep(.15,2.8,i.height);
            half streak=SAMPLE_TEXTURE2D(_DetailMap,sampler_DetailMap,float2((i.positionWS.x+i.positionWS.z)*.19,i.positionWS.y*.022)).g;
            half grime=_Dirt*saturate(bottom*.5+wall*streak*.5+d.b*.25);
            color*=1-grime*.52;
            color=lerp(color,color*half3(.68,.72,.54),bottom*wall*d.g*_Dirt*.28);
            color=lerp(color,color*half3(.79,.48,.3),_Rust*smoothstep(.52,.78,d.b)*.38);
            color*=1+(d.r-.5)*_DetailStrength;
            half wet=_Wetness*smoothstep(.38,.68,d.g)*smoothstep(.55,.95,n.y);
            color*=1-wet*.16;
            float3 perturb=float3(d.r-.5,d.b-.5,d.a-.5); perturb-=n*dot(n,perturb);
            n=normalize(n+perturb*_DetailNormal);
            SurfaceData surface=(SurfaceData)0; surface.albedo=color; surface.alpha=1; surface.normalTS=half3(0,0,1);
            surface.metallic=_Metallic; surface.specular=half3(.04,.04,.04);
            surface.smoothness=lerp(_Smoothness,.48,wet); surface.occlusion=1-grime*.18;
            InputData input=(InputData)0; input.positionWS=i.positionWS; input.normalWS=n;
            input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS); input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
            input.bakedGI=lerp(lerp(_AmbientGround.rgb,_AmbientEquator.rgb,saturate(n.y+1)),_AmbientSky.rgb,saturate(n.y));
            input.vertexLighting=VertexLighting(i.positionWS,n);
            input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS); input.shadowMask=half4(1,1,1,1);
            half4 result=UniversalFragmentPBR(input,surface); result.rgb=MixFog(result.rgb,i.fog); return result;
        }
        half4 Depth(Varyings i):SV_Target { Base(i); return 0; }
        half4 Normals(Varyings i):SV_Target { Base(i); return half4(normalize(i.normalWS),0); }
        float3 _LightDirection;
        Varyings ShadowVert(Attributes v)
        {
            Varyings o=Vert(v);
            o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.positionWS,o.normalWS,_LightDirection));
            #if UNITY_REVERSED_Z
            o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
            #else
            o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
            #endif
            return o;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit" Tags {"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            ENDHLSL
        }
        Pass { Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment Depth
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass { Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass { Name "DepthNormals" Tags {"LightMode"="DepthNormalsOnly"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Normals
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}

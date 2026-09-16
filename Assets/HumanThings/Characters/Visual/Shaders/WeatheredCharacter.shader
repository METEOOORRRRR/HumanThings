Shader "HumanThings/Weathered Character"
{
    Properties
    {
        [MainTexture] _BaseMap("Original Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Original Tint", Color) = (1,1,1,1)
        _BumpMap("Original RGB Normal", 2D) = "bump" {}
        _MetallicRoughnessMap("Original glTF Roughness G / Metallic B", 2D) = "white" {}
        _Regions("Skin R / Hair G / Cloth B / Equipment A", 2D) = "white" {}
        _WeatherMask("Contact R / Edges G / Cavities B", 2D) = "black" {}
        _DetailMap("Shared Fine Surface Detail", 2D) = "gray" {}
        _PaletteTint("Palette Tint", Color) = (.93,.95,.96,1)
        _DustColor("Dust", Color) = (.27,.285,.28,1)
        [HDR] _AmbientSky("Shared Environment Sky", Color) = (.42,.46,.48,1)
        [HDR] _AmbientEquator("Shared Environment Horizon", Color) = (.28,.3,.31,1)
        [HDR] _AmbientGround("Shared Environment Ground", Color) = (.13,.14,.15,1)
        _Saturation("Saturation", Range(0,1)) = .65
        _Brightness("Brightness", Range(.3,1.3)) = .82
        _WhiteCompression("White Compression", Range(0,1)) = .45
        _ValueFloor("Charcoal Lift", Range(0,.06)) = .032
        _SkinHair("Skin Saturation / Brightness / Smoothness / Hair Smoothness", Vector) = (.88,1.06,.16,.12)
        _Surfaces("Cloth Smoothness / Equipment Smoothness / Metallic / Normal", Vector) = (.1,.3,.28,.8)
        _Weather("Dirt / Dust / Wear / Wetness", Vector) = (.3,.22,.1,0)
        _DetailStrength("Fine Detail", Range(0,.2)) = .08
        _CavityStrength("Cavity Occlusion", Range(0,.5)) = .12
        [HideInInspector] _Cutoff("Cutoff", Float) = 0
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_Cull]
        HLSLINCLUDE
        #define _NORMALMAP 1
        #include "CharacterSurface.hlsl"
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LitPassVertex
            #pragma fragment CharacterLitFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            half4 CharacterLitFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surface;
                InitializeStandardLitSurfaceData(input.uv, surface);
                InputData data;
                InitializeInputData(input, surface.normalTS, data);
                InitializeBakedGIData(input, data);
                // Match the existing city's trilight ambient, rather than a different SH approximation.
                half y = data.normalWS.y;
                data.bakedGI = lerp(lerp(_AmbientGround.rgb, _AmbientEquator.rgb, saturate(y + 1)), _AmbientSky.rgb, saturate(y));
                half4 color = UniversalFragmentPBR(data, surface);
                color.rgb = MixFog(color.rgb, data.fogCoord);
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode"="MotionVectors" }
            ColorMask RG
            HLSLPROGRAM
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment CharacterDepthNormals
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #define _BumpScale 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            half4 CharacterDepthNormals(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3 normal = NormalizeNormalPerPixel(TransformTangentToWorld(CharacterNormal(input.uv), half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));
                #if defined(_GBUFFER_NORMALS_OCT)
                    return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normal) * .5 + .5)), 0);
                #else
                    return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
    }
    Fallback Off
}

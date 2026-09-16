Shader "Hidden/HumanThings/Horizon Saturation"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            float4 _SunDirection;
            float _GlowHeight,_GlowWidth,_Amount,_GlowEnabled;
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.texcoord;
                half4 original=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);
                float depth=SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if(depth>0.000001) return original;
                    float farDepth=0;
                #else
                    if(depth<0.999999) return original;
                    float farDepth=1;
                #endif
                float3 world=ComputeWorldSpacePosition(uv,farDepth,UNITY_MATRIX_I_VP);
                float3 d=normalize(world-_WorldSpaceCameraPos);
                float2 horizontal=d.xz/max(length(d.xz),.0001);
                float2 sunset=_SunDirection.xz/max(length(_SunDirection.xz),.0001);
                float angular=smoothstep(cos(radians(_GlowWidth)),1,dot(horizontal,sunset));
                float band=exp(-pow((max(0,d.y)-.012)/max(.01,_GlowHeight),2));
                float mask=saturate(_Amount)*_GlowEnabled*angular*band*smoothstep(0,.006,d.y);
                // Remove the neutral component at constant Rec.709 luminance after tone mapping.
                float3 color=max(0,original.rgb);
                float luminance=dot(color,float3(.2126,.7152,.0722));
                float3 chroma=color-min(color.r,min(color.g,color.b));
                float chromaLuminance=dot(chroma,float3(.2126,.7152,.0722));
                if(chromaLuminance<.00001) return original;
                float3 vivid=chroma*(luminance/chromaLuminance);
                // Keep the result in gamut without sacrificing luminance.
                float peak=max(vivid.r,max(vivid.g,vivid.b));
                float gamut=peak>1?saturate((1-luminance)/max(.0001,peak-luminance)):1;
                vivid=lerp(luminance.xxx,vivid,gamut);
                return half4(lerp(original.rgb,vivid,mask),original.a);
            }
            ENDHLSL
        }
    }
}

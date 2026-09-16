#ifndef HUMAN_THINGS_CHARACTER_SURFACE
#define HUMAN_THINGS_CHARACTER_SURFACE
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST, _BaseMap_TexelSize;
half4 _BaseColor, _PaletteTint, _DustColor, _SkinHair, _Surfaces, _Weather;
half4 _AmbientSky, _AmbientEquator, _AmbientGround;
half _Saturation, _Brightness, _WhiteCompression, _ValueFloor, _DetailStrength, _CavityStrength;
half _Cutoff, _Surface, _Cull;
CBUFFER_END
TEXTURE2D(_MetallicRoughnessMap); SAMPLER(sampler_MetallicRoughnessMap);
TEXTURE2D(_Regions); SAMPLER(sampler_Regions);
TEXTURE2D(_WeatherMask); SAMPLER(sampler_WeatherMask);
TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);

// glTF normals are imported as RGB tangent-space normals, not DXT5nm.
half3 CharacterNormal(float2 uv)
{
    half2 xy = (SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv).xy * 2 - 1) * _Surfaces.w;
    return half3(xy, sqrt(1 - saturate(dot(xy, xy))));
}

void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
{
    surface = (SurfaceData)0;
    half3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
    half4 region = SAMPLE_TEXTURE2D(_Regions, sampler_Regions, uv);
    region /= max(dot(region, half4(1,1,1,1)), .001);
    half4 weather = SAMPLE_TEXTURE2D(_WeatherMask, sampler_WeatherMask, uv);
    half2 mr = SAMPLE_TEXTURE2D(_MetallicRoughnessMap, sampler_MetallicRoughnessMap, uv).gb;
    half detail = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, uv * 32).r - .5;
    half luma = dot(base, half3(.2126,.7152,.0722));
    half saturation = lerp(_Saturation, _SkinHair.x, region.r);
    half3 color = lerp(luma.xxx, base, saturation) * _PaletteTint.rgb * _Brightness;
    color *= lerp(1, _SkinHair.y, region.r);
    color /= 1 + color * _WhiteCompression;
    color += _ValueFloor * (1 - saturate(luma * 8));
    half hardSurface = region.a;
    half cloth = region.b;
    half weatherable = saturate(cloth + hardSurface);
    half dirt = _Weather.x * weather.r * weatherable;
    half dust = _Weather.y * weather.r * weatherable;
    half wear = _Weather.z * weather.g * hardSurface;
    half wet = _Weather.w * weather.r * weatherable;
    color *= 1 - dirt * .3;
    color = lerp(color, _DustColor.rgb, dust * .4);
    color += wear * .035;
    color *= (1 + detail * _DetailStrength * weatherable) * (1 - wet * .18);
    half roughVariation = lerp(.7, 1, 1 - mr.x);
    half smoothness = dot(region, half4(_SkinHair.z, _SkinHair.w, _Surfaces.x, _Surfaces.y)) * roughVariation;
    smoothness *= 1 - dirt * .45;
    surface.albedo = max(color, 0);
    surface.alpha = 1;
    surface.normalTS = CharacterNormal(uv);
    surface.metallic = mr.y * _Surfaces.z * hardSurface;
    surface.specular = half3(.04,.04,.04);
    surface.smoothness = lerp(smoothness, .4, wet);
    surface.occlusion = 1 - weather.b * _CavityStrength;
    // The source has no emissive texture, clear coat, transmission or stylized rim.
}
#endif

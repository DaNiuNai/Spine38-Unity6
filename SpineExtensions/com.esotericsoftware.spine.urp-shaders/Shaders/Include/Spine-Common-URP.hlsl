#ifndef SPINE_COMMON_URP_INCLUDED
#define SPINE_COMMON_URP_INCLUDED

// URP 17.1+ only defines its deprecated aliases when _FORWARD_PLUS is used.
// Follow the actual cluster keyword without enabling that deprecated variant.
#if defined(USE_CLUSTER_LIGHT_LOOP)
    #define SPINE_USE_CLUSTER_LIGHT_LOOP USE_CLUSTER_LIGHT_LOOP
    #define SPINE_SUBTRACTIVE_LIGHT_CHECK CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
#else
    #define SPINE_USE_CLUSTER_LIGHT_LOOP USE_FORWARD_PLUS
    #define SPINE_SUBTRACTIVE_LIGHT_CHECK FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
#endif

#if defined(USE_FORWARD_PLUS) || defined(USE_CLUSTER_LIGHT_LOOP)
    #define IS_URP_14_OR_NEWER 1
    #define IS_URP_12_OR_NEWER 1
#else
    #define IS_URP_14_OR_NEWER 0
    #ifdef UNIVERSAL_REALTIME_LIGHTS_INCLUDED
        #define IS_URP_12_OR_NEWER 1
    #else
        #define IS_URP_12_OR_NEWER 0
    #endif
#endif
#if IS_URP_14_OR_NEWER && !defined(_USE_WEBGL1_LIGHTS)
    #define IS_URP_15_OR_NEWER 1
    #include "Packages/com.unity.render-pipelines.core/Runtime/Lighting/ProbeVolume/ProbeVolume.hlsl"
    #if defined(_APVWorldOffset)
        #define IS_URP_17_OR_NEWER 1
    #else
        #define IS_URP_17_OR_NEWER 0
    #endif
#else
    #define IS_URP_15_OR_NEWER 0
#endif

#if defined(_WRITE_RENDERING_LAYERS) && IS_URP_14_OR_NEWER
#define USE_WRITE_RENDERING_LAYERS
#endif

#if defined(_LIGHT_LAYERS) && IS_URP_12_OR_NEWER
#define USE_LIGHT_LAYERS
#endif

#if defined(_LIGHT_COOKIES) && IS_URP_12_OR_NEWER
#define USE_LIGHT_COOKIES
#endif

#ifdef USE_LIGHT_LAYERS
uint GetMeshRenderingLayerBackwardsCompatible()
{
    #if IS_URP_14_OR_NEWER
    return GetMeshRenderingLayer();
    #elif IS_URP_12_OR_NEWER
    return GetMeshRenderingLightLayer();
    #else
    return 0;
    #endif
}
#else
uint GetMeshRenderingLayerBackwardsCompatible()
{
    return 0;
}
#endif

#if SPINE_USE_CLUSTER_LIGHT_LOOP
// note: LIGHT_LOOP_BEGIN accesses inputData.normalizedScreenSpaceUV and inputData.positionWS.
#define LIGHT_LOOP_BEGIN_SPINE LIGHT_LOOP_BEGIN
#define LIGHT_LOOP_END_SPINE LIGHT_LOOP_END
#elif !_USE_WEBGL1_LIGHTS
#define LIGHT_LOOP_BEGIN_SPINE(lightCount) \
    for (uint lightIndex = 0u; lightIndex < lightCount; ++lightIndex) {

#define LIGHT_LOOP_END_SPINE }
#else
// WebGL 1 doesn't support variable for loop conditions
#define LIGHT_LOOP_BEGIN_SPINE(lightCount) \
    for (int lightIndex = 0; lightIndex < _WEBGL1_MAX_LIGHTS; ++lightIndex) { \
        if (lightIndex >= (int)lightCount) break;

#define LIGHT_LOOP_END_SPINE }
#endif

#endif

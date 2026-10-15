// One material for every particle and mesh effect in URP.
//
// Output is premultiplied (Blend One OneMinusSrcAlpha). _AlphaBlend picks the look per material:
// 0 = additive, 1 = alpha blended, anything between = both. Additive and alpha layers therefore
// share one shader: particle systems with the same material and the same stream list batch
// together, and mesh renderers stay SRP Batcher compatible.
//
// Toggles are shader_feature_local: set them on the material asset in the Editor. A keyword
// switched on from gameplay code was never built into the player - swap materials instead.
//
// Per-particle values come from the Particle System Renderer > Custom Vertex Streams, in this
// exact order (the stream list in the Inspector must match, or TEXCOORDs shift):
//
//   Position            POSITION.xyz
//   Color               COLOR.xyzw
//   UV                  TEXCOORD0.xy
//   Custom1.xyzw        TEXCOORD0.zw | TEXCOORD1.xy
//   StableRandom.x      TEXCOORD1.z
//
//   Custom1.x  erosion added to _Erosion          (Custom Data curve, 0 -> 1 over life)
//   Custom1.y  intensity added to _Intensity      (Custom Data curve, burst at birth)
//   Custom1.zw main texture UV offset             (random between two constants = random frame)
//   StableRandom.x  noise offset per particle     (no Custom Data needed)
//
// A MeshRenderer has no streams, and missing vertex inputs are NOT zero (w defaults to 1, a missing
// TEXCOORD1 reads whatever is bound): materials on MeshRenderers set _CustomData to 0.

Shader "VFX/Uber"
{
    Properties
    {
        [Header(Main)]
        _MainTex ("Main (RGB color, A shape)", 2D) = "white" {}
        _MainPan ("Main pan (xy units/s)", Vector) = (0, 0, 0, 0)
        [HDR] _Color ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 1
        _AlphaBlend ("Blend (0 add, 1 alpha)", Range(0, 1)) = 0
        _AlphaPower ("Shape contrast", Range(0.2, 4)) = 1
        [ToggleUI] _CustomData ("Read particle Custom Data streams (off for MeshRenderers)", Float) = 1

        [Header(Ramp)]
        [Toggle(_RAMP_ON)] _RampOn ("Color from ramp (shape alpha picks the color)", Float) = 0
        [NoScaleOffset] _RampTex ("Ramp (LUT, clamp, no compression)", 2D) = "white" {}
        _RampOffset ("Ramp offset", Range(-1, 1)) = 0

        [Header(Noise)]
        [Toggle(_NOISE_ON)] _NoiseOn ("Noise (erosion source + distortion)", Float) = 0
        _NoiseTex ("Noise (R, sRGB off)", 2D) = "gray" {}
        _NoisePan ("Noise pan (xy units/s)", Vector) = (0, 0, 0, 0)
        _Distortion ("UV distortion", Range(0, 0.5)) = 0

        [Header(Erosion)]
        _Erosion ("Erosion", Range(0, 1)) = 0
        _ErosionSoftness ("Erosion softness", Range(0.001, 1)) = 0.1
        _EdgeWidth ("Burn edge width", Range(0, 0.5)) = 0
        [HDR] _EdgeColor ("Burn edge color", Color) = (1, 0.5, 0.1, 1)

        [Header(Mask)]
        [Toggle(_MASK_ON)] _MaskOn ("Static mask (not panned)", Float) = 0
        _MaskTex ("Mask (R, sRGB off)", 2D) = "white" {}

        [Header(UV)]
        [Toggle(_POLAR_ON)] _PolarOn ("Polar UV (x angle, y radius)", Float) = 0

        [Header(Fades)]
        [Toggle(_SOFTPARTICLES_ON)] _SoftOn ("Soft particles (needs depth texture)", Float) = 0
        _SoftDistance ("Soft distance", Float) = 0.5
        [Toggle(_CAMERAFADE_ON)] _CameraFadeOn ("Camera near fade", Float) = 0
        _CameraFadeNear ("Camera fade start", Float) = 0.5
        _CameraFadeRange ("Camera fade range", Float) = 1

        [Header(State)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _RAMP_ON
            #pragma shader_feature_local _NOISE_ON
            #pragma shader_feature_local _MASK_ON
            #pragma shader_feature_local _POLAR_ON
            #pragma shader_feature_local _SOFTPARTICLES_ON
            #pragma shader_feature_local _CAMERAFADE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #if defined(_SOFTPARTICLES_ON)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #endif

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_RampTex);  SAMPLER(sampler_RampTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_MaskTex);  SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainPan;
                half4 _Color;
                half _Intensity;
                half _AlphaBlend;
                half _AlphaPower;
                float _CustomData;
                half _RampOffset;
                float4 _NoiseTex_ST;
                float4 _NoisePan;
                half _Distortion;
                half _Erosion;
                half _ErosionSoftness;
                half _EdgeWidth;
                half4 _EdgeColor;
                float4 _MaskTex_ST;
                float _SoftDistance;
                float _CameraFadeNear;
                float _CameraFadeRange;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float4 uv0 : TEXCOORD0;   // xy UV, zw Custom1.xy
                float4 uv1 : TEXCOORD1;   // xy Custom1.zw, z StableRandom.x
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;      // xy UV, zw main UV offset
                float4 custom : TEXCOORD1;  // x erosion, y intensity add, z random, w eye depth
                #if defined(_SOFTPARTICLES_ON)
                float4 screenPos : TEXCOORD2;
                #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.color = input.color;
                o.uv = float4(input.uv0.xy, input.uv1.xy);
                o.uv.zw *= _CustomData;
                o.custom = float4(input.uv0.zw * _CustomData, input.uv1.z * _CustomData, -pos.positionVS.z);
                #if defined(_SOFTPARTICLES_ON)
                o.screenPos = ComputeScreenPos(pos.positionCS);
                #endif
                return o;
            }

            float2 ToPolar(float2 uv)
            {
                float2 d = uv * 2.0 - 1.0;
                return float2(atan2(d.y, d.x) * (0.5 / PI) + 0.5, length(d));
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 baseUV = i.uv.xy;
                #if defined(_POLAR_ON)
                baseUV = ToPolar(baseUV);
                #endif

                float t = _Time.y;
                float random = i.custom.z;
                float2 mainUV = baseUV * _MainTex_ST.xy + _MainTex_ST.zw + _MainPan.xy * t + i.uv.zw;

                half noise = 1;
                #if defined(_NOISE_ON)
                float2 noiseUV = baseUV * _NoiseTex_ST.xy + _NoiseTex_ST.zw + _NoisePan.xy * t + random * 7.31;
                noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;
                mainUV += (noise - 0.5) * _Distortion;
                #endif

                #if defined(_POLAR_ON)
                half4 tex = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, mainUV, 0);
                #else
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV);
                #endif

                half shape = pow(saturate(tex.a), _AlphaPower);

                #if defined(_RAMP_ON)
                half3 color = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(saturate(shape + _RampOffset), 0.5)).rgb;
                #else
                half3 color = tex.rgb;
                #endif

                half erosion = saturate(_Erosion + i.custom.x);
                #if defined(_NOISE_ON)
                half erodeSource = noise;
                #else
                half erodeSource = shape;
                #endif
                half alive = smoothstep(erosion - _ErosionSoftness, erosion, erodeSource);
                half edge = (1 - smoothstep(erosion, erosion + _EdgeWidth, erodeSource)) * step(0.001, erosion) * step(0.0001, _EdgeWidth);

                color *= _Color.rgb * i.color.rgb * (_Intensity + i.custom.y);
                color = lerp(color, _EdgeColor.rgb, edge);

                half alpha = shape * alive * _Color.a * i.color.a;

                #if defined(_MASK_ON)
                alpha *= SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv.xy * _MaskTex_ST.xy + _MaskTex_ST.zw).r;
                #endif

                #if defined(_SOFTPARTICLES_ON)
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                alpha *= saturate((sceneDepth - i.custom.w) / max(_SoftDistance, 0.0001));
                #endif

                #if defined(_CAMERAFADE_ON)
                alpha *= saturate((i.custom.w - _CameraFadeNear) / max(_CameraFadeRange, 0.0001));
                #endif

                alpha = saturate(alpha);
                return half4(color * alpha, alpha * _AlphaBlend);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

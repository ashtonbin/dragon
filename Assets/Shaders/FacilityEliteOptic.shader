Shader "Facility/Elite Optical Glow"
{
    Properties
    {
        _Intensity("Optical intensity", Float) = 2.8
        _Visibility("Emitter visibility", Range(0,1)) = 1
        _StreakStrength("Shared rig glare contribution", Float) = .3
        _HaloStrength("Soft lens bloom", Float) = .58
        _CoreRadius("Round lens core radius", Float) = .011
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+30" "RenderType"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZTest LEqual
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _Visibility;
                float _StreakStrength;
                float _HaloStrength;
                float _CoreRadius;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 optical : TEXCOORD0; float distance : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 origin = TransformObjectToWorld(float3(0,0,0));
                float distanceToEye = distance(origin, GetCameraPositionWS());
                // Put glare just in front of the recessed glass, avoiding half-clipped
                // cores where the camera-facing plane meets a canted metal lens rim.
                origin += normalize(GetCameraPositionWS()-origin)*.014;
                // Optical glare faces each rendering camera and stays horizontal, rather than
                // rotating a luminous slab with the helmet. Each round lens stays small.
                float halfWidth = clamp(distanceToEye * .34, 1.7, 5.5);
                float2 optical = (input.uv - .5) * float2(halfWidth * 2, .68);
                float3 right = UNITY_MATRIX_I_V._m00_m10_m20;
                float3 up = UNITY_MATRIX_I_V._m01_m11_m21;
                float3 world = origin + right * optical.x + up * optical.y;
                output.positionCS = TransformWorldToHClip(world);
                output.optical = optical;
                output.distance = distanceToEye;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.optical;
                float x = p.x, y = p.y;
                float radius = length(p);
                // Continuous soft falloff: no rectangular luminous eye surface.
                float halo = exp(-dot(p,p) / .0072);
                float innerGlow = exp(-dot(p,p) / .00072);
                float hotCore = exp(-dot(p,p) / (_CoreRadius*_CoreRadius));
                float heart = exp(-dot(p,p) / .000075);
                float streakLength = clamp(.58 + input.distance * .035, .65, 1.5);
                float edge = 1 - smoothstep(clamp(input.distance * .34,1.7,5.5)*.78,
                                           clamp(input.distance * .34,1.7,5.5),abs(x));
                float streakWidth = max(.0048, fwidth(y)*.8);
                float streak = exp(-abs(x) / streakLength) * exp(-y*y / (streakWidth*streakWidth)) * edge;
                float streakHaze = exp(-abs(x) / (streakLength*.76)) * exp(-y*y / .00023) * edge;
                // Small diffraction spikes keep the single emitter fierce.
                float spikes = exp(-pow((y-x*.78)/.0055,2)) * exp(-radius/.028)
                             + exp(-pow((y+x*1.15)/.0042,2)) * exp(-radius/.025);
                float shimmer = .97 + .03 * sin(_Time.y*17.0);
                float3 red = float3(1.0,.006,.0015);
                float3 color = red * ((halo*.52 + innerGlow*1.25)*_HaloStrength
                             + (streak*1.30 + streakHaze*.20)*_StreakStrength + spikes*.40)
                             + float3(1.0,.58,.43) * hotCore*3.4
                             + float3(1.0,.96,.88) * heart*4.0;
                float farFade = 1 - smoothstep(38.0,70.0,input.distance);
                return half4(color * (_Intensity * _Visibility * farFade * shimmer), 0);
            }
            ENDHLSL
        }
    }
}

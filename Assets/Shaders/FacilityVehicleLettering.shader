Shader "Facility/Vehicle Lettering"
{
    Properties
    {
        [MainTexture] _MainTex ("Font Atlas", 2D) = "white" {}
        [MainColor] _BaseColor ("Lettering Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "Lit Vehicle Lettering"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest LEqual
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_instancing

            // Transparent lettering samples the actual shadow atlas, including in screen-shadow renderers.
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : COLOR;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs vertex = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertex.positionCS;
                output.positionWS = vertex.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _BaseColor;
                output.fogFactor = ComputeFogFactor(vertex.positionCS.z);
                return output;
            }

            half3 IncidentDiffuse(Light light, half3 normalWS, uint renderingLayers)
            {
                #ifdef _LIGHT_LAYERS
                    if (!IsMatchingLightLayer(light.layerMask, renderingLayers)) return 0;
                #endif
                return light.color * (saturate(dot(normalWS, light.direction))
                    * light.distanceAttenuation * light.shadowAttenuation);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half alpha = input.color.a * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                clip(alpha - (1.0h / 255.0h));
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                uint renderingLayers = GetMeshRenderingLayer();
                half4 shadowMask = unity_ProbesOcclusion;

                // There is no constant visibility floor or emission: all lettering follows its surroundings.
                half3 illumination = max(half3(0,0,0), SampleSH(normalWS));
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, shadowMask);
                illumination += IncidentDiffuse(mainLight, normalWS, renderingLayers);

                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    #if USE_CLUSTER_LIGHT_LOOP
                        [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
                        {
                            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                            Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                            illumination += IncidentDiffuse(light, normalWS, renderingLayers);
                        }
                    #endif
                    uint lightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                        illumination += IncidentDiffuse(light, normalWS, renderingLayers);
                    LIGHT_LOOP_END
                #endif

                return half4(MixFog(input.color.rgb * illumination, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

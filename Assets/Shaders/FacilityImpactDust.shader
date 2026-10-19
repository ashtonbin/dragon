Shader "Facility/Soft Impact Dust"
{
    Properties { _BaseColor("Tint", Color) = (.65,.62,.58,.26) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color * _BaseColor;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float radius = length(p);
                float edge = saturate((1.0 - radius) * 2.0);
                float mottling = .72 + .18 * sin(p.x * 17.0 + sin(p.y * 13.0)) + .10 * sin(p.y * 23.0);
                half4 col = i.color;
                col.a *= edge * edge * mottling;
                return col;
            }
            ENDHLSL
        }
    }
}

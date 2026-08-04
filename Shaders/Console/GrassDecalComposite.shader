Shader "Hidden/GrassSystem/DecalComposite"
{
    Properties
    {
        _MainTex ("Base Map", 2D) = "white" {}
        _OverlayTex ("Overlay", 2D) = "white" {}
        _OverlayRect ("Overlay Rect (u0, v0, du, dv)", Vector) = (0, 0, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            ZTest Always
            Cull Off
            ZWrite Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            TEXTURE2D(_OverlayTex);
            SAMPLER(sampler_MainTex);
            SAMPLER(sampler_OverlayTex);

            float4 _OverlayRect;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 baseC = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float2 ouv = (i.uv - _OverlayRect.xy) / max(_OverlayRect.zw, 1e-6);
                float inside = step(0.0, ouv.x) * step(ouv.x, 1.0) * step(0.0, ouv.y) * step(ouv.y, 1.0);
                half4 ov = SAMPLE_TEXTURE2D(_OverlayTex, sampler_OverlayTex, saturate(ouv));
                ov.a *= inside;
                half3 rgb = lerp(baseC.rgb, ov.rgb, ov.a);
                half a = baseC.a + ov.a * (1.0 - baseC.a);
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

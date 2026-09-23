// The 3D dice on the 2D board: its face picture lit by one fixed light (no scene lights needed), drawn in the transparent
// queue so it sorts with the board sprites by sorting order (above the pawns while it rolls). Convex model: back faces are
// culled and no depth is written, so it never hides the UI drawn after it.
Shader "Ludo/DiceShaded"
{
    Properties
    {
        _MainTex ("Faces", 2D) = "white" {}
        _LightDir ("Light direction (towards the light)", Vector) = (-0.45, 0.6, -0.65, 0)
        _Ambient ("Ambient", Range(0, 1)) = 0.55
        _Specular ("Specular", Range(0, 1)) = 0.25
        _Gloss ("Gloss", Range(2, 128)) = 28
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Dice"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _LightDir;
                float _Ambient;
                float _Specular;
                float _Gloss;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                float3 n = normalize(i.normalWS);
                float3 l = normalize(_LightDir.xyz);
                float3 viewDir = float3(0, 0, -1);                         // orthographic camera looking down the board
                float diffuse = saturate(dot(n, l));
                float spec = pow(saturate(dot(n, normalize(l + viewDir))), _Gloss) * _Specular;
                half3 colour = albedo * (_Ambient + (1 - _Ambient) * diffuse) + spec;
                return half4(colour, 1);
            }
            ENDHLSL
        }
    }
}

Shader "FamilyCompany/Production/PlayerV8BalancedAlbedo"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _AmbientFactor ("Neutral Fill", Range(0, 1)) = 0.70
        _KeyFactor ("Soft Form", Range(0, 1)) = 0.18
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half _AmbientFactor;
            half _KeyFactor;

            struct AppData
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 worldNormal : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(AppData input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, input.uv) * _Color;
                // Fixed neutral presentation light: independent of the office sky probe and
                // directional lights. It keeps the pre-painted hair/cloth detail but cannot
                // create specular, emission or direction-dependent silver hair bands.
                const half3 keyToSurface = half3(-0.36h, 0.80h, -0.48h);
                half form = saturate(dot(normalize(input.worldNormal), keyToSurface));
                half illumination = saturate(_AmbientFactor + _KeyFactor * form);
                return fixed4(albedo.rgb * illumination, albedo.a);
            }
            ENDCG
        }
    }

    Fallback "Diffuse"
}

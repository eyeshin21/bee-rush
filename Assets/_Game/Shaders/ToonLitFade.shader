Shader "HoneyBeeRush/ToonLitFade"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Ambient ("Ambient", Range(0,1)) = 0.55
        _ShadeTint ("Shade Tint", Color) = (0.62,0.60,0.72,1)
        _Emission ("Emission", Range(0,2)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200

        Pass
        {
            Tags { "LightMode"="Always" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 nrm : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _Emission)
            UNITY_INSTANCING_BUFFER_END(Props)

            float _Ambient;
            float4 _ShadeTint;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.nrm = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 baseCol = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float emission = UNITY_ACCESS_INSTANCED_PROP(Props, _Emission);
                float3 n = normalize(i.nrm);
                float3 keyDir = normalize(float3(-0.32, 0.70, -0.64));
                float key = saturate(dot(n, keyDir) * 0.5 + 0.5);
                key = smoothstep(0.10, 0.95, key);
                float lambert = saturate(_Ambient + key * (1.0 - _Ambient));
                float3 shaded = lerp(baseCol.rgb * _ShadeTint.rgb, baseCol.rgb, lambert);
                shaded += baseCol.rgb * emission;
                return fixed4(shaded, baseCol.a);
            }
            ENDCG
        }
    }

    Fallback "Unlit/Transparent"
}

Shader "HoneyBeeRush/ToonLit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Ambient ("Ambient", Range(0,1)) = 0.55
        _ShadeTint ("Shade Tint", Color) = (0.62,0.60,0.72,1)
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3.5
        _RimStrength ("Rim Strength", Range(0,2)) = 0.22
        _Emission ("Emission", Range(0,2)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Tags { "LightMode"="Always" }
            Cull Back
            ZWrite On

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
                float3 viewDir : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _Emission)
            UNITY_INSTANCING_BUFFER_END(Props)

            float _Ambient;
            float4 _ShadeTint;
            float4 _RimColor;
            float _RimPower;
            float _RimStrength;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.nrm = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 baseCol = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float emission = UNITY_ACCESS_INSTANCED_PROP(Props, _Emission);

                float3 n = normalize(i.nrm);
                float3 keyDir = normalize(float3(-0.32, 0.70, -0.64));
                float3 fillDir = normalize(float3(0.55, -0.25, -0.79));

                float key = saturate(dot(n, keyDir) * 0.5 + 0.5);
                key = smoothstep(0.10, 0.95, key);
                float fill = saturate(dot(n, fillDir) * 0.5 + 0.5) * 0.28;

                float lambert = saturate(_Ambient + key * (1.0 - _Ambient) + fill);

                float3 shaded = lerp(baseCol.rgb * _ShadeTint.rgb, baseCol.rgb, lambert);

                float rim = pow(1.0 - saturate(dot(n, normalize(i.viewDir))), _RimPower);
                shaded += _RimColor.rgb * rim * _RimStrength;

                shaded += baseCol.rgb * emission;

                return fixed4(shaded, baseCol.a);
            }
            ENDCG
        }
    }

    Fallback "Unlit/Color"
}

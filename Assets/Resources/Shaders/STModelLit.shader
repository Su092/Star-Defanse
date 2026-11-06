// 星轨防线 · 模型赛璐璐光照
//
// 与 Godot 端 StandardMaterial3D + 两盏方向光的观感对齐，
// 但改用硬边（两段式）漫反射 + 轮廓光，符合项目「硬边阴影」的视觉约定。
// 不接收实时阴影：战场地面是无光照的地图贴图，阴影反而会穿帮，
// 单位脚下改用程序化投影贴片（见 STBattleView.SpawnBlobShadow）。

Shader "StarLane/ModelLit"
{
    Properties
    {
        _MainTex ("Base Color", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Bands ("Cel Threshold", Range(0,1)) = 0.42
        _ShadeColor ("Shade Tint", Color) = (0.62,0.52,0.92,1)
        _RimColor ("Rim Color", Color) = (0.35,0.95,1,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3.2
        _RimStrength ("Rim Strength", Range(0,2)) = 0.5
        _Emission ("Emission", Color) = (0,0,0,1)
        _Flash ("Flash", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _ShadeColor;
            fixed4 _RimColor;
            fixed4 _Emission;
            float _Bands;
            float _RimPower;
            float _RimStrength;
            float _Flash;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;
                float3 n = normalize(i.worldNormal);

                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float ndl = dot(n, l) * 0.5 + 0.5;

                // 两段式硬边：亮面 / 暗面
                float lit = ndl >= _Bands ? 1.0 : 0.0;
                float soft = smoothstep(_Bands - 0.03, _Bands + 0.03, ndl);
                lit = lerp(lit, soft, 0.35);

                fixed3 lightCol = _LightColor0.rgb * 0.9 + unity_AmbientSky.rgb;
                fixed3 bright = albedo.rgb * lightCol;
                fixed3 shade = albedo.rgb * _ShadeColor.rgb * (unity_AmbientSky.rgb + 0.22);

                fixed3 col = lerp(shade, bright, lit);

                // 轮廓光（从相机侧后方打亮边缘）
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float rim = pow(1.0 - saturate(dot(n, viewDir)), _RimPower);
                col += _RimColor.rgb * rim * _RimStrength * saturate(ndl + 0.25);

                col += _Emission.rgb;
                col = lerp(col, fixed3(1.6, 1.6, 1.6), _Flash);

                return fixed4(col, 1.0);
            }
            ENDCG
        }

        // 仅背面补一层压暗（避免低模穿帮时露出纯黑）
        Pass
        {
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _ShadeColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;
                return fixed4(albedo.rgb * _ShadeColor.rgb * 0.35, 1.0);
            }
            ENDCG
        }
    }

    Fallback "VertexLit"
}

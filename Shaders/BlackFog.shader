Shader "Custom/BlackFog"
{
    Properties
    {
        _Color ("Fog Color", Color) = (0, 0, 0, 1)
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _Density ("Core Opacity", Range(0.5, 1)) = 0.93
        _EdgeWidth ("Edge Softness", Range(0.1, 0.9)) = 0.55
        _FlowSpeed ("Flow Speed", Range(0, 0.5)) = 0.06
        _Distort ("Edge Distortion", Range(0, 1)) = 0.45
        _Breath ("Breathing", Range(0, 0.15)) = 0.04
        _Shrink ("Light Shrink", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 viewDir : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float2 noiseUV1 : TEXCOORD2;
                float2 noiseUV2 : TEXCOORD3;
                float2 noiseUV3 : TEXCOORD4;
            };

            fixed4 _Color;
            sampler2D _NoiseTex;
            float _Density;
            float _EdgeWidth;
            float _FlowSpeed;
            float _Distort;
            float _Breath;
            float _Shrink;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = v.normal;
                o.viewDir = normalize(ObjSpaceViewDir(v.vertex));

                float t = _Time.y * _FlowSpeed;
                // 3 层不同尺度噪声: 大尺度形变 + 中尺度涡流 + 小尺度毛边
                o.noiseUV1 = v.uv * 1.2 + t * float2(0.15, 0.4);
                o.noiseUV2 = v.uv * 2.8 + t * float2(-0.25, 0.2);
                o.noiseUV3 = v.uv * 5.5 + t * float2(0.35, -0.18);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 视角厚度: 中心=1, 边缘=0
                float ndv = saturate(dot(normalize(i.normal), normalize(i.viewDir)));
                float thickness = pow(ndv, 0.3);

                // 3 层噪声
                float n1 = tex2D(_NoiseTex, i.noiseUV1).r;
                float n2 = tex2D(_NoiseTex, i.noiseUV2).r;
                float n3 = tex2D(_NoiseTex, i.noiseUV3).r;

                // 大尺度噪声: 扭曲整体厚度 (松弛形变)
                float thicknessWarp = (n1 - 0.5) * 2.0 * _Distort;
                
                // 边缘区域: 多层扭曲产生流动毛边
                float edgeBoost = saturate(1.0 - thickness);
                float edgeNoise = (n2 - 0.5) * 1.5 * _Distort * (0.3 + edgeBoost * 0.7);
                edgeNoise += (n3 - 0.5) * 1.0 * _Distort * edgeBoost * edgeBoost;

                // 缓慢呼吸
                float breath = sin(_Time.y * 0.6) * _Breath;

                float edgeFactor = thickness + thicknessWarp + edgeNoise + breath;

                // 核心: 厚实; 边缘: 宽幅柔和渐变 (松弛, 非紧绷)
                float alpha;
                float coreStart = _EdgeWidth;
                
                if (edgeFactor > coreStart)
                {
                    // 核心区域: 厚实, 但不过度 (不紧绷)
                    float coreT = smoothstep(coreStart, 1.1, edgeFactor);
                    alpha = lerp(_Density * 0.8, _Density, coreT);
                }
                else
                {
                    // 边缘: 极宽幅柔和渐变, 自然舒展
                    float edgeT = edgeFactor / coreStart;
                    // 用大尺度噪声让边缘不规则 (某些区域延伸更远)
                    float edgeExtend = n1 * 0.15 * edgeBoost;
                    edgeT += edgeExtend;
                    alpha = smoothstep(-0.08, 0.65, edgeT) * _Density;
                }

                // 强光下仅轻微收缩
                alpha *= (1.0 - _Shrink * 0.2);

                return fixed4(_Color.rgb, saturate(alpha));
            }
            ENDCG
        }
    }
}

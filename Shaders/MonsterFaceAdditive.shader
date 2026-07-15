Shader "Custom/MonsterFaceAdditive"
{
    Properties
    {
        _MainTex ("Face Texture", 2D) = "white" {}
        _Intensity ("Glow Intensity", Range(0.5, 5)) = 2.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            // 加法混合: 黑色(0,0,0)完全不显示, 亮色叠加发光
            Blend One One
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float _Intensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                // 加法混合: 直接输出颜色 * 强度
                // 黑色 = (0,0,0) → 不显示
                // 亮色 → 叠加发光
                col.rgb *= _Intensity;
                col.a = 1.0;
                return col;
            }
            ENDCG
        }
    }
}

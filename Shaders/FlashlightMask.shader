Shader "Custom/FlashlightMask"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FlashlightPos ("Flashlight Position", Vector) = (0.5, 0.5, 0, 0)
        _FlashlightRadius ("Radius", Range(0, 1)) = 0.2
        _FlashlightSoftness ("Softness", Range(0, 0.5)) = 0.05
        _Darkness ("Darkness", Range(0, 1)) = 0.98
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" }
        LOD 100

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _FlashlightPos;
            float _FlashlightRadius;
            float _FlashlightSoftness;
            float _Darkness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 center = _FlashlightPos.xy;
                float dist = distance(i.uv, center);

                float mask = smoothstep(
                    _FlashlightRadius - _FlashlightSoftness,
                    _FlashlightRadius + _FlashlightSoftness,
                    dist
                );

                float alpha = mask * _Darkness;
                return fixed4(0, 0, 0, alpha);
            }
            ENDCG
        }
    }
}

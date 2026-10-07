Shader "Arcade/CRTScreen"
{
    Properties
    {
        _MainTex ("Screen (RenderTexture)", 2D) = "black" {}
        _Curvature ("Kavis", Range(0, 0.5)) = 0.12
        _ScanlineStrength ("Scanline gücü", Range(0, 1)) = 0.35
        _ScanlineCount ("Scanline sayısı", Float) = 240
        _MaskStrength ("RGB maske", Range(0, 1)) = 0.15
        _Chromatic ("Renk kayması (texel)", Range(0, 2)) = 0.6
        _Vignette ("Vinyet", Range(0, 2)) = 1.0
        _Brightness ("Parlaklık", Range(0.5, 3)) = 1.5
        _Flicker ("Titreme", Range(0, 0.1)) = 0.02
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Curvature, _ScanlineStrength, _ScanlineCount, _MaskStrength;
            float _Chromatic, _Vignette, _Brightness, _Flicker;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float2 Curve(float2 uv)
            {
                uv = uv * 2.0 - 1.0;
                float2 offset = uv.yx * uv.yx * _Curvature;
                uv += uv * offset;
                return uv * 0.5 + 0.5;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = Curve(i.uv);
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return fixed4(0, 0, 0, 1);

                float2 ca = float2(_MainTex_TexelSize.x * _Chromatic, 0.0);
                float3 col;
                col.r = tex2D(_MainTex, uv + ca).r;
                col.g = tex2D(_MainTex, uv).g;
                col.b = tex2D(_MainTex, uv - ca).b;

                float scan = 0.5 + 0.5 * cos((uv.y * _ScanlineCount - 0.5) * 6.2831853);
                col *= lerp(1.0, scan, _ScanlineStrength);

                float m = fmod(floor(i.pos.x), 3.0);
                float3 mask = float3(m < 0.5 ? 1.0 : 0.0, (m > 0.5 && m < 1.5) ? 1.0 : 0.0, m > 1.5 ? 1.0 : 0.0);
                col *= lerp(float3(1.0, 1.0, 1.0), mask * 1.6 + 0.2, _MaskStrength);

                float2 v = uv * (1.0 - uv);
                col *= saturate(pow(v.x * v.y * 16.0, 0.25 * _Vignette));

                col *= 1.0 + _Flicker * sin(_Time.y * 110.0);
                col *= _Brightness;

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}

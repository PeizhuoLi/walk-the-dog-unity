Shader "Custom/ScreenSpacePoints"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PointSize ("Point Size", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            // TEXCOORD 0 is used to provide extra information for the point size
            // TEXCOORD 1 is used to store the original screenspace coordinate so the result is a cicle

            struct appdata
            {
                float4 vertex : POSITION; // This will be screen space coordinates
                float4 color : COLOR;
                float2 vertex_size_control : TEXCOORD0;
            };

            struct v2f
            {
                // float4 pos : SV_POSITION;
                float4 color : COLOR;
                float pointSize : PSIZE; // Point size
                float2 vertex_size_control : TEXCOORD0;
                float2 pos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float _PointSize;

            v2f vert (appdata v, out float4 outpos: SV_POSITION)
            {
                v2f o;
                // Convert screen space to clip space
                outpos = float4(v.vertex.xy * 2 - 1, 0.99, 1.0);
                outpos.y *= -1; // Invert Y axis for correct orientation
                o.color = v.color;
                o.pointSize = _PointSize * v.vertex_size_control.x;
                o.vertex_size_control.y = o.pointSize;
                o.pos = v.vertex.xy;
                return o;
            }

            fixed4 frag (in v2f i, in UNITY_VPOS_TYPE screenPosition: VPOS) : SV_Target
            {
                float2 center = i.pos * _ScreenParams.xy;
                float dist = distance(center, screenPosition.xy);
                float threshold = i.vertex_size_control.y;
                if (dist > threshold / 2)
                {
                    discard;
                }
                return i.color;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}

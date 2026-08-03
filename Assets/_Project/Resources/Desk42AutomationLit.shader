Shader "Desk42/AutomationLit"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float height : TEXCOORD1;
            };

            fixed4 _Color;

            v2f vert(appdata value)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(value.vertex);
                output.worldNormal = UnityObjectToWorldNormal(value.normal);
                output.height = mul(unity_ObjectToWorld, value.vertex).y;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float3 key = normalize(float3(-0.45, 0.82, -0.35));
                float diffuse = saturate(dot(normalize(input.worldNormal), key));
                float light = 0.58 + diffuse * 0.42;
                float lift = saturate(input.height * 0.025) * 0.05;
                return fixed4(_Color.rgb * (light + lift), _Color.a);
            }
            ENDCG
        }
    }
}

Shader "Unlit/FluidRender_ref"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                uint vertexID : SV_VertexID;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD0;
            };

            struct Vertex
            {
                float4 position;
                float4 normal;
            };

            StructuredBuffer<Vertex> _VertexBuffer;
            float4 col;

            v2f vert (appdata v)
            {
                v2f o;
                float3 vertPos = _VertexBuffer[v.vertexID].position;
                float3 normal = _VertexBuffer[v.vertexID].normal;
                o.vertex = UnityObjectToClipPos(float4(vertPos, 1));
                o.normal = normal;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 lightDir = _WorldSpaceLightPos0;
                //return fixed4(1.0f, 0.0f, 0.0f, 1.0f);
                // return col;
                // return float4(lightDir, 1.0f);
                return col * max(0.0, dot(lightDir, normalize(i.normal)));
                // return col * dot(lightDir, normalize(i.normal)) * 0.5 + 0.5;
            }
            ENDCG
        }
    }
}

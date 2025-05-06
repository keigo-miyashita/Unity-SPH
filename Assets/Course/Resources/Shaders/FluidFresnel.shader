Shader "Unlit/FluidFresnel"
{
    Properties
    {
        _DiffuseTexture ("DiffuseTexture", 2D) = "white" {}
        _DiffuseColor ("DiffuseColor", Color) = (1.0, 1.0, 1.0, 1)
        _RefractiveIndex ("RefractiveIndex", Float) = 1.33
        _F0 ("F0", Vector) = (0.02, 0.02, 0.02)
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
            #include "Lighting.cginc" // _LightColor0を使うためにincludeしなきゃっぽい

            struct appdata
            {
                uint vertexID : SV_VertexID;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float4 worldVertexPos : TEXCOORD1;
            };

            struct Vertex
            {
                float4 position;
                float4 normal;
            };

            StructuredBuffer<Vertex> _VertexBuffer;
            sampler2D _DiffuseTexture; // テクスチャとサンプラを合わせた古い書き方
            float4 _DiffuseTexture_ST;
            float4 _DiffuseColor;
            float _RefractiveIndex;
            float3 _F0;

            float3 FresnelSchlick(float cosTheta, float3 F0)
            {
                return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
            }

            v2f vert (appdata v)
            {
                v2f o;
                float3 vertPos = _VertexBuffer[v.vertexID].position;
                float3 normal = _VertexBuffer[v.vertexID].normal;
                o.vertex = UnityObjectToClipPos(float4(vertPos, 1));
                o.normal = normal;
                o.worldVertexPos = mul(unity_ObjectToWorld, float4(vertPos, 1));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 lightColor = _LightColor0; // デフォルトのライトの色
                float3 lightPos = _WorldSpaceLightPos0; // デフォルトのライトの位置

                float3 cameraPos = _WorldSpaceCameraPos; // デフォルトのカメラ位置

                float4 worldPos = i.worldVertexPos; // このフラグメントのワールド座標
                float3 normal = i.normal;

                float3 lightDir = normalize(lightPos - worldPos); // 頂点→ライトのベクトル
                float3 viewDir = normalize(cameraPos - worldPos); // 頂点→カメラのベクトル
                float3 reflectDir /*= 反射方向を取得*/; // reflectの第一引数はライト→頂点なのでマイナスをつける
                float3 refractDir /*= 屈折方向を取得*/;

                float cosTheta /*= 視線ベクトルと法線ベクトルが作る角度の余弦*/;
                float3 F /*= フレネル項を計算 */;

                // UNITY_SAMPLE_TEXCUBE(cubeMap, direction)でdirection方向のcubeMapテクスチャをサンプル可能
                // Unityではゲームビューのキューブマップが組み込み変数unity_SpecCube0に格納されている
                // lightColorをかけて色にしよう
                float4 reflectColor /* = */;
                float4 refractColor /* = */;
                
                float4 col /*= 反射項：屈折率 = F : 1 - Fでブレンド*/;

                return col;
            }
            ENDCG
        }
    }
}

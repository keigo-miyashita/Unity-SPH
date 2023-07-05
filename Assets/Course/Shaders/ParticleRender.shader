Shader "Custom/ParticleRender"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow
        #pragma instancing_options procedural:setup

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

		struct Input
		{
			float2 uv_MainTex;
		};

        struct ParticleData
        {
            float4 position;
            float4 velocity;
        };

		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
		// Particleデータの構造体バッファ
		StructuredBuffer<ParticleData> _ParticleDataBuffer;
		#endif

		sampler2D _MainTex; // テクスチャ

		half   _Glossiness; // 光沢
		half   _Metallic;   // 金属特性
		fixed4 _Color;      // カラー

		float3 _ObjectScale; // Boidオブジェクトのスケール

		// 頂点シェーダ
		void vert(inout appdata_full v)
		{
		#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED

			// インスタンスIDから Particle のデータを取得
			ParticleData particleData = _ParticleDataBuffer[unity_InstanceID];

			float3 pos = particleData.position.xyz; // Particle の位置を取得
			float3 scl = _ObjectScale;				// Particle のスケールを取得

			// オブジェクト座標からワールド座標に変換する行列を定義
			float4x4 object2world = (float4x4)0;
			// スケール値を代入
			object2world._11_22_33_44 = float4(scl.xyz, 1.0);
			object2world._14_24_34 = pos.xyz;

			// 頂点を座標変換
			v.vertex = mul(object2world, v.vertex);
			// 法線を座標変換
			v.normal = normalize(mul(object2world, v.normal));
		#endif
		}

		void setup()
		{
		}

		// サーフェスシェーダ
		void surf(Input IN, inout SurfaceOutputStandard o)
		{

			// トランスファーファンクション (heat -> color) Kindleman, 8 bit
			const float4 col_000 = float4(0.0, 0.0, 0.0, 1);
			const float4 col_001 = float4(0.1395570645131698, 0.02198279213822543, 0.4598402747775218, 1);
			const float4 col_010 = float4(0.028425818571614185, 0.24267452546380586, 0.5872810647948823, 1);
			const float4 col_011 = float4(0.021455556526349926, 0.4492082077166399, 0.3816968956770979, 1);
			const float4 col_100 = float4(0.03062583987948746, 0.6254558674785432, 0.0829129360827669, 1);
			const float4 col_101 = float4(0.43953003388234246, 0.7674494579667936, 0.037212909121285366, 1);
			const float4 col_110 = float4(0.9795488678271141, 0.8152887113235648, 0.5716520685975728, 1);
			const float4 col_111 = float4(1.0, 1.0, 1.0, 1);

			fixed4 col = (0, 0, 0, 0);

			#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED

			float t = length(_ParticleDataBuffer[unity_InstanceID].velocity) / 10.0f; // 5.0はマジックナンバー
			float tmp = 0;
			if (t > 1) {
				col = col_111;
			}
			else if (t > 0.857142857) {
				tmp = (t - 0.857142857) * 7; // tmp を 0 から 1 に正規化
				col = lerp(col_110, col_111, tmp);
			}
			else if (t > 0.714285714) {
				tmp = (t - 0.714285714) * 7;
				col = lerp(col_101, col_110, tmp);
			}
			else if (t > 0.571428571) {
				tmp = (t - 0.571428571) * 7;
				col = lerp(col_100, col_101, tmp);
			}
			else if (t > 0.428571429) {
				tmp = (t - 0.428571429) * 7;
				col = lerp(col_011, col_100, tmp);
			}
			else if (t > 0.285714286) {
				tmp = (t - 0.285714286) * 7;
				col = lerp(col_010, col_011, tmp);
			}
			else if (t > 0.142857143) {
				tmp = (t - 0.142857143) * 7;
				col = lerp(col_001, col_010, tmp);
			}
			else if (t >= 0) {
				tmp = t * 7;
				col = lerp(col_000, col_001, tmp);
			}
			else {
				col = col_000;
			}
			
			#endif
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex) *  col;
			o.Albedo = c.rgb;
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
		}
		ENDCG
		}
			FallBack "Diffuse"
}

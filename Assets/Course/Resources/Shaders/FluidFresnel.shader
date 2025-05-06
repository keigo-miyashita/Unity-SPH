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
            #include "Lighting.cginc" // _LightColor0���g�����߂�include���Ȃ�����ۂ�

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
            sampler2D _DiffuseTexture; // �e�N�X�`���ƃT���v�������킹���Â�������
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
                float4 lightColor = _LightColor0; // �f�t�H���g�̃��C�g�̐F
                float3 lightPos = _WorldSpaceLightPos0; // �f�t�H���g�̃��C�g�̈ʒu

                float3 cameraPos = _WorldSpaceCameraPos; // �f�t�H���g�̃J�����ʒu

                float4 worldPos = i.worldVertexPos; // ���̃t���O�����g�̃��[���h���W
                float3 normal = i.normal;

                float3 lightDir = normalize(lightPos - worldPos); // ���_�����C�g�̃x�N�g��
                float3 viewDir = normalize(cameraPos - worldPos); // ���_���J�����̃x�N�g��
                float3 reflectDir /*= ���˕������擾*/; // reflect�̑������̓��C�g�����_�Ȃ̂Ń}�C�i�X������
                float3 refractDir /*= ���ܕ������擾*/;

                float cosTheta /*= �����x�N�g���Ɩ@���x�N�g�������p�x�̗]��*/;
                float3 F /*= �t���l�������v�Z */;

                // UNITY_SAMPLE_TEXCUBE(cubeMap, direction)��direction������cubeMap�e�N�X�`�����T���v���\
                // Unity�ł̓Q�[���r���[�̃L���[�u�}�b�v���g�ݍ��ݕϐ�unity_SpecCube0�Ɋi�[����Ă���
                // lightColor�������ĐF�ɂ��悤
                float4 reflectColor /* = */;
                float4 refractColor /* = */;
                
                float4 col /*= ���ˍ��F���ܗ� = F : 1 - F�Ńu�����h*/;

                return col;
            }
            ENDCG
        }
    }
}

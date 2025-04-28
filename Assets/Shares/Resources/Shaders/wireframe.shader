Shader "Custom/GeometryWireFrame"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            #include "UnityCG.cginc"

            fixed4 _Color;

            //���_�V�F�[�_�[�ɓn���Ă��钸�_�f�[�^
            struct appdata
            {
                float4 vertex : POSITION;
            };

            //�W�I���g���V�F�[�_�[����t���O�����g�V�F�[�_�[�ɓn���f�[�^
            struct g2f
            {
                float4 vertex : SV_POSITION;
            };

            //���_�V�F�[�_�[
            appdata vert(appdata v)
            {
                return v;
            }

            //�W�I���g���V�F�[�_�[
            [maxvertexcount(3)] 
            void geom(triangle appdata input[3], inout LineStream<g2f> stream)
            {
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    appdata v = input[i];
                    g2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    stream.Append(o);
                }
            }

            //�t���O�����g�V�F�[�_�[
            fixed4 frag(g2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
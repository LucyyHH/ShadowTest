Shader "BokeGame/ProjectShadowSpriteCaster_2D"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "UseProjector2DShadow"="True"
        }
        
  		Cull Off
		Lighting Off
		ZWrite Off
		Blend One OneMinusSrcAlpha
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
				float2 texcoord : TEXCOORD0;
            };

            uniform sampler2D _MainTex;
			uniform float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                //入射光向量
                fixed3 lightDir = -normalize(_WorldSpaceLightPos0.xyz);
                //相乘得出比例，对灯光向量进行缩放后转换到模型空间就得出顶点偏移量
               v.vertex += mul(unity_WorldToObject, lightDir* 5);
                //顶点转到裁剪空间
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 mainTexColor = tex2D( _MainTex, i.texcoord);
                return fixed4(1,1,1,1);
            }
            ENDCG
        }
    }
}

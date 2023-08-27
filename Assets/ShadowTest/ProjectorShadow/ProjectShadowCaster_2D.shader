Shader "BokeGame/ProjectShadowCaster_2D"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags { 
            "RenderPipeline"="UniversalPipeline"
            "LightMode"="UniversalForward"
            "RenderType"="Transparent" 
            "Queue"="Transparent"
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"

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
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            CBUFFER_END


            v2f vert (appdata v)
            {
                v2f o;
                //相乘得出比例，对灯光向量进行缩放后转换到模型空间就得出顶点偏移量
                v.vertex += normalize(mul(unity_WorldToObject,float4(0,-1,0,0)));
                //顶点转到裁剪空间
                o.vertex = TransformObjectToHClip(v.vertex);
                //o.vertex += TransformObjectToHClip(mul(unity_WorldToObject,float4(0,1,0,0)));
                //o.vertex += float4(0,0.1f,0,0);
                o.texcoord = v.texcoord;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 mainTexColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
                half alpha = step(0.1,mainTexColor.a);
                return half4(alpha,alpha,alpha,alpha);
            }
            ENDHLSL
        }
    }
}

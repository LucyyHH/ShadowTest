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

            // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
			// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
			// #pragma instancing_options assumeuniformscaling
			UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
			UNITY_INSTANCING_BUFFER_END(Props)
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            CBUFFER_END


            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
                
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
                UNITY_SETUP_INSTANCE_ID(i);
                
                half4 mainTexColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
                half alpha = step(0.1,mainTexColor.a);
                return half4(alpha,alpha,alpha,alpha);
            }
            ENDHLSL
        }
    }
}

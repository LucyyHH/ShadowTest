Shader "PostProcess/ColorIntensity"
{
    Properties
    {
        // _BaseMap("BaseMap",2D) = "white"{}     //千万注意不要用这个_BaseMap属性名
        _MainTex("MainTex" , 2D) = "white"{}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 texcoord     : TEXCOORD;
            };
            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD;
            };

            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float  _Intensity;
                float4 _Color;
            CBUFFER_END

            half3 sample_height(const half2 uv)
			{
				return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0);
			}
            
            half2 sample_height_check_edge(half2 uv)
			{
				half3 height = sample_height(uv);
				uv = height.b == 1 ? round(uv * _MainTex_TexelSize.zw) / _MainTex_TexelSize.zw : uv;
				//uv = height.b > 0.9 ? uv * _HeightTex_TexelSize.zw + _HeightTex_TexelSize.xy / 2.0 : uv;
				//uv = height.b > 0.9 ? uv : floor(uv * _HeightTex_ST.zw) / _HeightTex_ST.zw;
				return uv;
			}
           
            Varyings vert (Attributes v)
            {
                Varyings o=(Varyings)0;
                
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.texcoord , _MainTex);
                //o.uv = sample_height_check_edge(o.uv);

                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                half4 FinalColor;

                float4 baseMap = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex , i.uv);
                
                FinalColor = baseMap * _Intensity * _Color;

                return half4(sample_height_check_edge(i.uv), 0, 1);
            }
            ENDHLSL
        }
    }
}
Shader "Custom/LC/Shadow/ProjShadowURP"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Alpha("Alpha", float) = 0.3
		_LandHeight("LandHeight", float) = 0.05
		_LandHeightOffset("LandHeightOffset", float) = 0.5
		_HeightTex ("Height Tex", 2D) = "black" {}
		_HeightTexLeft("Height Tex Left", float) = 0
		_HeightTexLength("Height Tex Length", float) = 0
		_HeightTexBack("Height Tex Back", float) = 0
		_HeightTexWidth("Height Tex Width", float) = 0
		_HeightTexBottom("Height Tex Bottom", float) = 0
		_HeightTexHigh("Height Tex High", float) = 0
		_MaxOffset("Max Offset", float) = 0
		_MainLightDir("Main Light Dir", Vector) = (1, 1, 1, 1)
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" }
		LOD 100

		Pass
		{
			Blend SrcAlpha  OneMinusSrcAlpha
			ZWrite Off
			Cull Back
			ColorMask RGB
			Lighting Off
			ZTest Less
			Fog { Mode Off }
			Offset -1, -1

			Stencil {
				Ref 1
				Comp Greater
				Pass replace
			}
 
			HLSLPROGRAM
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			
			#pragma vertex vert
			#pragma fragment frag

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			TEXTURE2D(_HeightTex);
			SAMPLER(sampler_HeightTex);

			CBUFFER_START(UnityPerMaterial)
			float4 _MainTex_ST;
			float _Alpha;
			float _LandHeight;
			float _LandHeightOffset;
			float4 _HeightTex_ST;
			float _HeightTexLeft;
			float _HeightTexLength;
			float _HeightTexBack;
			float _HeightTexWidth;
			float _HeightTexBottom;
			float _HeightTexHigh;
			float _MaxOffset;
			float4 _MainLightDir;
			CBUFFER_END

			// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
			// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
			// #pragma instancing_options assumeuniformscaling
			UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
			UNITY_INSTANCING_BUFFER_END(Props)
 
			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
 
			v2f vert(appdata v)
			{
				float3 _LightDir = normalize(_MainLightDir.xyz);

				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);

				v.vertex = mul(unity_ObjectToWorld, v.vertex);
				half3 view = normalize(_WorldSpaceCameraPos.xyz - v.vertex);
				//计算投影光照
				float LandHeight = SAMPLE_TEXTURE2D_LOD(_HeightTex, sampler_HeightTex, half2((v.vertex.x - _HeightTexLeft) / _HeightTexLength, (v.vertex.z - _HeightTexBack) / _HeightTexWidth), 0).r * _HeightTexHigh + _HeightTexBottom;
				//面上的点
				float3 p = float3(v.vertex.x, LandHeight, v.vertex.z);
				//源点
				float3 orig = v.vertex;
				//面的法线
				float3 n = float3(0, -1, 0);
				//光的方向
				float3 d = _LightDir;
				float t = dot((p - orig), n)/dot(d, n);
				v.vertex.xyz += d * t;
				
				float2 height = SAMPLE_TEXTURE2D_LOD(_HeightTex, sampler_HeightTex, half2((v.vertex.x - _HeightTexLeft) / _HeightTexLength, (v.vertex.z - _HeightTexBack) / _HeightTexWidth), 0).rg;
				v.vertex.xyz += d * ((v.vertex.y - (height.r * _HeightTexHigh + _HeightTexBottom)) / 2);
				v.vertex.xyz += (height.g * _MaxOffset + _LandHeightOffset + 0.5) * view;

				/*float2 height2 = SAMPLE_TEXTURE2D_LOD(_HeightTex, sampler_HeightTex, half2((v.vertex.x - _HeightTexLeft) / _HeightTexLength, (v.vertex.z - _HeightTexBack) / _HeightTexWidth), 0).rg;
				v.vertex.y = height2.r * _HeightTexHigh + _HeightTexBottom + _LandHeightOffset;	// 这句加了有地方会有问题
				v.vertex.xyz += height2.g * _MaxOffset * view;*/
				
				o.vertex = mul(unity_MatrixVP, v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);

				return o;
			}
 
			float4 frag(v2f i) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
				clip(tex.a - 0.5);
				
				return half4(0, 0, 0, _Alpha);
			}
			
			ENDHLSL
		}
	}

	Fallback "Universal Render Pipeline/Simple Lit"
}

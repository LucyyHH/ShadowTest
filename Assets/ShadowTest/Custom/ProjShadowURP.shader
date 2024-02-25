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
		_MaxHeight1("Max Height 1", float) = 0
		_MaxHeight2("Max Height 2", float) = 0
		_MaxOffset("Max Offset", float) = 0
		_MainLightDir("Main Light Dir(Invalid if Fixed)", Vector) = (1, 1, 1, 1)
		[HideInInspector]_ShadowDir("Shadow Dir", Vector) = (1, 1, 1, 1)
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
			float _MaxHeight1;
			float _MaxHeight2;
			float _MaxOffset;
			float4 _MainLightDir;
			float4 _ShadowDir;
			CBUFFER_END

			// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
			// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
			// #pragma instancing_options assumeuniformscaling
			UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
			UNITY_INSTANCING_BUFFER_END(Props)
			
 			#pragma multi_compile_local __ _FIXED_LIGHT_DIR

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

			float get_height(half2 height, const float original_y)
			{
				const float height1 = height.r * _MaxHeight1;
				const float height2 = height.g * _MaxHeight2 + _MaxHeight1;
				
				return height.g > 0 && height2 < original_y ? height2 : height1;
			}
 
			v2f vert(appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);

				v.vertex = mul(unity_ObjectToWorld, v.vertex);
				const half3 view = normalize(_WorldSpaceCameraPos.xyz - v.vertex);
				//计算高度
				half3 convert_pos = v.vertex;
				half3 light_dir;
				#if _FIXED_LIGHT_DIR
					light_dir = normalize(_ShadowDir.xyz);
					half3 normalize_y = normalize(half3(-_ShadowDir.x / _ShadowDir.y, -1 / _ShadowDir.y, -_ShadowDir.z / _ShadowDir.y));
					/*convert_pos = mul(convert_pos, half3x3(1, 0, 0,
									normalize_y.x, normalize_y.y, normalize_y.z,
									0, 0, 1));*/
					convert_pos = mul(half3x3(1, normalize_y.x, 0,
					                0, normalize_y.y, 0,
					                0, normalize_y.z, 1), convert_pos);
									//light_dir = normalize(_MainLightDir.xyz);
				#else
					light_dir = normalize(_MainLightDir.xyz);
				#endif
				
				
				half3 height = SAMPLE_TEXTURE2D_LOD(_HeightTex, sampler_HeightTex, half2((convert_pos.x - _HeightTexLeft) / _HeightTexLength, (convert_pos.z - _HeightTexBack) / _HeightTexWidth), 0);
				convert_pos.y = get_height(height.rg, convert_pos.y) + _HeightTexBottom;

				#if _FIXED_LIGHT_DIR
					v.vertex.xyz = mul(half3x3(1, -light_dir.x, 0,
					                0, -light_dir.y, 0,
					                0, -light_dir.z, 1), convert_pos);
				
					//v.vertex = mul(light_dir, convert_pos);
					//height = mul(light_dir, height);
				#else
					//面上的点
					const float3 p = float3(v.vertex.x, land_height, v.vertex.z);
					//源点
					const float3 orig = v.vertex;
					//面的法线
					const float3 n = float3(0, -1, 0);
					//光的方向
					const float3 d = light_dir;
					const float t = dot(p - orig, n)/dot(d, n);
					v.vertex.xyz += d * t;
				#endif
				

				/*#if !_FIXED_LIGHT_DIR
				const half3 height1 = SAMPLE_TEXTURE2D_LOD(_HeightTex, sampler_HeightTex, half2((v.vertex.x - _HeightTexLeft) / _HeightTexLength, (v.vertex.z - _HeightTexBack) / _HeightTexWidth), 0);
				v.vertex.y = get_height(height1, v.vertex.y) + _HeightTexBottom;
				#endif*/
				
				v.vertex.xyz += (height.b * _MaxOffset + _LandHeightOffset) * view;
				
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

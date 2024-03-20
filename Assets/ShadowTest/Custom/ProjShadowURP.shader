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
		[HideInInspector]_ShadowDir("Shadow Dir", Vector) = (1, 1, 1, 1)
		_MainLightDir("Main Light Dir(Invalid if Fixed)", Vector) = (1, 1, 1, 1)
		_StepLength("Step Length", float) = 1.0
		[Toggle(_COMPLEX)] _Complex("Complex", Float) = 0
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
			float4 _ShadowDir;
			float _StepLength;
			CBUFFER_END

			// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
			// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
			// #pragma instancing_options assumeuniformscaling
			UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
			UNITY_INSTANCING_BUFFER_END(Props)
			
 			#pragma multi_compile_local __ _FIXED_LIGHT_DIR _COMPLEX

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float3 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			half3 sample_height(half2 uv_pos)
			{
				return SAMPLE_TEXTURE2D_LOD(_HeightTex, sampler_HeightTex, half2((uv_pos.x - _HeightTexLeft) / _HeightTexLength, (uv_pos.z - _HeightTexBack) / _HeightTexWidth), 0);
			}

			half3 get_height(const half height)
			{
				return height * _HeightTexHigh + _HeightTexBottom;
			}
 
			v2f vert(appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);

				v.vertex = mul(unity_ObjectToWorld, v.vertex);
				//源点
				const float3 orig = v.vertex;
				//const half3 view = normalize(_WorldSpaceCameraPos.xyz - v.vertex);
				//计算高度
				float3 target_pos = v.vertex;
				half3 y_axis;
				half3 height;
#if _FIXED_LIGHT_DIR
					y_axis = normalize(-_ShadowDir.xyz);
					/*convert_pos = mul(convert_pos, half3x3(1, 0, 0,
									normalize_y.x, normalize_y.y, normalize_y.z,
									0, 0, 1));*/
					target_pos = mul(half3x3(1, -y_axis.x / y_axis.y, 0,
					                0, 1 / y_axis.y, 0,
					                0, -y_axis.z / y_axis.y, 1), target_pos);
									//light_dir = normalize(_MainLightDir.xyz);
					float3 uv_pos = mul(half3x3(1, y_axis.x, 0,
					                0, y_axis.y, 0,
					                0, y_axis.z, 1), float3(target_pos.x, _HeightTexBottom, target_pos.z));

					height = sample_height(target_pos.xz);
					target_pos.y = get_height(height.r);

					v.vertex.xyz = mul(half3x3(1, y_axis.x, 0,
					                0, y_axis.y, 0,
					                0, y_axis.z, 1), target_pos);
#else
					y_axis = normalize(-_MainLightDir.xyz);

					height = sample_height(target_pos.xz);
				
	#if _COMPLEX
					while (target_pos.y - height.y > _StepLength)
					{
						target_pos.xyz -= y_axis * _StepLength;
						height = sample_height(v.vertex.xz);
					}
					target_pos.y = get_height(height.r);
					v.vertex.xyz = target_pos;
	#else

					//面上的点
					target_pos.y = get_height(height.r);
					const float3 p = target_pos;
					//面的法线
					const float3 n = float3(0, 1, 0);
					//光的方向
					const float t = dot(orig - p, n) / dot(y_axis, n);
					v.vertex.xyz -= y_axis * t;

					height = sample_height(v.vertex.xz);
					v.vertex.xyz -= y_axis * ((v.vertex.y - (height.r * _HeightTexHigh + _HeightTexBottom)) / 2);
	#endif
#endif
				
				v.vertex.xyz += (height.g * _MaxOffset + _LandHeightOffset) * y_axis/*view*/;

				// 是否需要显示阴影
				o.uv.z = step(0, dot(y_axis, orig - v.vertex)) * height.b;
				//o.uv.z = 1;
				
				o.vertex = mul(unity_MatrixVP, v.vertex);
				o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex);

				return o;
			}
 
			float4 frag(v2f i) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				/*float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
				clip(tex.a - 0.5);*/
				
				return half4(0, 0, 0, i.uv.z * _Alpha);
			}
			
			ENDHLSL
		}
	}

	Fallback "Universal Render Pipeline/Simple Lit"
}

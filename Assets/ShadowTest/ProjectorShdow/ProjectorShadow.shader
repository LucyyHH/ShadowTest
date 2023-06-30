Shader "BokeGame/ProjectorShadow"
{
    Properties
    {
                [NoScaleOffset] _ShadowTex ("Cookie", 2D) = "gray" {}
        [NoScaleOffset] _MaskTex ("MaskTex", 2D) = "white" {}
        _Intensity("Intensity", range(0,1)) = 0.5
    }
    SubShader
    {
        Tags
        {
            "Queue"="AlphaTest+1"
        }

        Pass
        {
            ZWrite off
            Blend DstColor Zero
            offset -1,-1


            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local FSR_PROJECTOR_FOR_LWRP
            #include "Assets/ShadowTest/ProjectorShdow/ProjectorForLWRP/Shaders/P4LWRP.cginc"
            
            sampler2D _MaskTex;  
            float _Intensity;
          
            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 uvShadow : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                fsrTransformVertex(v.vertex, o.pos, o.uvShadow);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float shadow = tex2Dproj(_ShadowTex, UNITY_PROJ_COORD(i.uvShadow)).r;
                float maskColor = tex2Dproj(_MaskTex, UNITY_PROJ_COORD(i.uvShadow)).r;
            	return  float4(1,1,1,1) * (1 - _Intensity * (maskColor * shadow));
            }
            ENDHLSL
        }
    }
}
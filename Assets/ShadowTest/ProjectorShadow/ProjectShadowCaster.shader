Shader "BokeGame/ProjectShadowCaster"
{
    Properties
    {
    }
    SubShader
    {
        Tags { 
            "RenderType"="Opaque" 
            "Queue"="Geometry" 
            "UseProjectorShadow"="True"
        }
  
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                //写死的地面高度
                float height = -91;
                //入射光向量
                fixed3 lightDir = -normalize(_WorldSpaceLightPos0.xyz);
                //顶点的世界坐标位置
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                //该顶点在地面上的位置
                float3 floorPos = float3(worldPos.x,height, worldPos.z);
                //世界空间的-Y向量
                float3 worldY = fixed3(0,-1,0);
                //求出顶点到地面之间的向量在-Y向量的投影
                float a = dot((floorPos - worldPos),worldY);
                //求出灯光在-Y向量的投影
                float b = dot(lightDir,worldY);
                //相乘得出比例，对灯光向量进行缩放后转换到模型空间就得出顶点偏移量
               v.vertex += mul(unity_WorldToObject, lightDir *  a/b * 3 );
                //顶点转到裁剪空间
                o.vertex = UnityObjectToClipPos(v.vertex);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(1,1,1,1);
            }
            ENDCG
        }
    }
}

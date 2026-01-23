Shader "Custom/InstancedSprite" 
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}

	SubShader {
		Tags { "RenderType" = "Transparent"
				"Queue"="Transparent" }
		Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

		
		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing

			#include "UnityCG.cginc"
			#include "UnityShaderVariables.cginc"

			struct appdata_t {
				float4 vertex   : POSITION;
				fixed4 color    : COLOR;
				float2 uv: TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f {
				float4 vertex   : SV_POSITION;
				fixed4 color    : COLOR;
				float2 uv: TEXCOORD0;
				UNITY_VERTEX_OUTPUT_STEREO
			}; 

			fixed4 _Colors[1023];   // Max instanced batch size.
			sampler2D _MainTex;
			float4x4 _ParentMatrix;

			v2f vert(appdata_t i, uint instanceID: SV_InstanceID) 
			{
				v2f o;
				// Allow instancing
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				//float4x4 vpMatrix = unity_StereoMatrixVP[unity_StereoEyeIndex];
				o.vertex = mul(UNITY_MATRIX_VP, mul(mul(_ParentMatrix,unity_ObjectToWorld), i.vertex));;
				o.color = float4(1, 1, 1, 1);
				o.uv = i.uv;
				// If instancing on assign per-instance color.
				#ifdef UNITY_INSTANCING_ENABLED
					o.color = _Colors[instanceID];
				#endif

				return o;
			}

			fixed4 frag(v2f i) : SV_Target 
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

				fixed4 textureColor = tex2D(_MainTex, i.uv);
				fixed3 textureColorRGB = textureColor.rgb;
				fixed3 tintedTexture = i.color.rgb * textureColor.rgb;

				return float4(i.color.rgb, 1) * textureColor.a;
			}

			ENDCG
		}
	}
}
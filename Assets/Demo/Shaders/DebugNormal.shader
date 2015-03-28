Shader "Debug/Draw Normals" {
	Properties {
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM
		#pragma surface surf NoLight

		half4 LightingNoLight (SurfaceOutput s, half3 lightDir, half atten) {
			  half4 c;
			  c.rgb = s.Albedo;
			  c.a = s.Alpha;
			  return c;
		}

		struct Input {
			float4 color : COLOR;
		};

		void surf (Input IN, inout SurfaceOutput o) {
			o.Albedo = o.Normal * 0.5 + 0.5;
		}
		ENDCG
	} 
	FallBack Off
}

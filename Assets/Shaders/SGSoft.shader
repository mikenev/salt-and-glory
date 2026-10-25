// Salt and Glory: soft round particle (smoke, splashes, muzzle flash, wake foam). Procedural disc, alpha blended,
// colour from the particle's vertex colour, so no texture is needed.
Shader "SaltGlory/SoftParticle" {
  Properties { _Softness ("Edge softness", Range(0.05, 1)) = 0.6 }
  SubShader {
    Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
    Pass {
      Tags { "LightMode"="UniversalForward" }
      Blend SrcAlpha OneMinusSrcAlpha
      ZWrite Off
      Cull Off
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };
      struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };
      CBUFFER_START(UnityPerMaterial) float _Softness; CBUFFER_END
      V vert (A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv; o.col = i.col; return o; }
      half4 frag (V i) : SV_Target {
        float d = length(i.uv - 0.5) * 2;
        float a = saturate((1 - d) / _Softness);
        return half4(i.col.rgb, i.col.a * a);
      }
      ENDHLSL
    }
  }
}

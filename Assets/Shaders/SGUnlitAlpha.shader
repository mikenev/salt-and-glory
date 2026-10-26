// Salt and Glory: unlit alpha-blended texture (the chain-shot sprite flying over the sea). Colour = texture * _Tint.
Shader "SaltGlory/UnlitAlpha" {
  Properties {
    _MainTex ("Texture", 2D) = "white" {}
    _Tint ("Tint", Color) = (1,1,1,1)
  }
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
      struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
      struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
      TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
      CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST; float4 _Tint;
      CBUFFER_END
      V vert (A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = TRANSFORM_TEX(i.uv, _MainTex); return o; }
      half4 frag (V i) : SV_Target { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Tint; }
      ENDHLSL
    }
  }
}

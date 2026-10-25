// Salt and Glory: painterly sea. Samples a water-only patch (_Rect, in UV) of the ship-battle style frame, mirror-tiled in
// world XZ, two layers at different scales and drift directions, with a gentle UV wobble so the brushwork seems to move.
Shader "SaltGlory/Sea" {
  Properties {
    _MainTex ("Style frame", 2D) = "white" {}
    _Rect ("Water patch (u0, v0, du, dv)", Vector) = (0, 0.764, 0.39, 0.236)
    _Tile ("Metres per tile (layer A, layer B)", Vector) = (38, 61, 0, 0)
    _Tint ("Tint", Color) = (1.15, 1.2, 1.2, 1)
  }
  SubShader {
    Tags { "RenderType"="Opaque" "Queue"="Geometry-10" "RenderPipeline"="UniversalPipeline" }
    Pass {
      Tags { "LightMode"="UniversalForward" }
      ZWrite On
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      struct A { float4 pos : POSITION; };
      struct V { float4 pos : SV_POSITION; float2 w : TEXCOORD0; };
      TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
      CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST; float4 _Rect; float4 _Tile; float4 _Tint;
      CBUFFER_END
      V vert (A i) { V o; float3 w = TransformObjectToWorld(i.pos.xyz); o.pos = TransformWorldToHClip(w); o.w = w.xz; return o; }
      float2 mirror(float2 t) { float2 f = frac(t * 0.5) * 2; return 1 - abs(1 - f); }   // 0..1..0 triangle wave
      half3 layer(float2 p) { float2 m = mirror(p); return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, _Rect.xy + m * _Rect.zw).rgb; }
      half4 frag (V i) : SV_Target {
        float t = _Time.y;
        float2 p = i.w;
        float2 wob = float2(sin(p.y * 0.11 + t * 0.7), cos(p.x * 0.09 + t * 0.6)) * 0.9;
        float aspect = _Rect.z / _Rect.w * (1280.0 / 720.0);
        float2 a = (p + wob + float2(t * 0.35, t * 0.12)) / float2(_Tile.x * aspect, _Tile.x);
        float2 q = float2(p.x * 0.8 - p.y * 0.6, p.x * 0.6 + p.y * 0.8);            // layer B rotated ~37 degrees
        float2 b = (q - wob + float2(-t * 0.2, t * 0.3)) / float2(_Tile.y * aspect, _Tile.y);
        half3 c = lerp(layer(a), layer(b), 0.45);
        return half4(c * _Tint.rgb, 1);
      }
      ENDHLSL
    }
  }
}

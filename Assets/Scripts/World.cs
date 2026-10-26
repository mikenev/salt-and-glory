using System.Collections.Generic;
using UnityEngine;
namespace SaltGlory {
  public enum Nation { England, Spain, France, Dutch }

  /// <summary>M2 world: the 1660 Caribbean (real ports at real coordinates), nations at war, trade goods, and a land mask
  /// rasterised from rough coastline polygons. The mask drives land in the sea shader, ship-vs-land blocking and the
  /// placeholder parchment chart (used until Art Dev's map lands in Assets/Art/Map).</summary>
  public static class World {
    // world frame = Art Dev's chart (SG_Map_Caribbean.png): equirectangular, lon -98..-58, lat 6.83..30.5, x scaled by cos(18.75)
    public const float Lon0 = -98f, Lon1 = -58f, Lat0 = 6.8267f, Lat1 = 30.5f, K = 80f, KX = K * 0.94693f;   // world units per degree
    public const float XMin = 0f, ZMin = 0f, W = (Lon1 - Lon0) * KX, H = (Lat1 - Lat0) * K;
    public static Vector3 Geo(float lat, float lon) => new((lon - Lon0) * KX, 0f, (lat - Lat0) * K);
    public static Vector3 FromUV(Vector2 uv) => new(XMin + uv.x * W, 0f, ZMin + uv.y * H);
    public static Vector2 ToUV(Vector3 p) => new((p.x - XMin) / W, (p.z - ZMin) / H);

    public static readonly string[] NationNames = { "England", "Spain", "France", "Dutch Republic" };
    public static readonly string[] Adj = { "English", "Spanish", "French", "Dutch" };
    public static readonly Color[] NationColor = { new(0.75f, 0.18f, 0.16f), new(0.92f, 0.72f, 0.18f), new(0.25f, 0.4f, 0.85f), new(0.95f, 0.5f, 0.12f) };
    /// <summary>Who is at war with whom around 1660-65: Spain against the other three, and England against the Dutch.</summary>
    public static bool AtWar(Nation a, Nation b) => a != b && (a == Nation.Spain || b == Nation.Spain || (a == Nation.England && b == Nation.Dutch) || (a == Nation.Dutch && b == Nation.England));

    public class Port { public string name; public Nation nation; public Vector3 pos; public float[] price; }
    public static readonly string[] Goods = { "Sugar", "Tobacco", "Rum", "Cloth", "Spices" };   // matches the UI cargo icons
    static readonly float[] BasePrice = { 40f, 55f, 30f, 70f, 90f };
    public static readonly List<Port> Ports = new();
    static void P(string n, Nation nat, float lat, float lon, params float[] mul){
      var pr = new float[Goods.Length]; for (int i = 0; i < pr.Length; i++) pr[i] = Mathf.Round(BasePrice[i] * mul[i]);
      Ports.Add(new Port{ name = n, nation = nat, pos = Geo(lat, lon), price = pr });
    }
    /// <summary>Move the ports onto the chart's own port positions (SG_Map_Ports.json uv, origin bottom-left).</summary>
    public static void ApplyPortsJson(string json){
      if (string.IsNullOrEmpty(json)) return;
      try {
        var f = JsonUtility.FromJson<PortFile>(json); int n = 0;
        foreach (var e in f.ports){ var p = Ports.Find(q => q.name == e.name); if (p != null && e.uv != null && e.uv.Length == 2){ p.pos = FromUV(new Vector2(e.uv[0], e.uv[1])); n++; } }
        Debug.Log($"[SG] ports from chart json: {n}");
      } catch (System.Exception ex){ Debug.LogWarning("[SG] ports json: " + ex.Message); }
    }
    [System.Serializable] class PortFile { public PortEntry[] ports; }
    [System.Serializable] class PortEntry { public string name, nation; public float lat, lon; public float[] uv; }
    static World(){
      // price multipliers: < 1 = produced here (cheap), > 1 = wanted here (dear)
      P("Port Royal", Nation.England, 17.94f, -76.84f, 0.8f, 1.1f, 0.7f, 1.2f, 1.2f);
      P("Bridgetown", Nation.England, 13.10f, -59.62f, 0.6f, 1.2f, 0.8f, 1.3f, 1.4f);
      P("Havana", Nation.Spain, 23.13f, -82.38f, 0.9f, 0.6f, 1.2f, 1.3f, 0.9f);
      P("Cartagena", Nation.Spain, 10.39f, -75.51f, 1.3f, 1.0f, 1.3f, 1.4f, 0.8f);
      P("Santo Domingo", Nation.Spain, 18.47f, -69.89f, 0.8f, 0.9f, 1.1f, 1.3f, 0.7f);
      P("San Juan", Nation.Spain, 18.47f, -66.11f, 0.8f, 1.1f, 1.2f, 1.2f, 0.9f);
      P("Portobelo", Nation.Spain, 9.55f, -79.65f, 1.4f, 1.3f, 1.4f, 1.5f, 0.6f);
      P("Tortuga", Nation.France, 20.05f, -72.78f, 1.3f, 0.8f, 1.3f, 1.2f, 0.8f);
      P("Saint-Pierre", Nation.France, 14.74f, -61.18f, 0.7f, 1.2f, 0.8f, 1.1f, 1.2f);
      P("Willemstad", Nation.Dutch, 12.11f, -68.93f, 1.3f, 1.2f, 1.1f, 0.7f, 1.3f);
      P("Sint Eustatius", Nation.Dutch, 17.49f, -62.98f, 1.2f, 1.3f, 1.0f, 0.75f, 1.2f);
      P("St. Kitts", Nation.England, 17.32f, -62.80f, 0.7f, 0.75f, 1.0f, 1.25f, 1.3f);   // English Old Road; the French held both ends (Basseterre)
    }

    // ---------- land ----------
    static readonly float[][] Coasts = {   // (lon, lat) pairs, rough 17th-century-chart outlines
      new[]{ -84.95f,21.85f, -84.2f,22.5f, -83.0f,23.0f, -81.5f,23.15f, -80.3f,23.0f, -78.0f,22.3f, -77.2f,21.6f, -75.7f,21.1f, -74.2f,20.3f, -74.4f,20.0f, -75.5f,19.9f, -77.7f,19.85f, -77.2f,20.6f, -78.5f,21.5f, -80.5f,21.8f, -81.8f,22.2f, -82.8f,22.4f, -84.0f,21.95f },   // Cuba
      new[]{ -74.45f,18.4f, -73.3f,19.7f, -72.8f,19.92f, -71.7f,19.9f, -70.0f,19.65f, -69.0f,19.0f, -68.35f,18.6f, -68.7f,18.2f, -69.9f,18.43f, -71.0f,18.1f, -71.4f,17.6f, -72.5f,18.2f, -73.5f,18.2f },   // Hispaniola
      new[]{ -78.35f,18.3f, -77.6f,18.5f, -76.9f,18.4f, -76.2f,18.0f, -76.6f,17.85f, -77.2f,17.75f, -77.8f,17.85f },   // Jamaica
      new[]{ -67.25f,18.5f, -65.6f,18.42f, -65.6f,18.0f, -67.2f,17.95f },   // Puerto Rico
      new[]{ -90f,21.5f, -87.0f,21.5f, -86.8f,20.8f, -87.5f,19.5f, -87.8f,18.3f, -88.3f,16.0f, -86.0f,15.9f, -84.0f,15.6f, -83.2f,15.0f, -83.5f,13.0f, -83.8f,11.5f, -83.0f,10.0f, -81.8f,9.0f, -80.5f,9.1f, -79.6f,9.5f, -78.5f,9.4f, -77.3f,8.6f, -76.8f,8.0f, -76.2f,9.3f, -75.55f,10.35f, -74.8f,11.0f, -73.5f,11.3f, -72.2f,11.9f, -71.6f,11.0f, -71.3f,11.8f, -70.2f,11.6f, -69.8f,11.5f, -68.4f,10.6f, -66.0f,10.6f, -64.5f,10.2f, -63.0f,10.7f, -62.0f,10.6f, -61.0f,10.0f, -59.5f,8.5f, -58f,7f, -58f,4f, -90f,4f },   // the Main
      new[]{ -82.9f,27f, -82.6f,26f, -81.7f,25.15f, -80.4f,25.2f, -80.0f,26.5f, -80.0f,27f },   // Florida
    };
    static readonly float[] Isles = {   // lon, lat, rx, ry (degrees)
      -72.75f,20.08f,0.2f,0.04f,   -61.05f,14.65f,0.1f,0.2f,   -59.55f,13.18f,0.08f,0.12f,   -62.96f,17.49f,0.04f,0.05f,   -62.75f,17.3f,0.1f,0.06f,
      -61.5f,16.2f,0.25f,0.15f,   -61.35f,15.42f,0.08f,0.18f,   -60.97f,13.9f,0.07f,0.13f,   -61.2f,13.25f,0.06f,0.1f,   -61.7f,12.1f,0.06f,0.09f,
      -61.3f,10.45f,0.45f,0.3f,   -61.8f,17.08f,0.1f,0.07f,   -70.0f,12.5f,0.1f,0.05f,   -68.95f,12.2f,0.28f,0.07f,   -68.3f,12.15f,0.13f,0.08f,
      -78.0f,24.4f,0.3f,0.5f,   -77.3f,25.1f,0.25f,0.1f,   -73.4f,21.05f,0.4f,0.15f,   -75.1f,23.2f,0.15f,0.5f,   -82.8f,21.65f,0.4f,0.15f,
      -64.6f,18.4f,0.2f,0.06f,   -86.9f,20.4f,0.15f,0.25f,
    };
    public const int MW = 1280, MH = 800;   // half the chart's resolution, ~2.4 m per mask pixel
    public static float[] mask;   // 0 sea .. 1 land, blurred so the shore is a soft band
    public static Texture2D MaskTex;

    /// <summary>Land mask: from the chart (LandMask.bytes, made at build time from SG_Map_Caribbean.png) or, if that is missing,
    /// from the rough coastline polygons below.</summary>
    public static void BuildMask(byte[] fromChart = null){
      if (mask != null) return;
      var m = new float[MW * MH];
      if (fromChart != null && fromChart.Length == MW * MH){ for (int i = 0; i < m.Length; i++) m[i] = fromChart[i] / 255f; Debug.Log("[SG] land mask from chart"); }
      else RasterPolygons(m);
      Finish(m);
    }
    static void RasterPolygons(float[] m){
      Debug.Log("[SG] land mask from fallback polygons"); var xs = new List<float>();
      foreach (var poly in Coasts){
        int n = poly.Length / 2;
        for (int y = 0; y < MH; y++){
          float lat = Lat0 + (y + 0.5f) / MH * (Lat1 - Lat0); xs.Clear();
          for (int i = 0; i < n; i++){
            float x1 = poly[i * 2], y1 = poly[i * 2 + 1], x2 = poly[(i + 1) % n * 2], y2 = poly[(i + 1) % n * 2 + 1];
            if ((y1 <= lat) != (y2 <= lat)) xs.Add(x1 + (lat - y1) / (y2 - y1) * (x2 - x1));
          }
          xs.Sort();
          for (int k = 0; k + 1 < xs.Count; k += 2){
            int a = Mathf.Max(0, Mathf.CeilToInt((xs[k] - Lon0) / (Lon1 - Lon0) * MW - 0.5f)), b = Mathf.Min(MW - 1, Mathf.FloorToInt((xs[k + 1] - Lon0) / (Lon1 - Lon0) * MW - 0.5f));
            for (int x = a; x <= b; x++) m[y * MW + x] = 1f;
          }
        }
      }
      for (int i = 0; i < Isles.Length; i += 4){
        float cx = (Isles[i] - Lon0) / (Lon1 - Lon0) * MW, cy = (Isles[i + 1] - Lat0) / (Lat1 - Lat0) * MH;
        float rx = Isles[i + 2] / (Lon1 - Lon0) * MW + 2.5f, ry = Isles[i + 3] / (Lat1 - Lat0) * MH + 2.5f;   // + a minimum so small isles survive the blur
        for (int y = Mathf.Max(0, (int)(cy - ry)); y <= Mathf.Min(MH - 1, (int)(cy + ry) + 1); y++)
          for (int x = Mathf.Max(0, (int)(cx - rx)); x <= Mathf.Min(MW - 1, (int)(cx + rx) + 1); x++){
            float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry; if (dx * dx + dy * dy <= 1f) m[y * MW + x] = 1f;
          }
      }
    }
    static void Finish(float[] m){
      // two separable box-blur passes (radius 2 px, about 4 m each) give a soft shore band
      var tmp = new float[m.Length];
      for (int pass = 0; pass < 2; pass++){
        for (int y = 0; y < MH; y++) for (int x = 0; x < MW; x++){ float s = 0; for (int d = -2; d <= 2; d++) s += m[y * MW + Mathf.Clamp(x + d, 0, MW - 1)]; tmp[y * MW + x] = s / 5f; }
        for (int y = 0; y < MH; y++) for (int x = 0; x < MW; x++){ float s = 0; for (int d = -2; d <= 2; d++) s += tmp[Mathf.Clamp(y + d, 0, MH - 1) * MW + x]; m[y * MW + x] = s / 5f; }
      }
      mask = m;
      MaskTex = new Texture2D(MW, MH, TextureFormat.R8, false){ wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
      var px = new byte[MW * MH]; for (int i = 0; i < px.Length; i++) px[i] = (byte)(Mathf.Clamp01(m[i]) * 255f);
      MaskTex.SetPixelData(px, 0); MaskTex.Apply(false, true);
    }
    /// <summary>Land amount at a world point (bilinear), 1 outside the chart so ships stay on it.</summary>
    public static float Land(Vector3 p){
      var uv = ToUV(p); if (uv.x <= 0.002f || uv.y <= 0.002f || uv.x >= 0.998f || uv.y >= 0.998f) return 1f;
      float fx = uv.x * MW - 0.5f, fy = uv.y * MH - 0.5f; int x = Mathf.Clamp((int)fx, 0, MW - 2), y = Mathf.Clamp((int)fy, 0, MH - 2); fx -= x; fy -= y;
      float a = Mathf.Lerp(mask[y * MW + x], mask[y * MW + x + 1], fx), b = Mathf.Lerp(mask[(y + 1) * MW + x], mask[(y + 1) * MW + x + 1], fx);
      return Mathf.Lerp(a, b, fy);
    }
    public static bool IsSea(Vector3 p) => Land(p) < 0.25f;
    /// <summary>Nearest open-water point to p (spiral search), for spawning ships and putting the player outside a harbour.</summary>
    public static Vector3 WaterNear(Vector3 p, float min = 0f){
      for (float r = min; r < 200f; r += 4f) for (int i = 0; i < 24; i++){
        var q = p + Quaternion.Euler(0f, i * 15f, 0f) * Vector3.forward * r; if (Land(q) < 0.08f) return q;
      }
      return p;
    }

    /// <summary>Placeholder parchment chart drawn from the land mask (until Art Dev's map lands).</summary>
    public static Texture2D MakeChart(){
      BuildMask(); const int w = MW / 2, h = MH / 2; var t = new Texture2D(w, h, TextureFormat.RGBA32, false){ wrapMode = TextureWrapMode.Clamp };
      var px = new Color32[w * h]; var sea = new Color(0.93f, 0.86f, 0.68f); var land = new Color(0.78f, 0.66f, 0.45f); var ink = new Color(0.35f, 0.24f, 0.14f);
      for (int y = 0; y < h; y++) for (int x = 0; x < w; x++){
        float v = mask[(y * 2) * MW + x * 2], n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.08f + Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.03f;
        Color c = v > 0.5f ? land : sea; if (v > 0.3f && v < 0.62f) c = Color.Lerp(c, ink, 0.75f); else if (v > 0.12f && v <= 0.3f) c = Color.Lerp(sea, new Color(0.6f, 0.72f, 0.68f), 0.5f);
        float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y)) / 24f;   // darkened, aged edges
        c *= 1f - n - (1f - Mathf.Clamp01(edge)) * 0.25f; c.a = 1f; px[y * w + x] = c;
      }
      t.SetPixels32(px); t.Apply(); return t;
    }
  }
}

using UnityEngine;
using UnityEngine.InputSystem;
namespace SaltGlory {
  /// <summary>M4: Art Dev's painted HUD / menu skin (Assets/Art/UI/HUD, spec SG_UI_Skin.json), the title backdrop and logo, and the
  /// treasure fragment (the real chart around the dig spot, seen through the torn window, with the X). IMGUI: the sea HUD is drawn in
  /// 1280x720 reference pixels under a scale matrix, and 9-slices that need a smaller scale than their art are drawn in a nested
  /// matrix so the borders keep their proportions. Anything missing falls back to the M3 placeholder shapes.</summary>
  public partial class Game {
    [Header("M4 skin (null = the placeholder shapes)")]
    public Texture2D titleLogoTex, buttonDownTex, barFrameTex, hullFillTex, crewFillTex, gaugeFrameTex, gaugeSailTex, gaugeNeedleTex, readoutTex, shotBackTex, shotFrameTex, shotChainTex, fragTex, treasureXTex;
    bool Skin => barFrameTex && gaugeFrameTex && hullFillTex;
    GUIStyle barSt, readSt, hudPanelSt, hudBtnSt;
    const float BtnArtH = 96f;   // button art height: drawn smaller, it is scaled so its 36 px border keeps its look

    void SkinStyles(){
      barSt ??= new GUIStyle{ border = new RectOffset(28, 28, 14, 14) }; barSt.normal.background = barFrameTex;
      readSt ??= new GUIStyle{ border = new RectOffset(92, 24, 24, 24) }; readSt.normal.background = readoutTex ? readoutTex : barFrameTex;
      hudPanelSt ??= new GUIStyle{ border = new RectOffset(64, 64, 64, 64) }; hudPanelSt.normal.background = panelTex ? panelTex : white;
      hudBtnSt ??= new GUIStyle{ border = new RectOffset(36, 36, 36, 36) }; hudBtnSt.normal.background = buttonTex ? buttonTex : white;
    }
    /// <summary>Draw a 9-slice at rect r (current GUI space) as if the art were k times its size (borders scaled by k).</summary>
    void Slice9(Rect r, GUIStyle st, float k, Texture2D tex = null){
      if (Event.current.type != EventType.Repaint) return;
      var m = GUI.matrix; var bg = st.normal.background; if (tex) st.normal.background = tex;
      GUI.matrix = m * Matrix4x4.TRS(new Vector3(r.x, r.y, 0f), Quaternion.identity, new Vector3(k, k, 1f));
      st.Draw(new Rect(0f, 0f, r.width / k, r.height / k), GUIContent.none, false, false, false, false);
      GUI.matrix = m; st.normal.background = bg;
    }
    /// <summary>Bar frame + Art Dev's coloured fill cut off square from the left (Image Filled, Horizontal, origin Left; no tint).</summary>
    void SkinBar(Rect r, Texture2D fill, float v, string label){
      float k = r.height / 56f; Slice9(r, barSt, k);
      var win = new Rect(r.x + 16f * k, r.y + 14f * k, r.width - 32f * k, r.height - 28f * k); v = Mathf.Clamp01(v);
      if (fill && v > 0f) GUI.DrawTextureWithTexCoords(new Rect(win.x, win.y, win.width * v, win.height), fill, new Rect(0f, 0f, v, 1f));
      var st = new GUIStyle(small){ alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(Mathf.Max(11f, win.height * 0.8f)) };
      var lr = new Rect(win.x + 8f, win.y - 4f, win.width - 12f, win.height + 8f);
      Label(new Rect(lr.x + 1f, lr.y + 1f, lr.width, lr.height), label, st, new Color(0f, 0f, 0f, 0.8f)); Label(lr, label, st, Parchment);
    }
    void Readout(Rect r, Texture2D icon, string text){
      float k = r.height / 80f; Slice9(r, readSt, k);
      if (icon) GUI.DrawTexture(new Rect(r.x + 14f * k, r.y + 14f * k, 52f * k, 52f * k), icon, ScaleMode.ScaleToFit);
      Label(new Rect(r.x + 94f * k, r.y + 16f * k, r.width - 110f * k, 48f * k), text, new GUIStyle(inkSt ?? small){ alignment = TextAnchor.MiddleLeft, wordWrap = false, fontSize = Mathf.RoundToInt(20f * k) }, Ink);
    }
    void HudButton(Rect r, string label, bool down = false){
      Slice9(r, hudBtnSt, r.height / BtnArtH, down && buttonDownTex ? buttonDownTex : null);
      var lr = down ? new Rect(r.x, r.y + 2f, r.width, r.height) : r;
      Label(lr, label, new GUIStyle(txt){ alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = Mathf.RoundToInt(Mathf.Min(18f, r.height * 0.32f)) }, Ink);
    }

    // ---------- the wind / sail gauge (top right): needle = where the wind blows TO, relative to the bow (dial top) ----------
    Texture2D sailCut; Color32[] sailSrc; float sailCutAt = -1f;
    /// <summary>Radial 360 fill from the bottom, clockwise (as Image Filled would), baked into a copy when the sail setting moves.</summary>
    Texture2D SailFill(float f){
      if (!gaugeSailTex) return null;
      if (sailSrc == null){ if (!gaugeSailTex.isReadable) return gaugeSailTex; sailSrc = gaugeSailTex.GetPixels32(); sailCut = new Texture2D(gaugeSailTex.width, gaugeSailTex.height, TextureFormat.RGBA32, false){ wrapMode = TextureWrapMode.Clamp }; }
      f = Mathf.Round(f * 50f) / 50f; if (Mathf.Approximately(f, sailCutAt)) return sailCut;
      int n = sailCut.width, h = sailCut.height; var px = new Color32[sailSrc.Length]; float lim = f * 360f;
      for (int y = 0; y < h; y++) for (int x = 0; x < n; x++){
        float a = Mathf.Atan2(-(x - n / 2f + 0.5f), -(y - h / 2f + 0.5f)) * Mathf.Rad2Deg; if (a < 0f) a += 360f;
        int i = y * n + x; px[i] = a <= lim ? sailSrc[i] : new Color32(0, 0, 0, 0);
      }
      sailCut.SetPixels32(px); sailCut.Apply(); sailCutAt = f; return sailCut;
    }
    float WindBearing => Mathf.DeltaAngle(player.heading, windFrom + 180f);   // where the wind blows to, off the bow (+ starboard)

    /// <summary>The skinned sea HUD (status bars, readouts, gauge, met ship, top buttons, fire buttons, shot slots).</summary>
    void SkinHud(float s, float W, float H){
      Styles(s); var m0 = GUI.matrix; GUI.matrix = m0 * Matrix4x4.Scale(new Vector3(s, s, 1f)); float RW = W / s, RH = H / s;
      SkinBar(new Rect(24, 24, 320, 40), hullFillTex, player.hull / 100f, $"Hull {player.hull:F0}");
      SkinBar(new Rect(24, 70, 320, 40), crewFillTex, player.crew / Mathf.Max(1f, player.Complement), $"Crew {player.crew:F0}/{player.Complement}");
      Readout(new Rect(24, 118, 240, 60), coinTex, $"{career.gold} gold");
      int topG = 0; for (int g = 1; g < 5; g++) if (career.cargo[g] > career.cargo[topG]) topG = g;
      Readout(new Rect(24, 184, 240, 60), cargoTex[topG], $"Hold {career.Hold}/{career.HoldMax}   Fame {career.fame}");
      // gauge
      var gr = new Rect(RW - 24 - 180, 24, 180, 180);
      GUI.DrawTexture(gr, gaugeFrameTex); var sf = SailFill(player.sail); if (sf) GUI.DrawTexture(gr, sf);
      if (gaugeNeedleTex){
        float k = 180f / 256f; var mm = GUI.matrix; GUIUtility.RotateAroundPivot(WindBearing, new Vector2(gr.center.x * s, gr.center.y * s));
        GUI.DrawTexture(new Rect(gr.center.x - 20f * k, gr.center.y - 92f * k, 40f * k, 184f * k), gaugeNeedleTex); GUI.matrix = mm;
      }
      float eff = Ship.WindFactor(player.OffWind(windFrom));
      var cap = new GUIStyle(small){ alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold, fontSize = 15 };
      string l1 = $"{Ship.PointOfSail(player.OffWind(windFrom))} {eff * 100f:F0}%   {player.speed * 1.2f:F1} kn", l2 = $"Sails set {player.sail * 100f:F0}%   canvas {player.sails:F0}/100";
      Label(new Rect(RW - 325, 207, 300, 20), l1, cap, new Color(0f, 0f, 0f, 0.8f)); Label(new Rect(RW - 326, 206, 300, 20), l1, cap, Parchment);
      Label(new Rect(RW - 325, 227, 300, 20), l2, cap, new Color(0f, 0f, 0f, 0.8f)); Label(new Rect(RW - 326, 226, 300, 20), l2, cap, Parchment);
      // the met ship
      if (encActive && !enemy.gone){
        var er = new Rect(24, 256, 300, 124); Slice9(er, hudPanelSt, 0.5f);
        float d = Vector3.Distance(player.Pos, enemy.Pos);
        Label(new Rect(er.x + 24, er.y + 18, er.width - 48, 24), $"{enemy.label}  {d:F0} m", new GUIStyle(inkSt){ fontStyle = FontStyle.Bold, wordWrap = false, fontSize = 15 }, encHostile ? new Color(0.55f, 0.08f, 0.05f) : Ink);
        SkinBar(new Rect(er.x + 20, er.y + 44, er.width - 40, 30), hullFillTex, enemy.hull / 100f, $"Hull {enemy.hull:F0}");
        SkinBar(new Rect(er.x + 20, er.y + 76, er.width - 40, 30), crewFillTex, enemy.crew / Mathf.Max(1f, enemy.Complement), $"Crew {enemy.crew:F0}");
      }
      // top buttons, fire buttons, shot slots (screen rects so touch hit-tests match)
      GUI.matrix = m0;
      bool md = Mouse.current != null && Mouse.current.leftButton.isPressed; var mp = Event.current.mousePosition;
      HudButton(RPause, "II", md && RPause.Contains(mp)); HudButton(RMap, "MAP", md && RMap.Contains(mp)); HudButton(RLog, "LOG", md && RLog.Contains(mp));
      SkinFire(RPort, "PORT\n(Q)", player.portReady); SkinFire(RStar, "STARBOARD\n(E)", player.starReady);
      var rs = RShot; float sz = rs.height, gap = 12f * s;
      for (int i = 0; i < 3; i++){
        var r = new Rect(rs.x + i * (sz + gap), rs.y, sz, sz); bool on = (int)shot == i; var o = GUI.color; GUI.color = new Color(1f, 1f, 1f, on ? 1f : 0.5f);
        if (shotBackTex) GUI.DrawTexture(r, shotBackTex); else GUI.DrawTexture(r, disc);
        var ic = new Rect(r.x + r.width * 0.3f, r.y + r.height * 0.3f, r.width * 0.4f, r.height * 0.4f);
        if (i == 1 && shotChainTex) GUI.DrawTexture(new Rect(r.x + r.width * 0.2f, r.y + r.height * 0.2f, r.width * 0.6f, r.height * 0.6f), shotChainTex, ScaleMode.ScaleToFit);
        else if (i == 2){ GUI.color = new Color(0.12f, 0.1f, 0.09f, GUI.color.a); for (int k = 0; k < 5; k++){ float a = k * 72f * Mathf.Deg2Rad, rr = k == 4 ? 0f : r.width * 0.13f; var c = r.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr; GUI.DrawTexture(new Rect(c.x - r.width * 0.09f, c.y - r.width * 0.09f, r.width * 0.18f, r.width * 0.18f), disc); } }
        else { GUI.color = new Color(0.12f, 0.1f, 0.09f, GUI.color.a); GUI.DrawTexture(ic, disc); }
        GUI.color = new Color(1f, 1f, 1f, on ? 1f : 0.5f); if (shotFrameTex) GUI.DrawTexture(r, shotFrameTex); GUI.color = o;
      }
      var ls = new GUIStyle(small){ alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
      Label(new Rect(rs.x, rs.y - 24 * s, rs.width, 22 * s), $"{shot} shot  (Tab / Y / tap)", ls, Parchment);
      // sail slider (touch)
      var sl = RSlider; Box(sl, Panel); Box(new Rect(sl.center.x - 3 * s, sl.y + 10 * s, 6 * s, sl.height - 20 * s), new Color(1, 1, 1, 0.3f));
      float ky = sl.y + 10 * s + (1f - player.sail) * (sl.height - 20 * s); Slice9(new Rect(sl.x + 4 * s, ky - 12 * s, sl.width - 8 * s, 24 * s), hudBtnSt, 24 * s / BtnArtH);
      Label(new Rect(sl.x - 10 * s, sl.y - 24 * s, sl.width + 20 * s, 22 * s), "SAILS", new GUIStyle(small){ alignment = TextAnchor.MiddleCenter }, Parchment);
    }
    void SkinFire(Rect r, string label, float ready){
      float left = ready - Time.time; HudButton(r, "");
      if (left > 0f){ float f = Mathf.Clamp01(left / player.ReloadTime); Box(new Rect(r.x + 10 * S, r.y + 10 * S + (r.height - 20 * S) * (1f - f), r.width - 20 * S, (r.height - 20 * S) * f), new Color(0.5f, 0.15f, 0.1f, 0.4f)); }
      Label(r, left > 0f ? $"{label}\n{left:F1}s" : label, new GUIStyle(txt){ alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(15 * S) }, left > 0f ? new Color(0.45f, 0.3f, 0.2f) : Ink);
    }

    // ---------- the treasure fragment: Art Dev's torn template with the real chart around the dig spot in its window ----------
    const float FragSpan = 320f;   // metres of sea and shore across the window
    void DrawFragment(Rect r){
      var tex = mapTex; if (!tex){ chartFallback ??= World.MakeChart(); tex = chartFallback; }
      float k = r.width / 512f; var win = new Rect(r.x + 112f * k, r.y + 104f * k, 288f * k, 288f * k);
      var uv = World.ToUV(new Vector3(career.digX, 0f, career.digZ));
      float hu = FragSpan / World.W / 2f, hv = FragSpan / World.H / 2f;
      float u0 = Mathf.Clamp(uv.x - hu, 0f, 1f - 2f * hu), v0 = Mathf.Clamp(uv.y - hv, 0f, 1f - 2f * hv);
      GUI.DrawTextureWithTexCoords(win, tex, new Rect(u0, v0, 2f * hu, 2f * hv));
      if (fragTex) GUI.DrawTexture(r, fragTex);
      float xx = win.x + (uv.x - u0) / (2f * hu) * win.width, yy = win.y + (1f - (uv.y - v0) / (2f * hv)) * win.height, xs = 96f * k;
      if (treasureXTex) GUI.DrawTexture(new Rect(xx - xs / 2f, yy - xs / 2f, xs, xs), treasureXTex);
      else Label(new Rect(xx - xs / 2f, yy - xs / 2f, xs, xs), "X", new GUIStyle(big){ fontSize = Mathf.RoundToInt(xs * 0.6f) }, new Color(0.75f, 0.1f, 0.06f));
    }

    // ---------- the title: Art Dev's backdrop and logo, menu over the calm sea ----------
    void TitleSkinGUI(float W, float H, float uk){
      Background(titleTex, W, H); UseUiFit(W, H, uk);
      if (titleLogoTex){ float lw = 760f, lh = lw * titleLogoTex.height / titleLogoTex.width; GUI.DrawTexture(R(640 - lw / 2f, 190 - lh / 2f, lw, lh), titleLogoTex); }
      else Label(R(290, 150, 700, 80), "Salt and Glory", new GUIStyle(inkBig){ alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(64 * uk) }, Parchment);
      var sub = new GUIStyle(txt){ alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(22 * uk) };
      Label(R(342, 270, 600, 30), "A captain's career in the Caribbean, 1660", sub, new Color(0.1f, 0.06f, 0.02f, 0.85f)); Label(R(340, 268, 600, 30), "A captain's career in the Caribbean, 1660", sub, new Color(1f, 0.96f, 0.86f));
      int latest = Saves.Latest();
      if (!titleLoad){
        if (Btn(R(490, 436, 300, 64), "New career")) NewCareer();
        if (Btn(R(490, 512, 300, 64), "Continue", latest >= 0)) LoadFrom(latest);
        if (Btn(R(490, 588, 300, 64), "Load")){ titleLoad = true; focus = -1; Debug.Log("[SG] title load menu"); }
      } else {
        PanelBox(R(400, 318, 480, 380));
        for (int i = 0; i < Saves.Slots; i++){ var c = Saves.Load(i); if (Btn(R(450, 358 + i * 80, 380, 66), SlotLabel(i, c), c != null)) LoadFrom(i); }
        if (Btn(R(540, 606, 200, 56), "Back")){ titleLoad = false; focus = -1; }
      }
      Box(R(0, 676, 1280, 44), new Color(0.05f, 0.04f, 0.03f, 0.55f));
      Label(R(20, 678, 1240, 40), "Keyboard: arrows / WASD and Enter, Esc back   ·   Gamepad: D-pad / stick and A, B back   ·   Touch or mouse: tap a button",
        new GUIStyle(small){ alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(15 * uk) }, Parchment);
    }
  }
}

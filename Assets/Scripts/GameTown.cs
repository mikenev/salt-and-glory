using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace SaltGlory {
  /// <summary>M2: title menu (New, Continue, Load), docking, town screens (harbour hub, tavern, governor, merchant, shipwright),
  /// save slots and the parchment world map. Towns are point and click (mouse or touch); Esc / gamepad B goes back.
  /// Art is assigned by BuildWeb by file name; anything missing falls back to a plain parchment box or the style frames.</summary>
  public partial class Game {
    [Header("M2 art (null = fallback)")]
    public Texture2D titleTex, mapTex, portMarkerTex, portRingTex, shipMarkerTex, panelTex, buttonTex, buttonHiTex, coinTex, tavernTex, governorTex, merchantTex, shipwrightTex;
    public Texture2D[] harborTex = new Texture2D[4], flagTex = new Texture2D[4], flagIconTex = new Texture2D[4], cargoTex = new Texture2D[5];
    public Vector3[] flagAnchors = new Vector3[5];   // (x, y, scale) of the flag hoist on each harbour (by nation) and [4] the governor's office, in 1280x720 px

    enum Page { Hub, Tavern, Governor, Merchant, Shipwright, Save }
    Page screen = Page.Hub; bool titleLoad; string townMsg = ""; int portIdx; World.Port Here => World.Ports[portIdx];
    readonly List<string> rumours = new(); int rumourAt;
    bool clickPending; Vector2 clickPos; Texture2D chartFallback;

    void TownStart(){ state = State.Title; GameAudio.I?.SetScene(false, false); }

    // ---------- ports ----------
    World.Port NearestPort(Vector3 p){ World.Port best = null; float bd = 1e9f; foreach (var q in World.Ports){ float d = Vector3.Distance(p, q.pos); if (d < bd){ bd = d; best = q; } } return best; }
    bool NearPort(out World.Port p, float range = DockRange){ p = NearestPort(player.Pos); if (Vector3.Distance(player.Pos, p.pos) <= range) return true; p = null; return false; }

    void TryDock(string src){
      if (state != State.Sea || !NearPort(out var p)) return;
      if (encActive && encHostile && !enemy.sinking && !enemy.gone && Vector3.Distance(enemy.Pos, player.Pos) < 120f){ Banner("Not with an enemy on your stern!"); Debug.Log("[SG] dock blocked: enemy close"); return; }
      if (career.IsHostile(p.nation)){
        Banner($"The guns of {p.name} warn you off: {World.NationNames[(int)p.nation]} is hostile to you", 5f);
        Debug.Log($"[SG] dock refused {p.name} rep={career.rep[(int)p.nation]} by {src}"); return;
      }
      Debug.Log($"[SG] dock {p.name} by {src}");
      EnterTown(World.Ports.IndexOf(p), $"You drop anchor at {p.name}.");
    }
    void EnterTown(int idx, string msg){
      ClearFx(); encActive = false; enemy.Hide(); paused = mapOpen = false; Time.timeScale = 1f; ptrs.Clear();
      portIdx = idx; career.lastPort = idx; screen = Page.Hub; townMsg = msg; state = State.Town;
      player.speed = 0f; GameAudio.I?.SetScene(true, false); GameAudio.I?.Play("door"); GameAudio.I?.Play("bell");
      Debug.Log($"[SG] docked {Here.name} ({World.Adj[(int)Here.nation]}) gold={career.gold} hull={player.hull:F0}");
    }
    void SetSail(){
      var p = Here; var at = World.WaterNear(p.pos, 36f);
      for (float r = 36f; r < 120f; r += 6f){ bool ok = false;   // open water that is nearest to this port (St. Kitts and Sint Eustatius are close neighbours)
        for (int i = 0; i < 24 && !ok; i++){ var q = p.pos + Quaternion.Euler(0f, i * 15f, 0f) * Vector3.forward * r; if (World.Land(q) < 0.08f && NearestPort(q) == p){ at = q; ok = true; } }
        if (ok) break; }
      var d = at - p.pos;
      player.transform.position = new Vector3(at.x, 0f, at.z); player.heading = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; player.transform.rotation = Quaternion.Euler(0f, player.heading, 0f);
      player.sail = 0.6f; player.speed = 2f; camFocus = player.Pos;
      state = State.Sea; stateT = 0f; nextEncounter = Time.time + 20f; GameAudio.I?.SetScene(false, false); GameAudio.I?.Play("sails");
      Debug.Log($"[SG] set sail from {p.name}");
    }
    void WashAshore(){
      int best = -1; float bd = 1e9f;
      for (int i = 0; i < World.Ports.Count; i++){ var q = World.Ports[i]; float d = Vector3.Distance(player.Pos, q.pos) + (career.IsHostile(q.nation) ? 1e6f : 0f); if (d < bd){ bd = d; best = i; } }
      career.gold = Mathf.RoundToInt(career.gold * 0.7f); for (int i = 0; i < 5; i++) career.cargo[i] = 0;
      player.Reset(World.Ports[best].pos, 0f, 0.6f); player.hull = 50f; player.sails = 50f; player.crew = Mathf.Min(player.crew + 20f, 40f);
      Debug.Log("[SG] washed ashore");
      EnterTown(best, "Your sloop was lost. A fishing boat brought you and what crew survived ashore, and the shipwright raised a patched hull for you. The cargo is gone.");
    }

    // ---------- careers and saves ----------
    void NewCareer(){
      career = new Career(); player.Reset(World.Ports[0].pos, 150f, 0.6f); player.crew = career.crew;
      Debug.Log("[SG] new career");
      EnterTown(0, "1660. You are a young captain with a small sloop, 600 gold and a crew of 70. Port Royal is yours to start from.");
    }
    void SyncCareer(){
      career.x = player.Pos.x; career.z = player.Pos.z; career.heading = player.heading;
      career.hull = player.hull; career.sails = player.sails; career.crew = player.crew; career.where = Here.name;
    }
    void SaveTo(int slot){
      SyncCareer(); Saves.Save(slot, career); townMsg = $"Saved to slot {slot + 1}.";
      Debug.Log($"[SG] saved slot {slot + 1} port={Here.name} gold={career.gold} hold={career.Hold} rep={string.Join(",", career.rep)}");
    }
    void LoadFrom(int slot){
      var c = Saves.Load(slot); if (c == null) return;
      career = c; player.Reset(new Vector3(c.x, 0f, c.z), c.heading, 0.6f); player.hull = c.hull; player.sails = c.sails; player.crew = c.crew;
      Debug.Log($"[SG] loaded slot {slot + 1} port={c.where} gold={c.gold} hold={c.Hold} rep={string.Join(",", c.rep)} marque={c.marque}");
      int idx = Mathf.Clamp(c.lastPort, 0, World.Ports.Count - 1);
      EnterTown(idx, $"Game loaded (slot {slot + 1}, saved {c.savedAt}).");
    }

    // ---------- town actions ----------
    int Buy(int g) => Mathf.RoundToInt(Here.price[g]);
    int Sell(int g) => Mathf.RoundToInt(Here.price[g] * 0.85f);
    void Trade(int g, int n){
      if (n > 0){
        n = Mathf.Min(n, Career.HoldSize - career.Hold, career.gold / Mathf.Max(1, Buy(g)));
        if (n <= 0){ townMsg = career.Hold >= Career.HoldSize ? "Your hold is full." : "You can't afford that."; return; }
        career.gold -= n * Buy(g); career.cargo[g] += n; townMsg = $"Bought {n} {World.Goods[g]} for {n * Buy(g)} gold.";
      } else {
        n = Mathf.Min(-n, career.cargo[g]); if (n <= 0){ townMsg = $"You have no {World.Goods[g]} to sell."; return; }
        career.gold += n * Sell(g); career.cargo[g] -= n; townMsg = $"Sold {n} {World.Goods[g]} for {n * Sell(g)} gold.";
      }
      GameAudio.I?.Play("coins");
      Debug.Log($"[SG] trade {World.Goods[g]} {townMsg} gold={career.gold} hold={career.Hold} at {Here.name}");
    }
    void Repair(bool hull){
      float have = hull ? player.hull : player.sails; int per = hull ? 3 : 2;
      int pts = Mathf.Min(Mathf.CeilToInt(100f - have), career.gold / per);
      if (pts <= 0){ townMsg = have >= 100f ? "Nothing to repair." : "You can't afford repairs."; return; }
      career.gold -= pts * per; if (hull) player.hull = Mathf.Min(100f, have + pts); else player.sails = Mathf.Min(100f, have + pts);
      townMsg = $"The shipwright mends {(hull ? "the hull" : "the sails")} ({pts} points, {pts * per} gold)."; GameAudio.I?.Play("coins");
      Debug.Log($"[SG] repair {(hull ? "hull" : "sails")} pts={pts} gold={career.gold} hull={player.hull:F0} sails={player.sails:F0}");
    }
    void Recruit(){
      int n = Mathf.Min(10, Career.MaxCrew - Mathf.RoundToInt(player.crew));
      if (n <= 0){ townMsg = "Your sloop can't berth any more hands."; return; }
      if (career.gold < 60){ townMsg = "Nobody signs on without the 60 gold bounty."; return; }
      career.gold -= 60; player.crew = Mathf.Min(Career.MaxCrew, player.crew + n); townMsg = $"{n} sailors sign on. Crew {player.crew:F0}."; GameAudio.I?.Play("coins");
      Debug.Log($"[SG] recruit +{n} crew={player.crew:F0} gold={career.gold}");
    }
    Nation MissionTarget(Nation n) => n == Nation.Spain ? (portIdx % 2 == 0 ? Nation.England : Nation.France) : Nation.Spain;
    void MakeRumours(){
      rumours.Clear(); var r = new System.Random(portIdx * 31 + career.victories * 7 + career.gold);
      for (int k = 0; k < 3; k++){
        int g = r.Next(5); World.Port hi = World.Ports[0], lo = World.Ports[0];
        foreach (var p in World.Ports){ if (p.price[g] > hi.price[g]) hi = p; if (p.price[g] < lo.price[g]) lo = p; }
        rumours.Add(k == 1 ? $"\"{World.Goods[g]} is cheap in {lo.name}, they say.\"" : $"\"Merchants in {hi.name} pay well for {World.Goods[g]}.\"");
      }
      rumours.Add($"\"Pirates have been seen off {World.Ports[r.Next(World.Ports.Count)].name}. Keep your guns loaded.\"");
      rumours.Add("\"St. Kitts is a queer place: the English hold the middle of the island and the French both ends, with their town at Basseterre. They share it, but not happily.\"");
      rumours.Add("\"Spain is at war with England, France and the Dutch, and the English and Dutch are at each other's throats too.\"");
      for (int i = 0; i < 4; i++) if (career.IsHostile((Nation)i)) rumours.Add($"\"The {World.Adj[i]} would hang you on sight, captain. Stay clear of their ports.\"");
      rumourAt = r.Next(rumours.Count);
    }

    // ---------- input ----------
    void UpdateMenus(Keyboard kb, Gamepad gp){
      foreach (var p in Pointers()) if (p.began){ clickPending = true; clickPos = p.p; }
      bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.buttonEast.wasPressedThisFrame);
      if (state == State.Title){
        if (back) titleLoad = false;
        if (!titleLoad && ((kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) || (gp != null && gp.buttonSouth.wasPressedThisFrame))){
          int l = Saves.Latest(); if (l >= 0) LoadFrom(l); else NewCareer();
        }
        return;
      }
      if (back && screen != Page.Hub){ screen = Page.Hub; GameAudio.I?.SetScene(true, false); townMsg = ""; }
    }
    void Go(Page s){
      screen = s; townMsg = ""; if (s == Page.Tavern) MakeRumours();
      GameAudio.I?.SetScene(true, s == Page.Tavern); if (s != Page.Hub && s != Page.Save) GameAudio.I?.Play("door");
      Debug.Log($"[SG] town screen {s} at {Here.name}");
    }

    // ---------- GUI ----------
    GUIStyle panelSt, btnSt, btnHiSt, inkSt, inkBig, inkSmall;
    float bgK; Vector2 bgO;
    void Styles(float s){
      panelSt ??= new GUIStyle{ border = new RectOffset(64, 64, 64, 64) }; panelSt.normal.background = panelTex ? panelTex : white;
      btnSt ??= new GUIStyle{ border = new RectOffset(24, 24, 24, 24), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
      btnHiSt ??= new GUIStyle(btnSt);
      btnSt.normal.background = buttonTex ? buttonTex : white; btnHiSt.normal.background = buttonHiTex ? buttonHiTex : white;
      btnSt.normal.textColor = btnHiSt.normal.textColor = Ink; btnSt.fontSize = btnHiSt.fontSize = Mathf.RoundToInt(17 * s);
      inkSt ??= new GUIStyle(GUI.skin.label){ wordWrap = true }; inkBig ??= new GUIStyle(GUI.skin.label){ fontStyle = FontStyle.Bold, wordWrap = true }; inkSmall ??= new GUIStyle(GUI.skin.label){ wordWrap = true };
      inkSt.fontSize = Mathf.RoundToInt(17 * s); inkBig.fontSize = Mathf.RoundToInt(26 * s); inkSmall.fontSize = Mathf.RoundToInt(14 * s);
      inkSt.normal.textColor = inkBig.normal.textColor = inkSmall.normal.textColor = Ink;
    }
    void PanelBox(Rect r){ if (panelTex) GUI.Box(r, GUIContent.none, panelSt); else Box(r, new Color(0.93f, 0.86f, 0.68f, 0.95f)); }
    /// <summary>Point-and-click button: drawn on Repaint, fires once for a click / tap that began inside it this frame.</summary>
    bool Btn(Rect r, string label, bool enabled = true){
      var e = Event.current; bool hover = enabled && r.Contains(e.mousePosition);
      if (e.type == EventType.Repaint){
        var oc = GUI.color; if (!enabled) GUI.color = new Color(1f, 1f, 1f, 0.45f);
        if (buttonTex) GUI.Box(r, label, hover ? btnHiSt : btnSt); else { Box(r, hover ? new Color(0.98f, 0.9f, 0.7f) : new Color(0.88f, 0.8f, 0.62f)); Label(r, label, btnSt, Ink); }
        GUI.color = oc;
        if (enabled && clickPending && r.Contains(clickPos)){ clickPending = false; return true; }
      }
      return false;
    }
    Rect R(float x, float y, float w, float h) => new(bgO.x + x * bgK, bgO.y + y * bgK, w * bgK, h * bgK);   // 1280x720 design space, same fit as the backgrounds
    void Background(Texture2D t, float W, float H){
      if (t) GUI.DrawTexture(new Rect(0, 0, W, H), t, ScaleMode.ScaleAndCrop); else Box(new Rect(0, 0, W, H), new Color(0.2f, 0.15f, 0.1f));
    }
    void Flag(int nation, Vector3 a){
      var f = flagTex[nation]; if (!f || a.z <= 0f) return;
      GUI.DrawTexture(R(a.x, a.y, f.width * a.z, f.height * a.z), f);
    }
    void Icon(Texture2D t, Rect r){ if (t) GUI.DrawTexture(r, t, ScaleMode.ScaleToFit); }

    void MenusGUI(float s, float W, float H){
      Styles(s);
      bgK = Mathf.Max(W / 1280f, H / 720f); bgO = new Vector2((W - 1280f * bgK) / 2f, (H - 720f * bgK) / 2f);
      // UI rects use a "contain" fit so nothing is cropped off-screen
      float uk = Mathf.Min(W / 1280f, H / 720f); var keep = (bgK, bgO);
      if (state == State.Title){ TitleGUI(W, H, uk); }
      else TownGUI(W, H, uk, keep.bgK, keep.bgO);
      clickPending = Event.current.type == EventType.Repaint ? false : clickPending;
    }
    void UseUiFit(float W, float H, float uk){ bgK = uk; bgO = new Vector2((W - 1280f * uk) / 2f, (H - 720f * uk) / 2f); }

    void TitleGUI(float W, float H, float uk){
      Background(titleTex, W, H); Box(new Rect(0, 0, W, H), new Color(0.05f, 0.04f, 0.03f, 0.35f));
      UseUiFit(W, H, uk);
      PanelBox(R(390, 90, 500, 560));
      Label(R(390, 150, 500, 60), "Salt and Glory", new GUIStyle(inkBig){ alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(44 * uk) }, Ink);
      Label(R(430, 210, 420, 30), "A captain's career in the Caribbean, 1660", new GUIStyle(inkSt){ alignment = TextAnchor.MiddleCenter }, Ink);
      int latest = Saves.Latest();
      if (!titleLoad){
        if (Btn(R(490, 270, 300, 64), "New career")) NewCareer();
        if (Btn(R(490, 350, 300, 64), "Continue", latest >= 0)) LoadFrom(latest);
        if (Btn(R(490, 430, 300, 64), "Load")){ titleLoad = true; Debug.Log("[SG] title load menu"); }
        Label(R(430, 510, 420, 110), "Sail: A/D steer, W/S sails, Q/E broadsides, Tab shot, Enter dock, M map, Esc or Start pause.\nGamepad and touch work too. Towns are point and click.", new GUIStyle(inkSmall){ alignment = TextAnchor.UpperCenter }, Ink);
      } else {
        for (int i = 0; i < Saves.Slots; i++){ var c = Saves.Load(i); if (Btn(R(450, 270 + i * 80, 380, 66), SlotLabel(i, c), c != null)) LoadFrom(i); }
        if (Btn(R(540, 520, 200, 56), "Back")) titleLoad = false;
      }
    }
    static string SlotLabel(int i, Career c) => c == null ? $"Slot {i + 1}: empty" : $"Slot {i + 1}: {c.where}, {c.gold} gold\n{c.savedAt}";

    void TownGUI(float W, float H, float uk, float k0, Vector2 o0){
      var p = Here; int n = (int)p.nation;
      var bg = screen switch { Page.Tavern => tavernTex, Page.Governor => governorTex, Page.Merchant => merchantTex, Page.Shipwright => shipwrightTex, _ => harborTex[n] };
      if (!bg) bg = titleTex;
      Background(bg, W, H);
      if (screen == Page.Hub || screen == Page.Save) Flag(n, flagAnchors[n]); else if (screen == Page.Governor) Flag(n, flagAnchors[4]);   // flags placed with the cover fit of the background
      UseUiFit(W, H, uk);
      // status (top-left) and reputation (top-right)
      PanelBox(R(16, 16, 360, 200));
      Icon(flagIconTex[n], R(40, 38, 54, 54));
      Label(R(100, 36, 260, 32), p.name, inkBig, Ink); Label(R(100, 68, 260, 24), $"{World.NationNames[n]} · {Career.Standing(career.rep[n])}", inkSmall, Ink);
      Icon(coinTex, R(40, 100, 30, 30)); Label(R(76, 102, 280, 26), $"{career.gold} gold    Hold {career.Hold}/{Career.HoldSize}", inkSt, Ink);
      Label(R(40, 130, 320, 50), $"Hull {player.hull:F0}   Sails {player.sails:F0}   Crew {player.crew:F0}\nVictories {career.victories}", inkSmall, Ink);
      PanelBox(R(924, 16, 340, 200));
      for (int i = 0; i < 4; i++){
        Icon(flagIconTex[i], R(946, 34 + i * 36, 32, 32));
        Label(R(984, 38 + i * 36, 260, 26), $"{World.Adj[i]}: {Career.Standing(career.rep[i])} ({career.rep[i]}){(career.marque == i ? "  · marque" : "")}", career.IsHostile((Nation)i) ? new GUIStyle(inkSmall){ normal = { textColor = new Color(0.6f, 0.1f, 0.08f) } } : inkSmall, Ink);
      }
      switch (screen){
        case Page.Hub: HubGUI(); break;
        case Page.Tavern: TavernGUI(); break;
        case Page.Governor: GovernorGUI(); break;
        case Page.Merchant: MerchantGUI(); break;
        case Page.Shipwright: ShipwrightGUI(); break;
        case Page.Save: SaveGUI(); break;
      }
      if (screen != Page.Hub && Btn(R(1090, 640, 170, 56), "Back (Esc)")) Go(Page.Hub);
    }
    void Msg(Rect r){ if (!string.IsNullOrEmpty(townMsg)) Label(r, townMsg, inkSt, Ink); }
    void HubGUI(){
      PanelBox(R(40, 548, 1200, 160));
      Msg(R(86, 568, 1110, 50));
      string[] names = { "Tavern", "Governor", "Merchant", "Shipwright", "Save game", "Set sail" };
      for (int i = 0; i < 6; i++){
        if (!Btn(R(78 + i * 192, 622, 180, 60), names[i])) continue;
        if (i < 4) Go((Page)(i + 1)); else if (i == 4) Go(Page.Save); else SetSail();
      }
    }
    void Sheet(out Rect r){ r = R(150, 232, 980, 400); PanelBox(r); }
    void TavernGUI(){
      Sheet(out _);
      Label(R(200, 270, 880, 40), $"The tavern at {Here.name}", inkBig, Ink);
      if (rumours.Count > 0) Label(R(200, 318, 880, 70), rumours[rumourAt % rumours.Count], inkSt, Ink);
      if (Btn(R(200, 400, 280, 56), "Listen for more")){ rumourAt++; Debug.Log("[SG] tavern rumour " + rumours[rumourAt % rumours.Count]); }
      if (Btn(R(500, 400, 360, 56), $"Recruit 10 hands (60 gold)", player.crew < Career.MaxCrew)) Recruit();
      Label(R(200, 470, 880, 30), $"Crew {player.crew:F0}/{Career.MaxCrew}. More hands reload the guns faster.", inkSmall, Ink);
      Msg(R(200, 510, 880, 80));
    }
    void GovernorGUI(){
      var n = Here.nation; int ni = (int)n; var tgt = MissionTarget(n);
      Sheet(out _);
      Label(R(200, 262, 880, 40), $"The governor of {Here.name}", inkBig, Ink);
      // letter of marque
      string enemies = ""; for (int i = 0; i < 4; i++) if (World.AtWar(n, (Nation)i)) enemies += (enemies.Length > 0 ? " and " : "") + World.Adj[i];
      if (career.marque == ni) Label(R(200, 306, 880, 50), $"You sail under our letter of marque. Attacking {enemies} ships is lawful, and earns our thanks.", inkSt, Ink);
      else if (career.rep[ni] < 0) Label(R(200, 306, 880, 50), "\"Prove yourself a friend of the crown first.\" (Standing must be at least Neutral.)", inkSt, Ink);
      else {
        Label(R(200, 306, 880, 50), $"A letter of marque against the {enemies}" + (career.marque >= 0 ? $" (replaces your {World.Adj[career.marque]} letter)" : ""), inkSt, Ink);
        if (Btn(R(200, 350, 360, 52), "Accept the letter of marque")){
          career.marque = ni; career.AddRep(n, 5); townMsg = $"You now sail as a {World.Adj[ni]} privateer.";
          Debug.Log($"[SG] marque {World.Adj[ni]} rep={string.Join(",", career.rep)}");
        }
      }
      // mission
      if (career.missionFrom == -1){
        int reward = 350 + 50 * (portIdx % 4);
        Label(R(200, 420, 880, 30), $"Mission: sink a {World.Adj[(int)tgt]} ship. Reward {reward} gold.", inkSt, Ink);
        if (Btn(R(200, 456, 260, 52), "Accept mission")){
          career.missionFrom = ni; career.missionTarget = (int)tgt; career.missionReward = reward; career.missionDone = false;
          townMsg = "\"Bring me word of her sinking.\""; Debug.Log($"[SG] mission accepted target={World.Adj[(int)tgt]} reward={reward}");
        }
      } else if (career.missionFrom == ni && career.missionDone){
        Label(R(200, 420, 880, 30), $"Your {World.Adj[career.missionTarget]} prize is the talk of the town.", inkSt, Ink);
        if (Btn(R(200, 456, 300, 52), $"Claim {career.missionReward} gold")){
          career.gold += career.missionReward; career.AddRep(n, 10); GameAudio.I?.Play("coins");
          townMsg = $"The governor pays {career.missionReward} gold."; Debug.Log($"[SG] mission reward {career.missionReward} gold={career.gold} rep={string.Join(",", career.rep)}");
          career.missionFrom = career.missionTarget = -1; career.missionDone = false;
        }
      } else if (career.missionFrom == ni) Label(R(200, 420, 880, 60), $"Your task: sink a {World.Adj[career.missionTarget]} ship, then come back to any {World.Adj[ni]} governor." + (career.marque == ni ? "" : " (A letter of marque makes it lawful.)"), inkSt, Ink);
      else Label(R(200, 420, 880, 60), $"You are on {World.Adj[career.missionFrom]} business (sink a {World.Adj[career.missionTarget]} ship).", inkSt, Ink);
      Msg(R(200, 540, 880, 60));
    }
    void MerchantGUI(){
      Sheet(out _);
      Label(R(190, 250, 600, 36), $"Merchant of {Here.name}", inkBig, Ink);
      Label(R(760, 256, 340, 30), $"{career.gold} gold   Hold {career.Hold}/{Career.HoldSize}", new GUIStyle(inkSt){ alignment = TextAnchor.MiddleRight }, Ink);
      for (int g = 0; g < 5; g++){
        float y = 296 + g * 54;
        Icon(cargoTex[g], R(190, y, 46, 46));
        Label(R(246, y + 10, 150, 30), World.Goods[g], inkSt, Ink);
        Label(R(390, y + 10, 220, 30), $"Buy {Buy(g)}  ·  Sell {Sell(g)}", inkSt, Ink);
        Label(R(610, y + 10, 100, 30), $"Hold {career.cargo[g]}", inkSt, Ink);
        if (Btn(R(712, y + 2, 92, 44), "Buy 1")) Trade(g, 1);
        if (Btn(R(810, y + 2, 92, 44), "Buy 5")) Trade(g, 5);
        if (Btn(R(908, y + 2, 92, 44), "Sell 1", career.cargo[g] > 0)) Trade(g, -1);
        if (Btn(R(1006, y + 2, 100, 44), "Sell all", career.cargo[g] > 0)) Trade(g, -career.cargo[g]);
      }
      Msg(R(190, 572, 880, 50));
    }
    void ShipwrightGUI(){
      Sheet(out _);
      Label(R(200, 270, 880, 40), $"Shipwright of {Here.name}", inkBig, Ink);
      Label(R(200, 320, 880, 30), $"Hull {player.hull:F0}/100 (3 gold a point)    Sails {player.sails:F0}/100 (2 gold a point)    Gold {career.gold}", inkSt, Ink);
      int hc = Mathf.CeilToInt(100f - player.hull) * 3, sc = Mathf.CeilToInt(100f - player.sails) * 2;
      if (Btn(R(200, 370, 330, 56), hc > 0 ? $"Repair hull ({hc} gold)" : "Hull is sound", hc > 0)) Repair(true);
      if (Btn(R(550, 370, 330, 56), sc > 0 ? $"Repair sails ({sc} gold)" : "Sails are sound", sc > 0)) Repair(false);
      Label(R(200, 440, 880, 30), "If you are short of gold, the shipwright repairs what you can pay for.", inkSmall, Ink);
      Msg(R(200, 480, 880, 60));
    }
    void SaveGUI(){
      Sheet(out _);
      Label(R(200, 262, 880, 40), "Save your career", inkBig, Ink);
      for (int i = 0; i < Saves.Slots; i++) if (Btn(R(200, 316 + i * 76, 560, 64), SlotLabel(i, Saves.Load(i)))) SaveTo(i);
      Label(R(780, 330, 320, 120), "Saves stay in this browser (localStorage). Continue on the title screen loads the newest one.", inkSmall, Ink);
      Msg(R(780, 460, 320, 80));
    }

    /// <summary>The world map overlay (sea only): the chart, port markers ringed in nation colours, your ship and the met ship.</summary>
    void MapGUI(float s, float W, float H){
      Box(new Rect(0, 0, W, H), new Color(0.05f, 0.04f, 0.03f, 0.7f));
      var tex = mapTex; if (!tex){ chartFallback ??= World.MakeChart(); tex = chartFallback; }
      float mw = Mathf.Min(W - 40 * s, (H - 60 * s) * World.W / World.H), mh = mw * World.H / World.W;
      var mr = new Rect((W - mw) / 2f, (H - mh) / 2f + 14 * s, mw, mh); GUI.DrawTexture(mr, tex, ScaleMode.StretchToFill);
      Vector2 ToScr(Vector3 p){ var uv = World.ToUV(p); return new Vector2(mr.x + uv.x * mr.width, mr.y + (1f - uv.y) * mr.height); }
      float ms = Mathf.Max(22f, mr.width / 60f); var o = GUI.color;
      foreach (var p in World.Ports){
        var c = ToScr(p.pos);
        GUI.color = World.NationColor[(int)p.nation]; if (portRingTex) GUI.DrawTexture(new Rect(c.x - ms * 0.7f, c.y - ms * 0.7f, ms * 1.4f, ms * 1.4f), portRingTex); else Box(new Rect(c.x - ms * 0.5f, c.y - ms * 0.5f, ms, ms), GUI.color);
        GUI.color = o; if (portMarkerTex) GUI.DrawTexture(new Rect(c.x - ms * 0.5f, c.y - ms * 0.5f, ms, ms), portMarkerTex);
        bool hostile = career.IsHostile(p.nation);
        var st = new GUIStyle(txt){ alignment = TextAnchor.UpperCenter, fontSize = Mathf.RoundToInt(15 * s) };
        Label(new Rect(c.x - 110 * s + 1, c.y + ms * 0.7f + 1, 220 * s, 22 * s), p.name + (hostile ? " (hostile)" : ""), st, new Color(1f, 0.95f, 0.85f, 0.8f));
        Label(new Rect(c.x - 110 * s, c.y + ms * 0.7f, 220 * s, 22 * s), p.name + (hostile ? " (hostile)" : ""), st, hostile ? new Color(0.6f, 0.08f, 0.06f) : Ink);
      }
      if (encActive && !enemy.gone){ var e = ToScr(enemy.Pos); Box(new Rect(e.x - 5 * s, e.y - 5 * s, 10 * s, 10 * s), encHostile ? new Color(0.8f, 0.15f, 0.1f) : Ink); }
      var sp = ToScr(player.Pos); var m = GUI.matrix;
      GUIUtility.RotateAroundPivot(player.heading, sp);   // the marker's bow points up; heading 0 = north
      if (shipMarkerTex) GUI.DrawTexture(new Rect(sp.x - ms * 0.8f, sp.y - ms * 0.8f, ms * 1.6f, ms * 1.6f), shipMarkerTex); else { GUI.color = new Color(0.6f, 0.1f, 0.08f); GUI.DrawTexture(new Rect(sp.x - 10 * s, sp.y - 16 * s, 20 * s, 32 * s), arrow); }
      GUI.matrix = m; GUI.color = o;
      Label(new Rect(0, 4 * s, W, 30 * s), "The Caribbean, 1660   ·   M, Back, Esc or tap to close", new GUIStyle(txt){ alignment = TextAnchor.MiddleCenter }, Parchment);
    }
  }
}

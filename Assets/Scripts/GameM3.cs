using UnityEngine;
using UnityEngine.InputSystem;
namespace SaltGlory {
  /// <summary>M3: boarding into side-on deck duels (high / mid / low attacks, hold to block, timed block = parry, push the loser
  /// off the deck), capturing ships, treasure maps and digs, dynamic port prices and fame. Duel and treasure art load by name
  /// (BuildWeb) and fall back to simple shapes.</summary>
  public partial class Game {
    [Header("M3 art (null = fallback shapes)")]
    public Texture2D duelDeckTex, captainPlayerTex, captainEnemyTex, chestTex, mapPieceTex, digMarkTex;

    // ---------- prices (M2 QA: no stable money loops) ----------
    const float PricePerUnit = 0.012f, PriceRelax = 480f;   // each unit moves that port's price 1.2%; drifts back with an 8-minute time constant
    float Pressure(int port, int g) => career.market != null && career.market.Length > port * 5 + g ? career.market[port * 5 + g] : 0f;
    int BuyAt(int port, int g) => Mathf.Max(1, Mathf.RoundToInt(World.Ports[port].price[g] * (1f + Pressure(port, g))));
    int SellAt(int port, int g) => Mathf.Max(1, Mathf.RoundToInt(World.Ports[port].price[g] * 0.85f * (1f + Pressure(port, g))));
    void Push(int port, int g, float d){ int i = port * 5 + g; if (career.market != null && i < career.market.Length) career.market[i] = Mathf.Clamp(career.market[i] + d, -0.6f, 1f); }
    void DriftPrices(float dt){
      if (dt <= 0f || career.market == null) return; float k = Mathf.Exp(-dt / PriceRelax);
      for (int i = 0; i < career.market.Length; i++) career.market[i] *= k;
    }

    // ---------- the Enter / A / tap prompt at sea: board > dig > dock ----------
    float BoardRange => 10f + player.halfW * player.transform.localScale.x + 2f * enemy.halfW * enemy.transform.localScale.x;
    bool CanBoard() => state == State.Sea && encActive && !enemy.gone && !enemy.sinking && !player.sinking && Vector3.Distance(player.Pos, enemy.Pos) < BoardRange;
    bool NearDig() => state == State.Sea && career.digActive && digT < 0f && Vector3.Distance(player.Pos, new Vector3(career.digX, 0f, career.digZ)) < 32f;
    string Prompt(){
      if (CanBoard()) return $"Board the {enemy.label}";
      if (NearDig()) return "Row ashore and dig";
      return NearPort(out var p) ? $"Dock at {p.name}" : null;
    }
    void Interact(string src){
      if (CanBoard()){ Board(src); return; }
      if (NearDig()){ StartDig(src); return; }
      TryDock(src);
    }

    // ---------- fame and rank ----------
    void Fame(int pts, string why){ career.fame += pts; Debug.Log($"[SG] fame +{pts} ({why}) = {career.fame}"); }
    bool LawfulPrize => !encPirate && encLegal && career.marque >= 0 && World.AtWar((Nation)career.marque, encNation);

    // ---------- duels ----------
    class Fighter { public float x, act, stagger, blockFrom = -9f, fall = -1f, flashT; public int phase, h = 1, guardH = 1, dir; public bool block; public string flash = ""; }
    const float DeckL = 10f, ParryWindow = 0.22f, Reach = 2.5f, MinGap = 1.3f;
    static readonly float[] Windup = { 0.26f, 0.34f, 0.40f };   // high is the fast slash, low the slow sweep
    static readonly string[] HName = { "HIGH", "MID", "LOW" };
    readonly Fighter dp = new(), de = new();
    float duelT, aiT, aiReactAt = -1f, aiBlockUntil, aggr, reactP, parryP, smartP; bool aiWillBlock, aiParry; int aiGuard;
    int duelOver;   // 0 fighting, 1 won, 2 lost
    string duelResult = ""; float duelOverT;
    // touch: left 30% of the screen is a step stick; elsewhere swipe up / across / down to attack, hold to block, tap to parry
    int dStick = -99, dGest = -99; float dStickX, dGestT, dTouchBlockUntil; Vector2 dGestP; bool dGestHold;

    void Board(string src){
      if (!encHostile) BeginBattle(false);   // boarding a neutral is an attack
      ClearFx(); player.speed = enemy.speed = 0f;
      GameAudio.I?.Play("grapple"); GameAudio.I?.SetDuel(true);
      state = State.Duel; stateT = 0f; duelT = 0f; duelOver = 0; duelResult = ""; ptrs.Clear(); dStick = dGest = -99;
      // crew sizes set the starting line: more hands push the fight toward the enemy's rail (grape shot pays off here)
      float pc = Mathf.Max(1f, player.crew), ec = Mathf.Max(0f, enemy.crew), c = DeckL / 2f + Mathf.Clamp((pc - ec) / (pc + ec), -1f, 1f) * 3f;
      Init(dp, c - 0.9f, 1); Init(de, c + 0.9f, -1);
      bool war = encWarship || encTreasure;   // the AI captain: warships and treasure guards fight best, merchants worst
      aggr = war ? 0.62f : encPirate ? 0.58f : 0.42f; reactP = war ? 0.55f : encPirate ? 0.48f : 0.35f; parryP = war ? 0.16f : 0.1f; smartP = war ? 0.6f : 0.4f;
      aiT = 1.2f; aiReactAt = -1f;
      Debug.Log($"[SG] boarding {enemy.label} by {src} crew={pc:F0} vs {ec:F0} start={c:F2}");
    }
    void Init(Fighter f, float x, int dir){ f.x = x; f.dir = dir; f.act = f.stagger = 0f; f.phase = 0; f.h = f.guardH = 1; f.block = false; f.blockFrom = -9f; f.fall = -1f; f.flash = ""; f.flashT = 0f; }
    static bool Blocks(int attack, int guard) => Mathf.Abs(attack - guard) <= 1;   // high beats a low guard, low beats a high guard

    void UpdateDuel(Keyboard kb, Gamepad gp, float dt){
      duelT += dt;
      if (duelOver > 0){
        if (dp.fall >= 0f) dp.fall += dt; if (de.fall >= 0f) de.fall += dt;
        if (duelT - duelOverT > 1.2f) MenuInput(kb, gp);
        return;
      }
      // player input: A/D step, W / S (else mid) + J attack, hold K block (tap just before a hit = parry); gamepad stick + X / B
      float move = 0f; int stance = 1; bool wantBlock = false, attack = false; int attackH = -1;
      if (kb != null){
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move -= 1f; if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move += 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) stance = 0; else if (kb.sKey.isPressed || kb.downArrowKey.isPressed) stance = 2;
        wantBlock |= kb.kKey.isPressed; attack |= kb.jKey.wasPressedThisFrame;
      }
      if (gp != null){
        var st = gp.leftStick.ReadValue(); if (Mathf.Abs(st.x) > 0.3f) move += st.x; if (gp.dpad.left.isPressed) move -= 1f; if (gp.dpad.right.isPressed) move += 1f;
        if (st.y > 0.5f || gp.dpad.up.isPressed) stance = 0; else if (st.y < -0.5f || gp.dpad.down.isPressed) stance = 2;
        wantBlock |= gp.buttonEast.isPressed; attack |= gp.buttonWest.wasPressedThisFrame;
      }
      foreach (var (id, p, began, down) in Pointers()){
        string src = id < 0 ? "mouse" : "touch";
        if (began){ if (p.x < Screen.width * 0.3f){ dStick = id; dStickX = p.x; } else { dGest = id; dGestP = p; dGestT = duelT; dGestHold = false; } }
        if (id == dStick){ if (down) move += Mathf.Clamp((p.x - dStickX) / (80f * S), -1f, 1f); else dStick = -99; }
        else if (id == dGest){
          var d = p - dGestP;
          if (down){ if (duelT - dGestT > 0.18f && d.magnitude < 30f * S) dGestHold = true; if (dGestHold) wantBlock = true; }
          else {
            if (!dGestHold && d.magnitude >= 50f * S){ attack = true; attackH = Mathf.Abs(d.y) > Mathf.Abs(d.x) ? (d.y < 0f ? 0 : 2) : 1; Debug.Log($"[SG] duel swipe {HName[attackH]} by {src}"); }
            else if (!dGestHold) dTouchBlockUntil = duelT + 0.25f;   // a quick tap: a short block, i.e. a parry attempt
            dGest = -99;
          }
        }
      }
      if (duelT < dTouchBlockUntil) wantBlock = true;
      Step(dp, de, Mathf.Clamp(move, -1f, 1f), wantBlock, attack ? (attackH >= 0 ? attackH : stance) : stance, attack, dt);
      // the enemy captain
      float gap = de.x - dp.x; float emove = gap > 2.1f ? 1f : gap < 1.5f ? -0.4f : 0f; bool eAttack = false; int eh = 1;
      if (dp.phase == 1 && aiReactAt < 0f){ aiReactAt = duelT + Random.Range(0.09f, 0.17f); aiWillBlock = Random.value < reactP; aiParry = Random.value < parryP; aiGuard = Random.value < smartP ? dp.h : 1; }
      if (dp.phase != 1 && aiReactAt >= 0f){ aiReactAt = -1f; aiBlockUntil = duelT + 0.2f; }
      bool eBlock = (aiReactAt >= 0f && duelT >= aiReactAt && aiWillBlock) || (duelT < aiBlockUntil && de.block);
      aiT -= dt;
      if (!eBlock && de.phase == 0 && aiT <= 0f && gap < Reach - 0.2f){
        aiT = Random.Range(0.55f, 1.15f);
        if (Random.value < aggr){ eAttack = true; eh = dp.block ? (dp.guardH == 0 ? 2 : dp.guardH == 2 ? 0 : (Random.value < 0.5f ? 0 : 2)) : Random.Range(0, 3); }
      }
      bool wasBlock = de.block;
      Step(de, dp, emove, eBlock, eBlock ? aiGuard : eh, eAttack, dt);
      if (de.block && !wasBlock && !aiParry) de.blockFrom = duelT - 1f;   // a plain block, not timed for a parry
      if (duelOver == 0 && (dp.fall >= 0f || de.fall >= 0f)) EndDuel(de.fall >= 0f);
    }
    void Step(Fighter f, Fighter o, float move, bool wantBlock, int stance, bool attack, float dt){
      if (f.fall >= 0f){ f.fall += dt; return; }
      f.flashT -= dt;
      if (f.stagger > 0f){ f.stagger -= dt; f.block = false; return; }
      if (f.phase == 1){ f.act -= dt; if (f.act <= 0f){ Resolve(f, o); f.phase = 2; f.act = 0.3f; } }
      else if (f.phase == 2){ f.act -= dt; if (f.act <= 0f) f.phase = 0; }
      else {
        if (wantBlock && !f.block) f.blockFrom = duelT;
        f.block = wantBlock; f.guardH = stance;
        if (attack && !f.block){ f.phase = 1; f.h = stance; f.act = Windup[stance] * (f == de ? 1.6f : 1f); GameAudio.I?.Play("whoosh"); }   // the AI telegraphs longer
        f.x += move * f.dir * (f.block ? 1.1f : 2.6f) * dt;
      }
      if (f.dir > 0) f.x = Mathf.Clamp(f.x, 0.25f, o.x - MinGap); else f.x = Mathf.Clamp(f.x, o.x + MinGap, DeckL - 0.25f);
    }
    void Resolve(Fighter a, Fighter d){
      bool pl = a == dp; string who = pl ? "player" : "enemy";
      if (Mathf.Abs(a.x - d.x) > Reach || d.fall >= 0f){ Debug.Log($"[SG] duel {who} {HName[a.h]} misses"); return; }
      if (d.block && Blocks(a.h, d.guardH)){
        if (duelT - d.blockFrom <= ParryWindow){   // parry: the attacker is open for a free hit
          a.stagger = 0.9f; a.phase = 0; Flash(d, "PARRY!"); GameAudio.I?.Play("parry");
          Debug.Log($"[SG] duel parry by {(pl ? "enemy" : "player")}"); return;
        }
        float pb = d.guardH == a.h ? 0.15f : 0.45f; d.x += a.dir * pb; Flash(d, "BLOCK"); GameAudio.I?.Clash();
        Debug.Log($"[SG] duel {who} {HName[a.h]} blocked ({HName[d.guardH]} guard)");
      } else {
        float push = 1.25f + (d.stagger > 0f ? 0.45f : 0f);
        d.x += a.dir * push; a.x += a.dir * push * 0.8f; d.phase = 0; d.block = false; d.stagger = 0.25f;
        Flash(d, "HIT"); GameAudio.I?.Play("body"); GameAudio.I?.Clash();
        Debug.Log($"[SG] duel hit {(pl ? "enemy" : "player")} {HName[a.h]} vs {(d.block ? HName[d.guardH] + " guard" : "no guard")} px={dp.x:F2} ex={de.x:F2}");
      }
      if (d.x < 0f || d.x > DeckL){ d.fall = 0f; GameAudio.I?.Play("overboard"); Debug.Log($"[SG] duel {(pl ? "enemy" : "player")} captain overboard"); }
    }
    void Flash(Fighter f, string s){ f.flash = s; f.flashT = 0.7f; }
    public string DuelStatus() => state != State.Duel ? "" :
      $"px={dp.x:F2} ex={de.x:F2} pph={dp.phase} eph={de.phase} eh={de.h} eact={de.act:F2} eblk={(de.block ? 1 : 0)} eg={de.guardH} over={duelOver}";

    void EndDuel(bool won){
      duelOver = won ? 1 : 2; duelOverT = duelT; focus = -1;
      if (won){
        career.duelsWon++; career.captures++; career.victories++;
        int fame = 10 + 15 + (enemy.cls == ShipClass.Galleon ? 15 : enemy.cls == ShipClass.Frigate ? 8 : 0); Fame(fame, "duel won, ship captured");
        if (LawfulPrize){ career.Serve(16); career.AddRep((Nation)career.marque, 6); }
        int gold = enemy.cls == ShipClass.Galleon ? Random.Range(1200, 2000) : encPirate ? Random.Range(300, 500) : encWarship ? Random.Range(200, 350) : Random.Range(250, 450);
        career.gold += gold; string loot = $"+{gold} gold";
        int goods = enemy.cls == ShipClass.Fluyt ? Random.Range(25, 55) : enemy.cls == ShipClass.Galleon ? Random.Range(15, 35) : encWarship ? 0 : Random.Range(6, 16);
        for (int left = goods; left > 0;){ int g = Random.Range(0, 5), n = Mathf.Min(left, Random.Range(4, 14), career.HoldMax - career.Hold); if (n <= 0) break; career.cargo[g] += n; left -= n; loot += $", {n} {World.Goods[g]}"; }
        float pieceP = enemy.cls == ShipClass.Galleon ? 1f : encPirate ? 0.7f : 0.35f;
        if (Random.value < pieceP){ loot += ", a torn map piece"; GainPiece("captured"); }
        if (!encPirate && career.missionTarget == (int)encNation && !career.missionDone){ career.missionDone = true; loot += ". Mission done: report to the governor"; }
        duelResult = $"The {enemy.label} is yours! {loot}.";
        GameAudio.I?.Play("cheer"); GameAudio.I?.Play("coins");
        Debug.Log($"[SG] duel won: captured {enemy.label} class={enemy.C.name} gold={career.gold} hold={career.Hold} fame={career.fame} pieces={career.mapPieces}");
      } else {
        int lost = Mathf.RoundToInt(career.gold * 0.25f); career.gold -= lost; for (int i = 0; i < 5; i++) career.cargo[i] = 0;
        player.crew = Mathf.Max(10f, player.crew * 0.5f);
        duelResult = $"You are thrown into the sea. Your crew fish you out, but the {enemy.label} sails off with your cargo and {lost} gold.";
        Debug.Log($"[SG] duel lost gold={career.gold} crew={player.crew:F0}");
      }
    }
    void FinishBoarding(bool switchShip){
      GameAudio.I?.SetDuel(false); GameAudio.I?.SetBattle(false);
      if (duelOver == 1 && switchShip){
        int from = player.cls; ApplyClass(player, enemy.cls); career.shipClass = player.cls;
        player.hull = Mathf.Max(enemy.hull, 40f); player.sails = Mathf.Max(enemy.sails, 40f); player.crew = Mathf.Min(player.crew, player.Complement); player.mastDown = false;
        int over = career.Hold - career.HoldMax; for (int g = 4; g >= 0 && over > 0; g--){ int n = Mathf.Min(over, career.cargo[g]); career.cargo[g] -= n; over -= n; }
        Banner($"You take command of the {player.C.name.ToLower()}. Your old {ShipClass.All[from].name.ToLower()} is scuttled", 5f);
        Debug.Log($"[SG] switched ship {ShipClass.All[from].name} -> {player.C.name} hull={player.hull:F0} hold={career.Hold}/{career.HoldMax}");
      } else if (duelOver == 1){ PatchUp("capture"); Banner($"A prize crew takes the {enemy.label} off to be sold", 4f); }
      else Banner("Beaten on your own deck. Live to fight another day", 4f);
      encActive = false; enemy.Hide(); nextEncounter = Time.time + Random.Range(30f, 50f);
      state = State.Sea; stateT = 0f; ptrs.Clear();
    }

    // ---------- treasure ----------
    float digT = -1f; float treasureShowUntil; int treasureGold;
    void GainPiece(string why){
      if (career.digActive){ career.gold += 80; Debug.Log($"[SG] map piece ({why}) but a map is already complete: sold for 80"); return; }
      career.mapPieces = Mathf.Min(4, career.mapPieces + 1);
      Debug.Log($"[SG] map piece ({why}) pieces={career.mapPieces}");
      if (career.mapPieces >= 4) PlaceDig(state == State.Town ? Here.pos : player.Pos);
    }
    /// <summary>Four pieces make a map: the dig site is open water off a nearby shore, 140-320 m from where the map was completed,
    /// in sight across open water (no island in between), so the X can be reached by sailing.</summary>
    void PlaceDig(Vector3 near){
      if (state == State.Town) near = World.WaterNear(near, 36f);
      Vector3 best = World.WaterNear(near, 120f); bool found = false;
      for (int i = 0; i < 600 && !found; i++){
        var q = near + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(140f, 320f);
        if (World.Land(q) > 0.03f) continue;
        bool clear = true; for (float t = 0.05f; t < 1f && clear; t += 0.03f) clear = World.Land(Vector3.Lerp(near, q, t)) < 0.25f;
        if (!clear) continue;
        for (int k = 0; k < 8 && !found; k++) if (World.Land(q + Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward * 26f) > 0.5f){ best = q; found = true; }
      }
      career.digActive = true; career.digX = best.x; career.digZ = best.z; career.digNear = NearestPort(best).name;
      Banner($"The four pieces make a map! X marks a beach near {career.digNear}", 6f); GameAudio.I?.Play("map");
      Debug.Log($"[SG] treasure map complete: dig at x={best.x:F0} z={best.z:F0} near {career.digNear} shore={found}");
    }
    void StartDig(string src){ digT = 0f; player.sail = 0f; GameAudio.I?.Play("dig"); Banner("Your crew row ashore and dig...", 2f); Debug.Log($"[SG] digging by {src}"); }
    void UpdateDig(float dt){
      if (digT < 0f) return; digT += dt;
      if (digT > 0.8f && digT - dt <= 0.8f) GameAudio.I?.Play("dig");
      if (digT < 1.8f) return;
      digT = -1f; treasureGold = Random.Range(700, 1300); career.gold += treasureGold; career.treasures++; career.digActive = false; career.mapPieces = 0;
      Fame(20, "treasure"); GameAudio.I?.Play("chest"); GameAudio.I?.Play("coins"); treasureShowUntil = Time.unscaledTime + 4f;
      Banner($"Buried treasure! The chest holds {treasureGold} gold", 6f);
      Debug.Log($"[SG] treasure dug gold+{treasureGold} gold={career.gold} treasures={career.treasures}");
    }

    // ---------- duel GUI (side-on stage; placeholder shapes until Art Dev's deck and captains land) ----------
    void DuelGUI(float s, float W, float H){
      Styles(s); float uk = Mathf.Min(W / 1280f, H / 720f); UseUiFit(W, H, uk); BeginButtons();
      // the stage: deck art if present, else sky, sea, the deck, its rails and the mast
      if (duelDeckTex) GUI.DrawTexture(new Rect(0, 0, W, H), duelDeckTex, ScaleMode.ScaleAndCrop);
      else {
        for (int i = 0; i < 12; i++) Box(new Rect(0, H * 0.55f * i / 12f, W, H * 0.55f / 12f + 1), Color.Lerp(new Color(0.55f, 0.72f, 0.82f), new Color(0.92f, 0.86f, 0.72f), i / 11f));
        Box(new Rect(0, H * 0.55f, W, H * 0.45f), new Color(0.13f, 0.38f, 0.42f));
        Box(R(110, 470, 1060, 70), new Color(0.48f, 0.32f, 0.18f)); for (int i = 0; i < 4; i++) Box(R(110, 482 + i * 15, 1060, 2), new Color(0.32f, 0.2f, 0.1f));
        Box(R(96, 380, 20, 160), new Color(0.36f, 0.23f, 0.12f)); Box(R(1164, 380, 20, 160), new Color(0.36f, 0.23f, 0.12f));
        Box(R(96, 380, 90, 12), new Color(0.36f, 0.23f, 0.12f)); Box(R(1094, 380, 90, 12), new Color(0.36f, 0.23f, 0.12f));
        Box(R(630, 40, 22, 432), new Color(0.4f, 0.26f, 0.14f)); Box(R(520, 120, 240, 150), new Color(0.95f, 0.92f, 0.82f, 0.9f));
      }
      DrawFighter(dp, new Color(0.2f, 0.32f, 0.6f), captainPlayerTex); DrawFighter(de, encPirate ? new Color(0.2f, 0.2f, 0.2f) : World.NationColor[(int)encNation], captainEnemyTex);
      // header: who, crews; the position bar shows how close each captain is to the rail
      PanelBox(R(320, 10, 640, 120));
      Label(R(360, 34, 560, 34), $"Boarding the {enemy.label}", new GUIStyle(inkBig){ alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(24 * uk) }, Ink);
      Label(R(360, 70, 560, 26), $"Your crew {player.crew:F0}  vs  theirs {enemy.crew:F0}", new GUIStyle(inkSt){ alignment = TextAnchor.MiddleCenter }, Ink);
      Label(R(40, 650, 1200, 50), "A/D step  ·  W, Space or S + J: high, mid or low attack  ·  hold K block, tap K just before a hit to parry  ·  Esc pause\nGamepad: stick + X attack, B block  ·  Touch: drag on the left to step, swipe up / across / down to attack, hold to block, tap to parry",
        new GUIStyle(small){ alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(14 * uk) }, Parchment);
      if (duelOver > 0 && duelT - duelOverT > 1.2f){
        PanelBox(R(290, 180, 700, 300));
        Label(R(330, 214, 620, 44), duelOver == 1 ? "Victory!" : "Defeat", new GUIStyle(inkBig){ alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(34 * uk) }, Ink);
        Label(R(340, 262, 600, 100), duelResult, new GUIStyle(inkSt){ alignment = TextAnchor.UpperCenter }, Ink);
        if (duelOver == 1){
          if (Btn(R(340, 380, 290, 60), $"Keep your {player.C.name.ToLower()}")) FinishBoarding(false);
          if (Btn(R(650, 380, 290, 60), $"Take the {enemy.C.name.ToLower()} (hold {enemy.C.hold})")) FinishBoarding(true);
        } else if (Btn(R(490, 380, 300, 60), "Back to sea")) FinishBoarding(false);
      }
      if (paused){ Box(new Rect(0, H * 0.36f, W, 100 * s), new Color(0.05f, 0.04f, 0.03f, 0.6f)); Label(new Rect(0, H * 0.36f + 20 * s, W, 60 * s), "Paused", big, Parchment); }
      EndButtons();
    }
    void DrawFighter(Fighter f, Color coat, Texture2D tex){
      float sx = 140f + f.x / DeckL * 1000f, fy = f.fall >= 0f ? Mathf.Min(f.fall, 1.2f) * Mathf.Min(f.fall, 1.2f) * 380f : 0f;
      float baseY = 478f + fy, lean = f.stagger > 0f ? -f.dir * 8f : 0f; var o = GUI.color; var m = GUI.matrix;
      if (f.fall >= 0f){ var pv = R(sx, baseY - 60, 0, 0).position; GUIUtility.RotateAroundPivot(-f.dir * f.fall * 120f, pv); }
      if (tex){ var r = R(sx - 60 + lean, baseY - 170, 120, 170); if (f.dir < 0){ r.x += r.width; r.width = -r.width; } GUI.DrawTexture(r, tex); }
      else {
        GUI.color = new Color(0.15f, 0.1f, 0.07f); GUI.DrawTexture(R(sx - 16 + lean, baseY - 52, 12, 52), white); GUI.DrawTexture(R(sx + 4 + lean, baseY - 52, 12, 52), white);   // legs
        GUI.color = coat; GUI.DrawTexture(R(sx - 20 + lean, baseY - 122, 40, 74), white);   // coat
        GUI.color = new Color(0.93f, 0.78f, 0.62f); GUI.DrawTexture(R(sx - 15 + lean, baseY - 152, 30, 30), disc);   // head
        GUI.color = new Color(0.12f, 0.08f, 0.06f); GUI.DrawTexture(R(sx - 22 + lean, baseY - 160, 44, 12), white);   // hat
      }
      // the cutlass: guard (upright at its height), wind-up (drawn back), strike (thrust at its height), idle (forward at mid)
      int h = f.phase > 0 ? f.h : f.guardH; float hy = new[]{ 140f, 100f, 58f }[h];
      float ang = f.block ? -90f * f.dir : f.phase == 1 ? (h == 0 ? -150f : h == 2 ? 150f : 180f) : f.phase == 2 ? (h == 0 ? -20f : h == 2 ? 20f : 0f) : (h == 0 ? -35f : h == 2 ? 35f : 0f);
      if (f.dir < 0) ang = 180f - ang;
      var hand = R(sx + f.dir * 18 + lean, baseY - hy, 0, 0).position;
      GUI.color = new Color(0.85f, 0.87f, 0.9f); GUIUtility.RotateAroundPivot(ang, hand); GUI.DrawTexture(new Rect(hand.x, hand.y - 2.5f * bgK, 64 * bgK, 5 * bgK), white);
      GUI.matrix = m; GUI.color = o;
      // telegraph and result text over the head
      if (f.phase == 1) Label(R(sx - 60, baseY - 200, 120, 26), HName[f.h] + "!", new GUIStyle(txt){ alignment = TextAnchor.MiddleCenter }, new Color(1f, 0.85f, 0.4f));
      else if (f.block) Label(R(sx - 60, baseY - 200, 120, 26), "guard " + HName[f.guardH].ToLower(), new GUIStyle(small){ alignment = TextAnchor.MiddleCenter }, Parchment);
      if (f.flashT > 0f) Label(R(sx - 80, baseY - 232, 160, 30), f.flash, new GUIStyle(txt){ alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(22 * bgK) }, f.flash == "HIT" ? new Color(1f, 0.45f, 0.35f) : new Color(0.75f, 0.95f, 1f));
    }

    /// <summary>The chest after a dig (Art Dev's chest if present, else a drawn one).</summary>
    void TreasureGUI(float s, float W, float H){
      if (Time.unscaledTime > treasureShowUntil) return;
      var r = new Rect(W / 2f - 110 * s, H / 2f - 160 * s, 220 * s, 170 * s);
      if (chestTex) GUI.DrawTexture(r, chestTex, ScaleMode.ScaleToFit);
      else { Box(new Rect(r.x + 20 * s, r.y + 70 * s, 180 * s, 90 * s), new Color(0.45f, 0.27f, 0.12f)); Box(new Rect(r.x + 20 * s, r.y + 40 * s, 180 * s, 30 * s), new Color(0.55f, 0.34f, 0.15f));
        Box(new Rect(r.x + 30 * s, r.y + 60 * s, 160 * s, 18 * s), new Color(1f, 0.82f, 0.25f)); Box(new Rect(r.x + 100 * s, r.y + 90 * s, 20 * s, 24 * s), new Color(0.85f, 0.7f, 0.3f)); }
      Label(new Rect(0, r.yMax + 6 * s, W, 34 * s), $"+{treasureGold} gold", new GUIStyle(big){ fontSize = Mathf.RoundToInt(30 * s) }, new Color(1f, 0.85f, 0.35f));
    }
  }
}

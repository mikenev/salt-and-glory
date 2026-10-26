using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace SaltGlory {
  /// <summary>Sailing the 1660 Caribbean with wind on a tilted top-down camera, ships met at sea (neutral traders, warships,
  /// pirates), broadside battles, docking at ports. Towns, title, saves and the world map are in GameTown.cs.
  /// Keyboard, gamepad and touch. HUD is IMGUI.</summary>
  public partial class Game : MonoBehaviour {
    public GameObject sloopModel;
    public Material playerMat, ballMat, softMat, seaMat;
    public GameObject[] classModels = new GameObject[5]; public Material[] classMats = new Material[5];   // M3 ship art by class (null = the sloop stands in)
    public Mesh ballMesh, chainMesh; public Material chainMat;   // chain shot: Art Dev's SG_Shot_Chain sprite on a flat spinning quad
    public Camera cam; public Transform sea;
    public TextAsset landMask, portsJson;   // LandMask.bytes (built from the chart) and SG_Map_Ports.json

    enum State { Title, Sea, Town, Lost, Duel }
    State state = State.Title; bool paused, mapOpen, logOpen; float stateT;
    Ship player, enemy;
    Career career = new();
    float windBase = 60f, windFrom = 60f;   // degrees the wind blows FROM (0 = from screen-top / +Z)
    Shot shot = Shot.Round, enemyShot = Shot.Round;
    const float CamPitch = 52f, MuzzleSpeed = 42f, Gravity = 9.81f, DockRange = 60f;
    Vector3 camFocus; float camDist = 46f;
    string banner = ""; float bannerUntil;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
    float nextLog; bool devFreezeEnemy, devGod; int devCls = -1;
#endif

    // ---------- ships met at sea ----------
    bool encActive, encHostile, encPirate, encWarship, encTreasure, encLegal, encFlee; Nation encNation; float encStart, nextEncounter, encCourse; Vector3 encPlayerStart;

    class Ball { public Transform t; public Vector3 v; public Ship from; public Shot type; public float life; public bool sprite; }
    readonly List<Ball> balls = new(); readonly Stack<Transform> ballPool = new(), chainPool = new();
    ParticleSystem smokePS, splashPS, flashPS;

    // ---------- setup ----------
    void Start(){
      Application.targetFrameRate = 60;
      World.ApplyPortsJson(portsJson ? portsJson.text : null);
      World.BuildMask(landMask ? landMask.bytes : null);
      if (seaMat){ seaMat.SetTexture("_LandTex", World.MaskTex); seaMat.SetVector("_LandRect", new Vector4(World.XMin, World.ZMin, World.W, World.H)); }
      player = MakeShip("Your sloop", playerMat); enemy = MakeShip("Ship", playerMat); ApplyClass(player, 0); career.FixUp(World.Ports.Count);
      player.Reset(World.WaterNear(World.Ports[0].pos, 30f), 150f, 0.6f); enemy.Hide();
      camFocus = player.Pos;
      smokePS = MakePS("Smoke", -0.05f); splashPS = MakePS("Splash", 1.2f); flashPS = MakePS("Flash", 0f);
      MakeHudTextures(); TownStart();
      Debug.Log("[SG] ready");
    }
    Ship MakeShip(string n, Material m){ var s = new GameObject(n).AddComponent<Ship>(); s.Init(sloopModel, m, n); return s; }
    /// <summary>Give a ship a class: Art Dev's model and material for it when present, else the sloop scaled and tinted (placeholder).</summary>
    void ApplyClass(Ship s, int c){
      var m = c < classModels.Length ? classModels[c] : null;
      s.SetClass(c, m ? m : sloopModel, c < classMats.Length && classMats[c] ? classMats[c] : playerMat, !m);
      if (s == player){ s.label = "Your " + s.C.name.ToLower(); s.SetTint(m ? Color.white : s.C.tint); }
    }
    ParticleSystem MakePS(string n, float gravity){
      var go = new GameObject(n); var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var m = ps.main; m.loop = false; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 800; m.gravityModifier = gravity;
      var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
      var col = ps.colorOverLifetime; col.enabled = true;
      var g = new Gradient(); g.SetKeys(new[]{ new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[]{ new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
      col.color = g;
      var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.5f));
      var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = softMat;
      ps.Play(); return ps;
    }
    static void Emit(ParticleSystem ps, Vector3 p, Vector3 v, float size, float life, Color c){
      ps.Emit(new ParticleSystem.EmitParams{ position = p, velocity = v, startSize = size, startLifetime = life, startColor = c, applyShapeToPosition = false }, 1);
    }
    /// <summary>Clear smoke, splashes, flashes, cannonballs and sink coroutines (restart, dock, load, wash ashore).</summary>
    void ClearFx(){
      StopAllCoroutines();
      foreach (var ps in new[]{ smokePS, splashPS, flashPS }) if (ps){ ps.Clear(); ps.Play(); }
      foreach (var b in balls) Recycle(b); balls.Clear();
    }
    void Banner(string s, float secs = 4f){ banner = s; bannerUntil = Time.unscaledTime + secs; }

    // ---------- input ----------
    enum Role { Steer, Slider, Port, Star, ShotBtn, Pause, Map, Dock }
    readonly Dictionary<int, (Role role, float startX)> ptrs = new();
    float touchRudder, lastTouch = -10f; bool touchSteering;
    float S => Screen.height / 720f;
    Rect RPort => new Rect(20 * S, Screen.height - 160 * S, 140 * S, 140 * S);
    Rect RStar => new Rect(Screen.width - 160 * S, Screen.height - 160 * S, 140 * S, 140 * S);
    Rect RShot => new Rect(Screen.width / 2f - 90 * S, Screen.height - 76 * S, 180 * S, 60 * S);
    Rect RSlider => new Rect(Screen.width - 112 * S, Screen.height - 480 * S, 72 * S, 290 * S);
    Rect RPause => new Rect(Screen.width / 2f - 90 * S, 12 * S, 80 * S, 50 * S);
    Rect RMap => new Rect(Screen.width / 2f + 10 * S, 12 * S, 80 * S, 50 * S);
    Rect RDock => new Rect(Screen.width / 2f - 170 * S, Screen.height - 150 * S, 340 * S, 60 * S);

    /// <summary>Touches and the mouse as one list of pointers (GUI coordinates: y down).</summary>
    IEnumerable<(int id, Vector2 p, bool began, bool down)> Pointers(){
      var ts = Touchscreen.current;
      if (ts != null) foreach (var t in ts.touches){
        bool down = t.press.isPressed, began = t.press.wasPressedThisFrame;
        if (!down && !t.press.wasReleasedThisFrame) continue;
        lastTouch = Time.unscaledTime;
        var p = t.position.ReadValue(); yield return (t.touchId.ReadValue(), new Vector2(p.x, Screen.height - p.y), began, down);
      }
      var ms = Mouse.current;   // ignore the mouse briefly after any touch (browsers can mirror touches as mouse clicks)
      if (ms != null && Time.unscaledTime - lastTouch > 0.6f && (ms.leftButton.isPressed || ms.leftButton.wasReleasedThisFrame)){
        var p = ms.position.ReadValue(); yield return (-1, new Vector2(p.x, Screen.height - p.y), ms.leftButton.wasPressedThisFrame, ms.leftButton.isPressed);
      }
    }
    bool AnyPress(){
      var kb = Keyboard.current; if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) return true;
      var gp = Gamepad.current; if (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame)) return true;
      foreach (var p in Pointers()) if (p.began) return true;
      return false;
    }

    void HandlePlayerInput(float dt){
      var kb = Keyboard.current; var gp = Gamepad.current;
      float steer = 0f, sailDelta = 0f;
      if (kb != null){
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) sailDelta += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) sailDelta -= 1f;
        if (kb.qKey.wasPressedThisFrame) Fire(player, -1, "keyboard");
        if (kb.eKey.wasPressedThisFrame) Fire(player, 1, "keyboard");
        if (kb.tabKey.wasPressedThisFrame) CycleShot("keyboard");
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Interact("keyboard");
      }
      if (gp != null){
        float x = gp.leftStick.x.ReadValue(); if (Mathf.Abs(x) > 0.2f) steer += x;
        sailDelta += gp.rightTrigger.ReadValue() - gp.leftTrigger.ReadValue();
        if (gp.leftShoulder.wasPressedThisFrame) Fire(player, -1, "gamepad");
        if (gp.rightShoulder.wasPressedThisFrame) Fire(player, 1, "gamepad");
        if (gp.buttonNorth.wasPressedThisFrame) CycleShot("gamepad");
        if (gp.buttonSouth.wasPressedThisFrame) Interact("gamepad");
      }
      // touch / mouse: fire buttons, shot button, sail slider, pause, map, dock prompt; drag anywhere else to steer
      touchSteering = false;
      var seen = new HashSet<int>();
      foreach (var (id, p, began, down) in Pointers()){
        seen.Add(id);
        if (began){
          Role r = RPort.Contains(p) ? Role.Port : RStar.Contains(p) ? Role.Star : RShot.Contains(p) ? Role.ShotBtn : RSlider.Contains(p) ? Role.Slider
            : RPause.Contains(p) ? Role.Pause : RMap.Contains(p) ? Role.Map : (Prompt() != null && RDock.Contains(p)) ? Role.Dock : Role.Steer;
          ptrs[id] = (r, p.x); string src = id < 0 ? "mouse" : "touch";
          if (r == Role.Port) Fire(player, -1, src); else if (r == Role.Star) Fire(player, 1, src);
          else if (r == Role.ShotBtn) CycleShot(src); else if (r == Role.Pause){ SetPaused(true, src); return; }
          else if (r == Role.Map){ SetMap(true, src); return; } else if (r == Role.Dock){ Interact(src); return; }
          else if (r == Role.Steer) Debug.Log($"[SG] {src} steer start");
        }
        if (!ptrs.TryGetValue(id, out var st)) continue;
        if (!down){ if (st.role == Role.Slider) Debug.Log($"[SG] sail slider {player.sail:F2}"); ptrs.Remove(id); continue; }
        if (st.role == Role.Slider){ var rs = RSlider; player.sail = Mathf.Clamp01(1f - (p.y - rs.y - 10 * S) / (rs.height - 20 * S)); }
        else if (st.role == Role.Steer){ touchSteering = true; touchRudder = Mathf.Clamp((p.x - st.startX) / (110f * S), -1f, 1f); }
      }
      foreach (var k in new List<int>(ptrs.Keys)) if (!seen.Contains(k)) ptrs.Remove(k);
      if (touchSteering) steer += touchRudder;
      player.rudder = Mathf.Clamp(steer, -1f, 1f);
      if (sailDelta != 0f) player.sail = Mathf.Clamp01(player.sail + sailDelta * 0.6f * dt);
    }
    void CycleShot(string src){ shot = (Shot)(((int)shot + 1) % 3); Debug.Log($"[SG] shot type {shot} by {src}"); }
    void SetPaused(bool on, string src){
      paused = on; Time.timeScale = on || mapOpen ? 0f : 1f; ptrs.Clear();
      Debug.Log($"[SG] {(on ? "paused" : "resumed")} by {src}");
    }
    void SetMap(bool on, string src){
      mapOpen = on; Time.timeScale = on || paused ? 0f : 1f; ptrs.Clear();
      if (on) GameAudio.I?.Play("map");
      Debug.Log($"[SG] map {(on ? "open" : "closed")} by {src}");
    }

    // ---------- loop ----------
    void Update(){
      var kb = Keyboard.current; var gp = Gamepad.current;
      DriftPrices(Time.deltaTime);
      if (state == State.Title || state == State.Town){ UpdateMenus(kb, gp); return; }
      bool pauseKey = (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) || (gp != null && gp.startButton.wasPressedThisFrame);
      bool mapKey = (kb != null && kb.mKey.wasPressedThisFrame) || (gp != null && gp.selectButton.wasPressedThisFrame);
      bool logKey = kb != null && kb.lKey.wasPressedThisFrame;
      if (mapOpen){ if (mapKey || pauseKey || AnyPress()) SetMap(false, "input"); return; }
      if (logOpen){ if (logKey || pauseKey || AnyPress()){ logOpen = false; Time.timeScale = 1f; Debug.Log("[SG] log closed"); } return; }
      if (paused){ if (pauseKey || AnyPress()) SetPaused(false, "input"); return; }
      if (state == State.Sea || state == State.Duel){
        if (pauseKey){ SetPaused(true, "key"); return; }
        if (mapKey && state == State.Sea){ SetMap(true, "key"); return; }
        if (logKey && state == State.Sea){ logOpen = true; Time.timeScale = 0f; ptrs.Clear(); Debug.Log("[SG] log open at sea"); return; }
      }
      float dt = Time.deltaTime; stateT += dt;
      windFrom = windBase + (Mathf.PerlinNoise(Time.time * 0.012f, 3.1f) - 0.5f) * 70f;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
      DevKeys(kb);
#endif
      if (state == State.Duel){ UpdateDuel(kb, gp, dt); return; }
      if (state == State.Sea){ HandlePlayerInput(dt); UpdateDig(dt); } else player.rudder = 0f;
      if (state == State.Lost && stateT > 2.5f && AnyPress()){ WashAshore(); return; }
      if (state != State.Sea && state != State.Lost) return;
      Encounters(); EnemyAI();
      Vector3 pp = player.Pos, ep = enemy.Pos;
      player.Tick(dt, windFrom); enemy.Tick(dt, windFrom);
      Aground(player, pp); Aground(enemy, ep);
      Separate();
      UpdateBalls(dt);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
      if (state == State.Sea && Time.time > nextLog){ nextLog = Time.time + 1f; QAStatus(); }   // per-second status: dev builds only
#endif
    }
    /// <summary>One status line on demand (the release smoke test calls this through SendMessage; dev builds log it every second).</summary>
    public void QAStatus(){
      if (!player) return;
      var el = player.transform.InverseTransformPoint(enemy.Pos);   // enemy bearing: side +1 starboard / -1 port, degrees off the beam
      NearPort(out var np);
      Debug.Log($"[SG] status state={state} speed={player.speed:F1} heading={player.heading:F0} windFrom={windFrom:F0} sail={player.sail:F2} eff={Ship.WindFactor(player.OffWind(windFrom)):F2} " +
        $"enc={(encActive ? 1 : 0)} hostile={(encHostile ? 1 : 0)} dist={(encActive ? Vector3.Distance(player.Pos, enemy.Pos) : -1f):F0} side={(el.x > 0f ? 1 : -1)} offBeam={Mathf.Atan2(el.z, Mathf.Abs(el.x)) * Mathf.Rad2Deg:F0} " +
        $"hull={player.hull:F0} sails={player.sails:F0} crew={player.crew:F0} enemyHull={(encActive ? enemy.hull : 0f):F0} gold={career.gold} hold={career.Hold} port={(np != null ? np.name : "-")} " +
        $"rep={career.rep[0]},{career.rep[1]},{career.rep[2]},{career.rep[3]} marque={career.marque} x={player.Pos.x:F0} z={player.Pos.z:F0} ex={enemy.Pos.x:F0} ez={enemy.Pos.z:F0} " +
        $"cls={player.cls} ecls={enemy.cls} ecrew={enemy.crew:F0} fame={career.fame} pieces={career.mapPieces} dig={(career.digActive ? 1 : 0)} digx={career.digX:F0} digz={career.digZ:F0} " +
        $"board={(CanBoard() ? 1 : 0)} duel={(state == State.Duel ? 1 : 0)} {DuelStatus()}");
    }
    void Aground(Ship s, Vector3 prev){
      if (s.gone || s.sinking) return;
      float bow = 6f * s.transform.localScale.x; bool blocked = false;
      if (World.Land(s.Pos + s.Fwd * bow) > 0.42f || World.Land(s.Pos) > 0.42f){
        s.transform.position = prev; blocked = true; if (s.speed > 2f && s == player) Debug.Log("[SG] aground: turn away from the shore"); s.speed = Mathf.Min(s.speed, 0.6f);
      }
      // push-out: when blocked, or when the hull sits in the shore band (a narrow inlet, a turn against the coast), ease the ship
      // down the slope of the land mask into open water so it can never be pinned (M2 QA: the inlet north of Port Royal)
      float here = World.Land(s.Pos);
      if (blocked || here > 0.2f){
        const float e = 6f; var p = s.Pos;
        var g = new Vector3(World.Land(p + Vector3.right * e) - World.Land(p - Vector3.right * e), 0f, World.Land(p + Vector3.forward * e) - World.Land(p - Vector3.forward * e));
        if (g.sqrMagnitude > 1e-6f) s.transform.position -= g.normalized * (1f + here * 3f) * Time.deltaTime * 2.5f;
      }
    }

    // ---------- ships met at sea ----------
    void Encounters(){
      if (state != State.Sea) return;
      if (encActive){
        float d = Vector3.Distance(player.Pos, enemy.Pos);
        if (enemy.gone){ encActive = false; GameAudio.I?.SetBattle(false); nextEncounter = Time.time + Random.Range(30f, 50f); return; }
        if (!enemy.sinking && d > 290f){
          if (encHostile) PatchUp("escaped");
          GameAudio.I?.SetBattle(false); Debug.Log($"[SG] lost sight of {enemy.label}"); Banner($"The {enemy.label} is lost from sight"); encActive = false; enemy.Hide(); nextEncounter = Time.time + Random.Range(30f, 50f);
        }
        return;
      }
      if (Time.time < nextEncounter || NearPort(out _, 110f)) return;
      // a sail on the horizon: mostly ships of the nearest port's nation, sometimes another power's, sometimes pirates
      var near = NearestPort(player.Pos); float r = Random.value;
      encPirate = r < 0.15f; encNation = r < 0.6f ? near.nation : (Nation)Random.Range(0, 4); float role = Random.value;
      encTreasure = !encPirate && role < 0.12f; if (encTreasure) encNation = Nation.Spain;   // the treasure fleets are Spanish
      encWarship = !encPirate && !encTreasure && role < 0.45f;
      Vector3 at = Vector3.zero; bool found = false;
      for (int i = 0; i < 16 && !found; i++){ at = player.Pos + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * 150f; found = World.Land(at) < 0.05f; }
      if (!found){ nextEncounter = Time.time + 8f; return; }
      SpawnEncounter(at, Mathf.Atan2(player.Pos.x - at.x, player.Pos.z - at.z) * Mathf.Rad2Deg + Random.Range(-50f, 50f));
    }
    /// <summary>Ship class by role: merchants sail fluyts (some brigantines), warships frigates, treasure fleets galleons,
    /// pirates sloops or brigantines.</summary>
    int RoleClass() => encTreasure ? ShipClass.Galleon : encWarship ? ShipClass.Frigate : encPirate ? (Random.value < 0.55f ? ShipClass.Sloop : ShipClass.Brigantine)
      : Random.value < 0.75f ? ShipClass.Fluyt : ShipClass.Brigantine;
    void SpawnEncounter(Vector3 at, float hdg, int cls = -1){
      ApplyClass(enemy, cls >= 0 ? cls : RoleClass());
      enemy.Reset(at, hdg, 0.75f); enemy.maxSpeed *= encWarship || encPirate ? 0.95f : 0.85f; enemy.reloadBase *= encWarship ? 1.35f : 1.6f;   // AI ships a little slower and slower to load
      string cn = enemy.C.name.ToLower();
      enemy.label = encPirate ? $"Pirate {cn}" : encTreasure ? "Spanish treasure galleon" : $"{World.Adj[(int)encNation]} {(encWarship ? "warship" : "merchant")} {cn}";
      var placeholder = classModels[enemy.cls] == null;
      enemy.SetTint((encPirate ? new Color(0.5f, 0.47f, 0.45f) : Color.Lerp(Color.white, World.NationColor[(int)encNation], 0.4f)) * (placeholder ? enemy.C.tint : Color.white));
      encActive = true; encHostile = false; encLegal = true; encFlee = !encWarship && !encPirate;
      encCourse = hdg;
      bool war = career.marque >= 0 && World.AtWar((Nation)career.marque, encNation);
      Debug.Log($"[SG] sail ho: {enemy.label} class={enemy.C.name} dist={Vector3.Distance(at, player.Pos):F0}");
      if (encPirate || (encWarship && (career.IsHostile(encNation) || war))) BeginBattle(true);
      else Banner($"Sail ho! A {enemy.label}. Fire on her to attack" + (war ? " (legal under your letter of marque)" : ""));
    }
    /// <summary>The encounter turns into a fight: either they attack us (hostile on sight) or we attacked them.</summary>
    void BeginBattle(bool theyAttack){
      encHostile = true; encStart = Time.time; encPlayerStart = player.Pos; GameAudio.I?.SetBattle(true);
      if (theyAttack){ Banner($"The {enemy.label} runs out her guns!"); Debug.Log($"[SG] battle started at sea by {enemy.label}"); return; }
      // we fired first: legal against the enemies of our patron, piracy otherwise
      bool legal = encPirate || (career.marque >= 0 && World.AtWar((Nation)career.marque, encNation)); encLegal = legal;
      if (!encPirate){
        if (legal){ career.AddRep(encNation, -10); career.AddRep((Nation)career.marque, 4); }
        else { career.AddRep(encNation, -25); for (int i = 0; i < 4; i++) if (i != (int)encNation) career.AddRep((Nation)i, -3); }
      }
      Banner(legal ? $"You attack the {enemy.label}" : $"Piracy! {World.NationNames[(int)encNation]} will not forget this");
      Debug.Log($"[SG] battle started at sea by player vs {enemy.label} legal={legal} rep={string.Join(",", career.rep)}");
    }
    void PatchUp(string why){   // between battles the crew patches the hull and sails (partly; the shipwright does the rest)
      player.hull = Mathf.Min(100f, player.hull + 20f); player.sails = Mathf.Min(100f, player.sails + 25f);
      Debug.Log($"[SG] patched up after battle ({why}) hull={player.hull:F0} sails={player.sails:F0}");
    }

    void EnemyAI(){
      var e = enemy; if (!encActive || e.sinking || e.gone) return;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
      if (devFreezeEnemy){ e.rudder = 0f; e.sail = 0f; return; }
#endif
      Vector3 d = player.Pos - e.Pos; d.y = 0f; float dist = d.magnitude, bearing = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, desired;
      if (player.sinking || state != State.Sea){ e.rudder = 0f; e.sail = 0.4f; return; }
      if (!encHostile){   // a neutral holds her course, bearing away from any shore ahead
        if (World.Land(e.Pos + e.Fwd * 30f) > 0.2f) encCourse = e.heading + 70f;
        desired = encCourse; e.sail = 0.7f;
      } else if (encFlee && !e.mastDown && e.sails > 30f) desired = bearing + 180f;   // merchants and treasure ships run for it
      else if (dist > 60f) desired = bearing;
      else {
        // run parallel-ish to bring a broadside to bear, closing in when far and opening out when close
        float a1 = bearing + 90f, a2 = bearing - 90f;
        desired = Mathf.Abs(Mathf.DeltaAngle(e.heading, a1)) < Mathf.Abs(Mathf.DeltaAngle(e.heading, a2)) ? a1 : a2;
        float corr = Mathf.Clamp((dist - 32f) * 1.5f, -30f, 30f);
        desired += Mathf.Sign(Mathf.DeltaAngle(desired, bearing)) * corr;
      }
      float off = Mathf.DeltaAngle(windFrom, desired);   // never point into the wind
      if (Mathf.Abs(off) < 42f) desired = windFrom + (off >= 0f ? 42f : -42f);
      e.rudder = Mathf.Clamp(Mathf.DeltaAngle(e.heading, desired) / 25f, -1f, 1f);
      if (!encHostile) return;
      e.sail = encFlee ? 1f : dist > 60f ? 0.9f : 0.65f;
      // first volley waits ~25 s, or until she is in range AND at least 15 s have passed (M2 QA)
      float since = Time.time - encStart; bool mayFire = since > 25f || (dist < 62f && since >= 15f);
      if (mayFire && dist < 62f){
        var local = e.transform.InverseTransformPoint(player.Pos);
        float offBeam = Mathf.Abs(Mathf.Atan2(local.z, Mathf.Abs(local.x)) * Mathf.Rad2Deg);
        if (offBeam < 22f){
          int side = local.x > 0f ? 1 : -1;
          if (Time.time >= (side < 0 ? e.portReady : e.starReady))   // pick the shot for this volley: grape up close, chain at full sails, else round
            enemyShot = dist < 30f && player.crew > 30f ? Shot.Grape : player.sails > 55f && Random.value < 0.35f ? Shot.Chain : Shot.Round;
          Fire(e, side, "ai");
        }
      }
    }
    void Separate(){
      if (enemy.gone) return;
      Vector3 d = enemy.Pos - player.Pos; d.y = 0f; float m = d.magnitude;
      float sep = 5.5f * (player.transform.localScale.x + enemy.transform.localScale.x);
      if (m < sep && m > 0.01f){ var push = d / m * (sep - m) * 0.5f; enemy.transform.position += push; player.transform.position -= push; player.speed *= 0.97f; enemy.speed *= 0.97f; }
    }

    // ---------- guns ----------
    void Fire(Ship s, int side, string src){
      if (s.sinking || s.gone || state != State.Sea || paused || mapOpen) return;
      bool port = side < 0; float ready = port ? s.portReady : s.starReady;
      if (Time.time < ready) return;
      if (port) s.portReady = Time.time + s.ReloadTime; else s.starReady = Time.time + s.ReloadTime;
      Shot type = s == player ? shot : enemyShot;
      Debug.Log($"[SG] fire {(port ? "port" : "starboard")} by {(s == player ? "player" : "enemy")} src={src} shot={type}");
      StartCoroutine(Broadside(s, port ? s.portGuns : s.starGuns, side, type));
    }
    IEnumerator Broadside(Ship s, List<Vector3> guns, int side, Shot type){
      foreach (var g in guns){
        if (s.sinking || s.gone) yield break;
        var mp = s.transform.TransformPoint(g) + s.Right * side * 0.6f; var dir = s.Right * side;
        float spread = s == player ? 2.5f : 8f;
        dir = Quaternion.Euler(0f, Random.Range(-spread, spread), 0f) * dir;
        var v = dir * MuzzleSpeed + s.Fwd * s.speed + Vector3.up * Random.Range(5.4f, 6.2f);
        // chain shot: the two-ball chain sprite spinning flat; grape: small balls; round: the plain ball
        bool sprite = type == Shot.Chain && chainMesh && chainMat;
        var t = sprite ? (chainPool.Count > 0 ? chainPool.Pop() : NewBall(true)) : (ballPool.Count > 0 ? ballPool.Pop() : NewBall(false));
        t.gameObject.SetActive(true); t.position = mp; t.rotation = Quaternion.LookRotation(dir);
        t.localScale = sprite ? Vector3.one : type == Shot.Chain ? new Vector3(1.7f, 0.34f, 0.34f) : Vector3.one * (type == Shot.Grape ? 0.3f : 0.5f);
        balls.Add(new Ball{ t = t, v = v, from = s, type = type, life = 3f, sprite = sprite });
        float vol = s == player ? 1f : Mathf.Clamp01(1.2f - Vector3.Distance(s.Pos, player.Pos) / 140f);
        GameAudio.I?.Play(s == player ? "cannon" : "enemyCannon", vol);
        Emit(flashPS, mp, dir * 6f, 1.6f, 0.15f, new Color(1f, 0.7f, 0.3f, 0.95f));
        for (int i = 0; i < 4; i++) Emit(smokePS, mp + dir * i * 0.8f, dir * Random.Range(2f, 5f) + Vector3.up * 0.6f + Random.insideUnitSphere * 0.6f, Random.Range(2.2f, 3.6f), Random.Range(1.8f, 2.8f), new Color(0.93f, 0.9f, 0.85f, 0.85f));
        yield return new WaitForSeconds(0.09f);
      }
    }
    Transform NewBall(bool chain){
      var go = new GameObject(chain ? "ChainShot" : "Ball"); go.AddComponent<MeshFilter>().sharedMesh = chain ? chainMesh : ballMesh; go.AddComponent<MeshRenderer>().sharedMaterial = chain ? chainMat : ballMat;
      return go.transform;
    }
    void ClearBalls(Ship from){ for (int i = balls.Count - 1; i >= 0; i--) if (balls[i].from == from){ Recycle(balls[i]); balls.RemoveAt(i); } }
    void Recycle(Ball b){ b.t.gameObject.SetActive(false); (b.sprite ? chainPool : ballPool).Push(b.t); }
    void UpdateBalls(float dt){
      for (int i = balls.Count - 1; i >= 0; i--){
        var b = balls[i]; b.v.y -= Gravity * dt; b.t.position += b.v * dt; b.life -= dt;
        if (b.type == Shot.Chain) b.t.Rotate(0f, 1100f * dt, 0f, Space.World);
        var target = b.from == player ? enemy : player; var p = b.t.position;
        if (!target.sinking && !target.gone){
          var l = target.transform.InverseTransformPoint(p);
          if (Mathf.Abs(l.x) < 2.9f && Mathf.Abs(l.z) < 7.2f && l.y < 4.5f && l.y > -1.5f){ Hit(target, b.type, p); Recycle(b); balls.RemoveAt(i); continue; }
        }
        if (p.y < 0f || b.life <= 0f){
          if (p.y < 0.5f){
            for (int k = 0; k < 5; k++) Emit(splashPS, new Vector3(p.x, 0.2f, p.z), new Vector3(Random.Range(-1f, 1f), Random.Range(4f, 7f), Random.Range(-1f, 1f)), Random.Range(0.7f, 1.3f), 0.9f, new Color(0.95f, 0.98f, 1f, 0.9f));
            GameAudio.I?.Play("splash", Mathf.Clamp01(1.2f - Vector3.Distance(p, player.Pos) / 120f));
          }
          Recycle(b); balls.RemoveAt(i);
        }
      }
    }
    void Hit(Ship s, Shot type, Vector3 p){
#if DEVELOPMENT_BUILD || UNITY_EDITOR
      if (s == player && devGod) return;
#endif
      if (s == enemy && encActive && !encHostile) BeginBattle(false);   // firing on a neutral is an attack
      var from = s == player ? enemy : player;
      float k = (s == player ? 0.7f : 1f) * from.C.gunK, hk = k / s.C.hullK, ck = k / Mathf.Sqrt(s.C.hullK) * s.Complement / 100f;   // classes: guns hit harder, hulls take less
      switch (type){
        case Shot.Round: s.hull -= 8f * hk * (s == player ? 0.7f : 1f); s.crew -= 1.5f * ck; break;   // enemy round shot ~30% lighter (M1 QA)
        case Shot.Chain: s.sails -= 13f * k / Mathf.Sqrt(s.C.hullK); s.hull -= 2f * hk; break;
        case Shot.Grape: s.crew -= 9f * ck; s.hull -= 1.5f * hk; break;
      }
      if (!s.mastDown && s.sails < 30f){ s.mastDown = true; GameAudio.I?.Play("mast"); Debug.Log($"[SG] mast down on {(s == player ? "player" : "enemy")}"); if (s == enemy) Banner($"Her mainmast goes by the board! The {enemy.label} is slowed"); }
      s.hull = Mathf.Max(0f, s.hull); s.sails = Mathf.Max(0f, s.sails); s.crew = Mathf.Max(0f, s.crew);
      GameAudio.I?.Play("hit", s == player ? 1f : Mathf.Clamp01(1.2f - Vector3.Distance(s.Pos, player.Pos) / 140f));
      for (int i = 0; i < 4; i++) Emit(splashPS, p, Random.insideUnitSphere * 4f + Vector3.up * 3f, Random.Range(0.35f, 0.6f), 0.8f, new Color(0.45f, 0.3f, 0.18f, 1f));   // splinters
      for (int i = 0; i < 2; i++) Emit(smokePS, p, Vector3.up * 1.5f + Random.insideUnitSphere, Random.Range(2f, 3f), 1.8f, new Color(0.35f, 0.32f, 0.3f, 0.7f));
      Debug.Log($"[SG] hit {(s == player ? "player" : "enemy")} shot={type} hull={s.hull:F0} sails={s.sails:F0} crew={s.crew:F0}");
      if (s.hull <= 0f) Sink(s);
    }
    void Sink(Ship s){
      s.sinking = true; s.rudder = 0f;
      if (s == enemy){
        ClearBalls(enemy);   // her shots still in the air fall harmlessly (M2 QA)
        career.victories++; career.fame += 5; GameAudio.I?.SetBattle(false); GameAudio.I?.Play("cheer");
        int gold = encWarship ? Random.Range(120, 200) : encPirate ? Random.Range(220, 320) : Random.Range(180, 320); career.gold += gold;
        string loot = "";
        if (!encWarship){ int g = Random.Range(0, 5), n = Mathf.Min(Random.Range(4, 11), career.HoldMax - career.Hold); if (n > 0){ career.cargo[g] += n; loot = $", {n} {World.Goods[g]}"; } }
        if (!encPirate && encLegal && career.marque >= 0 && World.AtWar((Nation)career.marque, encNation)){ career.AddRep((Nation)career.marque, 6); career.Serve(6); }
        if (!encPirate && career.missionTarget == (int)encNation && !career.missionDone){ career.missionDone = true; loot += ". Mission done: report to the governor"; Debug.Log("[SG] mission target sunk"); }
        PatchUp("victory"); GameAudio.I?.Play("coins");
        Banner($"Prize! The {enemy.label} goes down. +{gold} gold{loot}", 6f);
        Debug.Log($"[SG] enemy sunk victories={career.victories} gold={career.gold} hold={career.Hold}");
      } else { state = State.Lost; stateT = 0f; GameAudio.I?.SetBattle(false); Debug.Log("[SG] player sunk"); }
      StartCoroutine(SinkSmoke(s));
    }
    IEnumerator SinkSmoke(Ship s){
      for (float t = 0; t < 4f && !s.gone; t += 0.15f){
        Emit(smokePS, s.Pos + Vector3.up * 2f + Random.insideUnitSphere * 2f, Vector3.up * 2f, Random.Range(3f, 4.5f), 2.5f, new Color(0.3f, 0.28f, 0.27f, 0.75f));
        if (t < 2f) Emit(splashPS, s.Pos + Random.insideUnitSphere * 4f, Vector3.up * 5f, 1.2f, 1f, new Color(0.95f, 0.98f, 1f, 0.9f));
        yield return new WaitForSeconds(0.15f);
      }
    }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    void DevKeys(Keyboard kb){
      if (kb == null || state != State.Sea) return;
      if (kb.f1Key.wasPressedThisFrame){   // a hostile Spanish warship alongside to starboard, 30 m off, same heading
        encPirate = false; encWarship = true; encNation = Nation.Spain; SpawnEncounter(player.Pos + player.Right * 30f, player.heading);
        if (!encHostile) BeginBattle(true);
        enemy.speed = player.speed; Debug.Log("[SG] dev: enemy alongside");
      }
      if (kb.f2Key.wasPressedThisFrame){ devFreezeEnemy = !devFreezeEnemy; Debug.Log($"[SG] dev: freeze enemy {devFreezeEnemy}"); }
      if (kb.f3Key.wasPressedThisFrame){ devGod = !devGod; Debug.Log($"[SG] dev: god {devGod}"); }
      if (kb.f4Key.wasPressedThisFrame){ nextEncounter = 0f; Debug.Log("[SG] dev: encounter now"); }
      if (kb.f5Key.wasPressedThisFrame){   // the next ship class alongside to starboard (neutral)
        devCls = (devCls + 1) % ShipClass.All.Length; encPirate = encWarship = encTreasure = false; encNation = Nation.France;
        SpawnEncounter(player.Pos + player.Right * 40f, player.heading, devCls); Debug.Log($"[SG] dev: class {ShipClass.All[devCls].name} alongside");
      }
      if (kb.f6Key.wasPressedThisFrame){ GainPiece("dev"); }
    }
#endif

    // ---------- camera ----------
    void LateUpdate(){
      if (!player) return;
      Vector3 focus = player.Pos; float dist = 46f;
      if (!enemy.gone){
        float sep = Vector3.Distance(player.Pos, enemy.Pos);
        if (sep < 80f){ focus = Vector3.Lerp(player.Pos, enemy.Pos, 0.5f); dist = Mathf.Clamp(sep * 1.45f + 18f, 46f, 125f); }   // frame both ships in a fight
      }
      float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 2.5f);
      camFocus = Vector3.Lerp(camFocus, focus + player.Fwd * player.speed * 0.6f, k); camDist = Mathf.Lerp(camDist, dist, k * 0.5f);
      var rot = Quaternion.Euler(CamPitch, 0f, 0f);
      cam.transform.SetPositionAndRotation(camFocus - rot * Vector3.forward * camDist, rot);
      if (sea) sea.position = new Vector3(Mathf.Round(camFocus.x / 10f) * 10f, 0f, Mathf.Round(camFocus.z / 10f) * 10f);   // world-space UVs, so the snap is invisible
    }

    // ---------- HUD (placeholder IMGUI shapes) ----------
    Texture2D white, disc, ring, arrow; GUIStyle txt, big, small;
    void MakeHudTextures(){
      white = Texture2D.whiteTexture;
      disc = Proc(64, (x, y) => { float d = new Vector2(x - 31.5f, y - 31.5f).magnitude; return Mathf.Clamp01(31f - d); });
      ring = Proc(128, (x, y) => { float d = new Vector2(x - 63.5f, y - 63.5f).magnitude; return Mathf.Clamp01(1f - Mathf.Abs(d - 58f) / 3f); });
      arrow = Proc(64, (x, y) => {   // points up (towards +y in texture = top on screen)
        float cx = Mathf.Abs(x - 31.5f); float head = y > 38 ? Mathf.Clamp01((63 - y) * 0.55f - cx) : 0f; float shaft = y >= 4 && y <= 40 && cx < 5f ? 1f : 0f;
        return Mathf.Max(head, shaft);
      });
    }
    static Texture2D Proc(int n, System.Func<int, int, float> a){
      var t = new Texture2D(n, n, TextureFormat.RGBA32, false){ wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
      var px = new Color32[n * n]; for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a(x, y)) * 255));
      t.SetPixels32(px); t.Apply(); return t;
    }
    static readonly Color Parchment = new(0.96f, 0.9f, 0.76f), Ink = new(0.17f, 0.12f, 0.08f), Panel = new(0.12f, 0.09f, 0.06f, 0.6f);
    void Box(Rect r, Color c){ var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o; }
    void Bar(float x, float y, float w, string label, float v, Color c){
      float s = S; GUI.Label(new Rect(x, y, 80 * s, 22 * s), label, small);
      Box(new Rect(x + 70 * s, y + 5 * s, w, 12 * s), new Color(0, 0, 0, 0.45f)); Box(new Rect(x + 70 * s, y + 5 * s, w * Mathf.Clamp01(v / 100f), 12 * s), c);
    }
    void Label(Rect r, string t, GUIStyle st, Color c){ var o = GUI.contentColor; GUI.contentColor = c; GUI.Label(r, t, st); GUI.contentColor = o; }
    void EdgeArrow(Vector3 to, Color c, float s, float W, float H){
      var m = GUI.matrix; var o = GUI.color;
      var dir = to - player.Pos; var sp = new Vector2(dir.x, -dir.z).normalized; var ctr = new Vector2(W / 2f, H / 2f);
      float r = Mathf.Min(W, H) * 0.42f; var p = ctr + sp * r; GUI.color = c;
      GUIUtility.RotateAroundPivot(Mathf.Atan2(sp.x, -sp.y) * Mathf.Rad2Deg, p); GUI.DrawTexture(new Rect(p.x - 14 * s, p.y - 20 * s, 28 * s, 40 * s), arrow); GUI.matrix = m; GUI.color = o;
    }
    void OnGUI(){
      if (!player || white == null) return;
      float s = S, W = Screen.width, H = Screen.height;
      txt ??= new GUIStyle(GUI.skin.label){ fontStyle = FontStyle.Bold }; big ??= new GUIStyle(GUI.skin.label){ alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold }; small ??= new GUIStyle(GUI.skin.label);
      txt.fontSize = Mathf.RoundToInt(18 * s); big.fontSize = Mathf.RoundToInt(44 * s); small.fontSize = Mathf.RoundToInt(15 * s);
      txt.normal.textColor = small.normal.textColor = big.normal.textColor = Parchment;
      if (state == State.Title || state == State.Town){ MenusGUI(s, W, H); return; }

      // port names and the met ship, labelled in the world
      foreach (var pt in World.Ports){
        if (Vector3.Distance(pt.pos, camFocus) > 260f) continue;
        var sp = cam.WorldToScreenPoint(pt.pos); if (sp.z < 0) continue; var g = new Vector2(sp.x, H - sp.y);
        Box(new Rect(g.x - 7 * s, g.y - 7 * s, 14 * s, 14 * s), World.NationColor[(int)pt.nation]);
        Label(new Rect(g.x - 120 * s, g.y + 8 * s, 240 * s, 24 * s), pt.name + (career.IsHostile(pt.nation) ? " (hostile)" : ""), new GUIStyle(txt){ alignment = TextAnchor.UpperCenter }, Parchment);
      }
      if (state == State.Duel){ DuelGUI(s, W, H); return; }
      if (encActive && !enemy.gone){
        var sp = cam.WorldToScreenPoint(enemy.Pos + Vector3.up * 14f * enemy.transform.localScale.x);
        if (sp.z > 0) Label(new Rect(sp.x - 150 * s, H - sp.y - 24 * s, 300 * s, 24 * s), enemy.label, new GUIStyle(small){ alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold }, encHostile ? new Color(1f, 0.6f, 0.5f) : Parchment);
      }
      // wind compass (top-left): arrow points where the wind blows
      var cr = new Rect(16 * s, 16 * s, 110 * s, 110 * s);
      var o = GUI.color; GUI.color = new Color(0.12f, 0.09f, 0.06f, 0.55f); GUI.DrawTexture(cr, disc); GUI.color = Parchment; GUI.DrawTexture(cr, ring);
      var m = GUI.matrix; GUIUtility.RotateAroundPivot(windFrom + 180f, cr.center); GUI.color = new Color(0.55f, 0.8f, 0.95f); GUI.DrawTexture(new Rect(cr.center.x - 22 * s, cr.center.y - 40 * s, 44 * s, 80 * s), arrow); GUI.matrix = m;
      GUIUtility.RotateAroundPivot(player.heading, cr.center); GUI.color = new Color(0.95f, 0.75f, 0.35f); GUI.DrawTexture(new Rect(cr.center.x - 5 * s, cr.y - 2 * s, 10 * s, 18 * s), white); GUI.matrix = m;
      GUI.color = o;
      Label(new Rect(cr.x, cr.yMax + 2 * s, 260 * s, 24 * s), "WIND", small, Parchment);
      float eff = Ship.WindFactor(player.OffWind(windFrom));
      Box(new Rect(136 * s, 16 * s, 250 * s, 130 * s), Panel);
      Label(new Rect(146 * s, 20 * s, 240 * s, 26 * s), $"{Ship.PointOfSail(player.OffWind(windFrom))}  {eff * 100f:F0}%", txt, Parchment);
      Label(new Rect(146 * s, 44 * s, 240 * s, 24 * s), $"Speed {player.speed * 1.2f:F1} kn   Sails {player.sail * 100f:F0}%", small, Parchment);
      Bar(146 * s, 66 * s, 150 * s, "Hull", player.hull, new Color(0.85f, 0.55f, 0.3f));
      Bar(146 * s, 84 * s, 150 * s, "Sails", player.sails, new Color(0.92f, 0.88f, 0.75f));
      Bar(146 * s, 102 * s, 150 * s, "Crew", player.crew, new Color(0.6f, 0.8f, 0.55f));
      Label(new Rect(146 * s, 120 * s, 240 * s, 24 * s), $"Gold {career.gold}   Hold {career.Hold}/{career.HoldMax}   Fame {career.fame}", small, Parchment);
      if (encActive && !enemy.gone){
        float d = Vector3.Distance(player.Pos, enemy.Pos);
        Box(new Rect(W - 296 * s, 16 * s, 280 * s, 92 * s), Panel);
        Label(new Rect(W - 286 * s, 20 * s, 270 * s, 26 * s), $"{enemy.label}  {d:F0} m", txt, encHostile ? new Color(1f, 0.75f, 0.65f) : Parchment);
        Bar(W - 286 * s, 48 * s, 150 * s, "Hull", enemy.hull, new Color(0.85f, 0.4f, 0.3f));
        Bar(W - 286 * s, 66 * s, 150 * s, "Sails", enemy.sails, new Color(0.92f, 0.88f, 0.75f));
        Bar(W - 286 * s, 84 * s, 150 * s, "Crew", enemy.crew, new Color(0.6f, 0.8f, 0.55f));
        if (d > 80f) EdgeArrow(enemy.Pos, new Color(1f, 0.55f, 0.45f, 0.9f), s, W, H);
      }
      // nearest port: pointer when far, dock prompt (Enter / A / tap) when close
      var near = NearestPort(player.Pos); float nd = Vector3.Distance(player.Pos, near.pos);
      if (nd > 140f) EdgeArrow(near.pos, new Color(0.96f, 0.9f, 0.76f, 0.75f), s, W, H);
      if (career.digActive){   // the dig site from the four map pieces
        var dg = new Vector3(career.digX, 0f, career.digZ); float dd = Vector3.Distance(player.Pos, dg);
        if (dd > 90f) EdgeArrow(dg, new Color(0.95f, 0.75f, 0.2f, 0.9f), s, W, H);
        else { var sp = cam.WorldToScreenPoint(dg); if (sp.z > 0){ var g = new Vector2(sp.x, H - sp.y); if (digMarkTex) GUI.DrawTexture(new Rect(g.x - 24 * s, g.y - 24 * s, 48 * s, 48 * s), digMarkTex); else Label(new Rect(g.x - 30 * s, g.y - 24 * s, 60 * s, 48 * s), "X", new GUIStyle(big){ fontSize = Mathf.RoundToInt(40 * s) }, new Color(0.75f, 0.1f, 0.06f)); } }
      }
      string prompt = Prompt();
      if (prompt != null){
        var rd = RDock; Box(rd, new Color(0.25f, 0.17f, 0.08f, 0.85f));
        Label(rd, prompt + "  (Enter / A / tap)", new GUIStyle(txt){ alignment = TextAnchor.MiddleCenter }, Parchment);
      }
      // the nearest port sits under the pause and map buttons, clear of the ship (M2 QA)
      if (!NearPort(out _)) Label(new Rect(W / 2f - 250 * s, 66 * s, 500 * s, 24 * s), $"Nearest port: {near.name} ({World.Adj[(int)near.nation]}) {nd:F0} m   ·   M map   ·   L log" + (career.digActive ? "   ·   dig site marked" : ""), new GUIStyle(small){ alignment = TextAnchor.MiddleCenter }, Parchment);
      // touch controls (also clickable)
      DrawFire(RPort, "PORT\n(Q)", player.portReady); DrawFire(RStar, "STARBOARD\n(E)", player.starReady);
      var rs = RShot; Box(rs, Panel); Label(rs, $"Shot: {shot}  (Tab)", new GUIStyle(txt){ alignment = TextAnchor.MiddleCenter }, Parchment);
      var sl = RSlider; Box(sl, Panel); Box(new Rect(sl.center.x - 3 * s, sl.y + 10 * s, 6 * s, sl.height - 20 * s), new Color(1, 1, 1, 0.3f));
      float ky = sl.y + 10 * s + (1f - player.sail) * (sl.height - 20 * s); Box(new Rect(sl.x + 6 * s, ky - 9 * s, sl.width - 12 * s, 18 * s), Parchment);
      Label(new Rect(sl.x - 10 * s, sl.y - 24 * s, sl.width + 20 * s, 22 * s), "SAILS", new GUIStyle(small){ alignment = TextAnchor.MiddleCenter }, Parchment);
      var rp = RPause; Box(rp, Panel); Label(rp, "II", new GUIStyle(txt){ alignment = TextAnchor.MiddleCenter }, Parchment);
      var rm = RMap; Box(rm, Panel); Label(rm, "MAP", new GUIStyle(txt){ alignment = TextAnchor.MiddleCenter }, Parchment);

      TreasureGUI(s, W, H);
      if (Time.unscaledTime < bannerUntil && !mapOpen){
        Box(new Rect(W / 2f - 380 * s, 150 * s, 760 * s, 40 * s), new Color(0.05f, 0.04f, 0.03f, 0.6f));
        Label(new Rect(W / 2f - 380 * s, 150 * s, 760 * s, 40 * s), banner, new GUIStyle(txt){ alignment = TextAnchor.MiddleCenter, wordWrap = true }, Parchment);
      }
      if (mapOpen){ MapGUI(s, W, H); return; }
      if (logOpen){ Styles(s); UseUiFit(W, H, Mathf.Min(W / 1280f, H / 720f)); LogGUI(); return; }
      if (paused || state == State.Lost){
        string head = paused ? "Paused" : $"Your {player.C.name.ToLower()} went down";
        string sub = paused ? "Esc, Start, Enter or tap to resume" : stateT > 2.5f ? "Enter or tap: wash ashore at a friendly port" : "";
        Box(new Rect(0, H * 0.36f, W, 120 * s), new Color(0.05f, 0.04f, 0.03f, 0.6f));
        Label(new Rect(0, H * 0.36f + 8 * s, W, 60 * s), head, big, Parchment);
        Label(new Rect(0, H * 0.36f + 70 * s, W, 40 * s), sub, new GUIStyle(txt){ alignment = TextAnchor.MiddleCenter }, Parchment);
      }
    }
    void DrawFire(Rect r, string label, float ready){
      float left = ready - Time.time; Box(r, Panel);
      if (left > 0f){ float f = Mathf.Clamp01(left / player.ReloadTime); Box(new Rect(r.x, r.y + r.height * (1f - f), r.width, r.height * f), new Color(0.5f, 0.15f, 0.1f, 0.45f)); }
      Label(r, left > 0f ? $"{label}\n{left:F1}s" : label, new GUIStyle(txt){ alignment = TextAnchor.MiddleCenter }, left > 0f ? new Color(0.8f, 0.7f, 0.6f) : Parchment);
    }
  }
}

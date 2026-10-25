using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace SaltGlory {
  /// <summary>M1: open-sea sailing with wind on a tilted top-down camera, and a broadside battle against one enemy sloop.
  /// Keyboard, gamepad and touch. HUD is IMGUI with plain shapes (no UI art yet).</summary>
  public class Game : MonoBehaviour {
    public GameObject sloopModel;
    public Material playerMat, enemyMat, ballMat, softMat;
    public Mesh ballMesh;
    public Camera cam; public Transform sea;

    enum State { Title, Sailing, Won, Lost }
    State state = State.Title; bool paused; float stateT;
    Ship player, enemy;
    float windBase = 60f, windFrom = 60f;   // degrees the wind blows FROM (0 = from screen-top / +Z)
    Shot shot = Shot.Round; int victories;
    const float CamPitch = 52f, MuzzleSpeed = 42f, Gravity = 9.81f;
    Vector3 camFocus; float camDist = 46f;
    float nextLog, nextFoam;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
    bool devFreezeEnemy, devGod;
#endif

    class Ball { public Transform t; public Vector3 v; public Ship from; public Shot type; public float life; }
    readonly List<Ball> balls = new(); readonly Stack<Transform> ballPool = new();
    ParticleSystem smokePS, splashPS, flashPS, foamPS;

    // ---------- setup ----------
    void Start(){
      Application.targetFrameRate = 60;
      player = MakeShip("Your sloop", playerMat); enemy = MakeShip("Enemy sloop", enemyMat);
      player.Reset(Vector3.zero, 150f, 0.6f);
      SpawnEnemy(); camFocus = player.Pos;
      smokePS = MakePS("Smoke", -0.05f); splashPS = MakePS("Splash", 1.2f); flashPS = MakePS("Flash", 0f); foamPS = MakePS("Foam", 0f);
      MakeHudTextures();
      Debug.Log("[SG] ready");
    }
    Ship MakeShip(string n, Material m){ var s = new GameObject(n).AddComponent<Ship>(); s.Init(sloopModel, m, n); return s; }
    void SpawnEnemy(){
      float a = Random.Range(0f, 360f); var p = player.Pos + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 130f;
      enemy.Reset(p, a + 180f, 0.85f);
      enemy.maxSpeed = 10f; enemy.reloadBase = 4.8f; enemy.turnRate = 34f;
      Debug.Log($"[SG] enemy spawned dist={Vector3.Distance(p, player.Pos):F0}");
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

    // ---------- input ----------
    enum Role { Steer, Slider, Port, Star, ShotBtn, Pause, Ui }
    readonly Dictionary<int, (Role role, float startX)> ptrs = new();
    float touchRudder, lastTouch = -10f; bool touchSteering;
    float S => Screen.height / 720f;
    Rect RPort => new Rect(20 * S, Screen.height - 160 * S, 140 * S, 140 * S);
    Rect RStar => new Rect(Screen.width - 160 * S, Screen.height - 160 * S, 140 * S, 140 * S);
    Rect RShot => new Rect(Screen.width / 2f - 90 * S, Screen.height - 76 * S, 180 * S, 60 * S);
    Rect RSlider => new Rect(Screen.width - 112 * S, Screen.height - 480 * S, 72 * S, 290 * S);
    Rect RPause => new Rect(Screen.width / 2f - 40 * S, 12 * S, 80 * S, 50 * S);

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
      }
      if (gp != null){
        float x = gp.leftStick.x.ReadValue(); if (Mathf.Abs(x) > 0.2f) steer += x;
        sailDelta += gp.rightTrigger.ReadValue() - gp.leftTrigger.ReadValue();
        if (gp.leftShoulder.wasPressedThisFrame) Fire(player, -1, "gamepad");
        if (gp.rightShoulder.wasPressedThisFrame) Fire(player, 1, "gamepad");
        if (gp.buttonNorth.wasPressedThisFrame) CycleShot("gamepad");
      }
      // touch / mouse: fire buttons, shot button, sail slider, drag anywhere else to steer
      touchSteering = false;
      var seen = new HashSet<int>();
      foreach (var (id, p, began, down) in Pointers()){
        seen.Add(id);
        if (began){
          Role r = RPort.Contains(p) ? Role.Port : RStar.Contains(p) ? Role.Star : RShot.Contains(p) ? Role.ShotBtn : RSlider.Contains(p) ? Role.Slider : RPause.Contains(p) ? Role.Pause : Role.Steer;
          ptrs[id] = (r, p.x); string src = id < 0 ? "mouse" : "touch";
          if (r == Role.Port) Fire(player, -1, src); else if (r == Role.Star) Fire(player, 1, src);
          else if (r == Role.ShotBtn) CycleShot(src); else if (r == Role.Pause) SetPaused(true, src);
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
      paused = on; Time.timeScale = on ? 0f : 1f; ptrs.Clear();
      Debug.Log($"[SG] {(on ? "paused" : "resumed")} by {src}");
    }

    // ---------- loop ----------
    void Update(){
      var kb = Keyboard.current; var gp = Gamepad.current;
      if (state == State.Title){ if (AnyPress()){ state = State.Sailing; Debug.Log("[SG] start"); } return; }
      bool pauseKey = (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) || (gp != null && gp.startButton.wasPressedThisFrame);
      if (paused){ if (pauseKey || AnyPress()) SetPaused(false, "input"); return; }
      if (pauseKey && state == State.Sailing){ SetPaused(true, "key"); return; }
      float dt = Time.deltaTime; stateT += dt;
      windFrom = windBase + (Mathf.PerlinNoise(Time.time * 0.012f, 3.1f) - 0.5f) * 70f;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
      DevKeys(kb);
#endif
      if (state == State.Sailing) HandlePlayerInput(dt); else { player.rudder = 0f; }
      if ((state == State.Won || state == State.Lost) && stateT > 2.5f && AnyPress()) NextBattle();
      EnemyAI();
      player.Tick(dt, windFrom); enemy.Tick(dt, windFrom);
      Separate();
      UpdateBalls(dt);
      Wake(player); Wake(enemy);
      if (Time.time > nextFoam) nextFoam = Time.time + 0.07f;
      if (state == State.Sailing && Time.time > nextLog){
        nextLog = Time.time + 1f;
        var el = player.transform.InverseTransformPoint(enemy.Pos);   // enemy bearing: side +1 starboard / -1 port, degrees off the beam
        Debug.Log($"[SG] sail speed={player.speed:F1} heading={player.heading:F0} windFrom={windFrom:F0} sail={player.sail:F2} eff={Ship.WindFactor(player.OffWind(windFrom)):F2} dist={Vector3.Distance(player.Pos, enemy.Pos):F0} side={(el.x > 0f ? 1 : -1)} offBeam={Mathf.Atan2(el.z, Mathf.Abs(el.x)) * Mathf.Rad2Deg:F0} hull={player.hull:F0} enemyHull={enemy.hull:F0}");
      }
    }
    void NextBattle(){
      if (state == State.Lost) player.Reset(player.Pos, player.heading, 0.6f);
      foreach (var b in balls) Recycle(b.t); balls.Clear();
      SpawnEnemy(); state = State.Sailing; stateT = 0f; Debug.Log("[SG] next battle");
    }

    void EnemyAI(){
      var e = enemy; if (e.sinking || e.gone) return;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
      if (devFreezeEnemy){ e.rudder = 0f; e.sail = 0f; return; }
#endif
      if (player.sinking || state != State.Sailing){ e.rudder = 0f; e.sail = 0.4f; return; }
      Vector3 d = player.Pos - e.Pos; d.y = 0f; float dist = d.magnitude, bearing = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, desired;
      if (dist > 60f) desired = bearing;
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
      e.sail = dist > 60f ? 0.9f : 0.65f;
      if (dist < 62f){
        var local = e.transform.InverseTransformPoint(player.Pos);
        float offBeam = Mathf.Abs(Mathf.Atan2(local.z, Mathf.Abs(local.x)) * Mathf.Rad2Deg);
        if (offBeam < 22f) Fire(e, local.x > 0f ? 1 : -1, "ai");
      }
    }
    void Separate(){
      if (enemy.gone) return;
      Vector3 d = enemy.Pos - player.Pos; d.y = 0f; float m = d.magnitude;
      if (m < 11f && m > 0.01f){ var push = d / m * (11f - m) * 0.5f; enemy.transform.position += push; player.transform.position -= push; player.speed *= 0.97f; enemy.speed *= 0.97f; }
    }
    void Wake(Ship s){
      if (s.gone || Time.time <= nextFoam || s.speed < 1.5f) return;
      var p = s.Pos - s.Fwd * 6.2f + Vector3.up * 0.2f;
      Emit(foamPS, p + s.Right * Random.Range(-0.8f, 0.8f), -s.Fwd * 0.5f + s.Right * Random.Range(-0.6f, 0.6f), Random.Range(1.4f, 2.6f), 2.4f, new Color(0.92f, 0.96f, 0.95f, 0.35f));
      Emit(foamPS, s.Pos + s.Fwd * 6f + s.Right * Random.Range(-1.6f, 1.6f), s.Right * Random.Range(-1.5f, 1.5f), Random.Range(1f, 1.8f), 1.2f, new Color(0.95f, 0.98f, 0.97f, 0.4f));
    }

    // ---------- guns ----------
    void Fire(Ship s, int side, string src){
      if (s.sinking || s.gone || state != State.Sailing || paused) return;
      bool port = side < 0; float ready = port ? s.portReady : s.starReady;
      if (Time.time < ready) return;
      if (port) s.portReady = Time.time + s.ReloadTime; else s.starReady = Time.time + s.ReloadTime;
      Shot type = s == player ? shot : Shot.Round;
      Debug.Log($"[SG] fire {(port ? "port" : "starboard")} by {(s == player ? "player" : "enemy")} src={src} shot={type}");
      StartCoroutine(Broadside(s, port ? s.portGuns : s.starGuns, side, type));
    }
    IEnumerator Broadside(Ship s, List<Vector3> guns, int side, Shot type){
      foreach (var g in guns){
        if (s.sinking) yield break;
        var mp = s.transform.TransformPoint(g) + s.Right * side * 0.6f; var dir = s.Right * side;
        float spread = s == player ? 2.5f : 8f;
        dir = Quaternion.Euler(0f, Random.Range(-spread, spread), 0f) * dir;
        var v = dir * MuzzleSpeed + s.Fwd * s.speed + Vector3.up * Random.Range(5.4f, 6.2f);
        var t = ballPool.Count > 0 ? ballPool.Pop() : NewBall(); t.gameObject.SetActive(true); t.position = mp;
        t.localScale = Vector3.one * (type == Shot.Grape ? 0.3f : 0.5f);
        balls.Add(new Ball{ t = t, v = v, from = s, type = type, life = 3f });
        float vol = s == player ? 1f : Mathf.Clamp01(1.2f - Vector3.Distance(s.Pos, player.Pos) / 140f);
        GameAudio.I?.Play(s == player ? "cannon" : "enemyCannon", vol);
        Emit(flashPS, mp, dir * 6f, 1.6f, 0.15f, new Color(1f, 0.7f, 0.3f, 0.95f));
        for (int i = 0; i < 4; i++) Emit(smokePS, mp + dir * i * 0.8f, dir * Random.Range(2f, 5f) + Vector3.up * 0.6f + Random.insideUnitSphere * 0.6f, Random.Range(2.2f, 3.6f), Random.Range(1.8f, 2.8f), new Color(0.93f, 0.9f, 0.85f, 0.85f));
        yield return new WaitForSeconds(0.09f);
      }
    }
    Transform NewBall(){
      var go = new GameObject("Ball"); go.AddComponent<MeshFilter>().sharedMesh = ballMesh; go.AddComponent<MeshRenderer>().sharedMaterial = ballMat;
      return go.transform;
    }
    void Recycle(Transform t){ t.gameObject.SetActive(false); ballPool.Push(t); }
    void UpdateBalls(float dt){
      for (int i = balls.Count - 1; i >= 0; i--){
        var b = balls[i]; b.v.y -= Gravity * dt; b.t.position += b.v * dt; b.life -= dt;
        var target = b.from == player ? enemy : player; var p = b.t.position;
        if (!target.sinking && !target.gone){
          var l = target.transform.InverseTransformPoint(p);
          if (Mathf.Abs(l.x) < 2.4f && Mathf.Abs(l.z) < 6.6f && l.y < 4.5f && l.y > -1.5f){ Hit(target, b.type, p); Recycle(b.t); balls.RemoveAt(i); continue; }
        }
        if (p.y < 0f || b.life <= 0f){
          if (p.y < 0.5f){ for (int k = 0; k < 5; k++) Emit(splashPS, new Vector3(p.x, 0.2f, p.z), new Vector3(Random.Range(-1f, 1f), Random.Range(4f, 7f), Random.Range(-1f, 1f)), Random.Range(0.7f, 1.3f), 0.9f, new Color(0.95f, 0.98f, 1f, 0.9f)); }
          Recycle(b.t); balls.RemoveAt(i);
        }
      }
    }
    void Hit(Ship s, Shot type, Vector3 p){
#if DEVELOPMENT_BUILD || UNITY_EDITOR
      if (s == player && devGod) return;
#endif
      float k = s == player ? 0.7f : 1f;   // the enemy's gunners are weaker, so a fair fight favours the player
      switch (type){
        case Shot.Round: s.hull -= 8f * k; s.crew -= 1.5f * k; break;
        case Shot.Chain: s.sails -= 13f * k; s.hull -= 2f * k; break;
        case Shot.Grape: s.crew -= 9f * k; s.hull -= 1.5f * k; break;
      }
      s.hull = Mathf.Max(0f, s.hull); s.sails = Mathf.Max(0f, s.sails); s.crew = Mathf.Max(0f, s.crew);
      GameAudio.I?.Play("hit", s == player ? 1f : Mathf.Clamp01(1.2f - Vector3.Distance(s.Pos, player.Pos) / 140f));
      for (int i = 0; i < 4; i++) Emit(splashPS, p, Random.insideUnitSphere * 4f + Vector3.up * 3f, Random.Range(0.35f, 0.6f), 0.8f, new Color(0.45f, 0.3f, 0.18f, 1f));   // splinters
      for (int i = 0; i < 2; i++) Emit(smokePS, p, Vector3.up * 1.5f + Random.insideUnitSphere, Random.Range(2f, 3f), 1.8f, new Color(0.35f, 0.32f, 0.3f, 0.7f));
      Debug.Log($"[SG] hit {(s == player ? "player" : "enemy")} shot={type} hull={s.hull:F0} sails={s.sails:F0} crew={s.crew:F0}");
      if (s.hull <= 0f) Sink(s);
    }
    void Sink(Ship s){
      s.sinking = true; s.rudder = 0f; stateT = 0f;
      if (s == enemy){ victories++; state = State.Won; Debug.Log($"[SG] enemy sunk victories={victories}"); }
      else { state = State.Lost; Debug.Log("[SG] player sunk"); }
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
      if (kb == null) return;
      if (kb.f1Key.wasPressedThisFrame && !enemy.sinking){   // bring the enemy alongside to starboard, 30 m off, same heading
        enemy.Reset(player.Pos + player.Right * 30f, player.heading, 0.6f); enemy.speed = player.speed; Debug.Log("[SG] dev: enemy alongside");
      }
      if (kb.f2Key.wasPressedThisFrame){ devFreezeEnemy = !devFreezeEnemy; Debug.Log($"[SG] dev: freeze enemy {devFreezeEnemy}"); }
      if (kb.f3Key.wasPressedThisFrame){ devGod = !devGod; Debug.Log($"[SG] dev: god {devGod}"); }
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
    void OnGUI(){
      if (!player || white == null) return;
      float s = S, W = Screen.width, H = Screen.height;
      txt ??= new GUIStyle(GUI.skin.label){ fontStyle = FontStyle.Bold }; big ??= new GUIStyle(GUI.skin.label){ alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold }; small ??= new GUIStyle(GUI.skin.label);
      txt.fontSize = Mathf.RoundToInt(18 * s); big.fontSize = Mathf.RoundToInt(44 * s); small.fontSize = Mathf.RoundToInt(15 * s);
      txt.normal.textColor = small.normal.textColor = big.normal.textColor = Parchment;

      if (state == State.Title){
        Box(new Rect(0, 0, W, H), new Color(0.05f, 0.04f, 0.03f, 0.55f));
        Label(new Rect(0, H * 0.22f, W, 70 * s), "Salt and Glory", big, Parchment);
        var c = new GUIStyle(txt){ alignment = TextAnchor.MiddleCenter, wordWrap = true };
        Label(new Rect(W * 0.15f, H * 0.4f, W * 0.7f, 200 * s),
          "Sail with the wind and sink the enemy sloop.\n\nA / D steer   ·   W / S sails   ·   Q / E port / starboard broadside   ·   Tab shot type   ·   Esc pause\n" +
          "Touch: drag to steer, sail slider, fire buttons\n\nPress Enter, click or tap to set sail", c, Parchment);
        return;
      }
      // wind compass (top-left): arrow points where the wind blows
      var cr = new Rect(16 * s, 16 * s, 110 * s, 110 * s);
      var o = GUI.color; GUI.color = new Color(0.12f, 0.09f, 0.06f, 0.55f); GUI.DrawTexture(cr, disc); GUI.color = Parchment; GUI.DrawTexture(cr, ring);
      var m = GUI.matrix; GUIUtility.RotateAroundPivot(windFrom + 180f, cr.center); GUI.color = new Color(0.55f, 0.8f, 0.95f); GUI.DrawTexture(new Rect(cr.center.x - 22 * s, cr.center.y - 40 * s, 44 * s, 80 * s), arrow); GUI.matrix = m;
      GUIUtility.RotateAroundPivot(player.heading, cr.center); GUI.color = new Color(0.95f, 0.75f, 0.35f); GUI.DrawTexture(new Rect(cr.center.x - 5 * s, cr.y - 2 * s, 10 * s, 18 * s), white); GUI.matrix = m;
      GUI.color = o;
      Label(new Rect(cr.x, cr.yMax + 2 * s, 260 * s, 24 * s), "WIND", small, Parchment);
      float eff = Ship.WindFactor(player.OffWind(windFrom));
      Box(new Rect(136 * s, 16 * s, 250 * s, 110 * s), Panel);
      Label(new Rect(146 * s, 20 * s, 240 * s, 26 * s), $"{Ship.PointOfSail(player.OffWind(windFrom))}  {eff * 100f:F0}%", txt, Parchment);
      Label(new Rect(146 * s, 44 * s, 240 * s, 24 * s), $"Speed {player.speed * 1.2f:F1} kn   Sails {player.sail * 100f:F0}%", small, Parchment);
      Bar(146 * s, 66 * s, 150 * s, "Hull", player.hull, new Color(0.85f, 0.55f, 0.3f));
      Bar(146 * s, 84 * s, 150 * s, "Sails", player.sails, new Color(0.92f, 0.88f, 0.75f));
      Bar(146 * s, 102 * s, 150 * s, "Crew", player.crew, new Color(0.6f, 0.8f, 0.55f));
      if (!enemy.gone){
        float d = Vector3.Distance(player.Pos, enemy.Pos);
        Box(new Rect(W - 266 * s, 16 * s, 250 * s, 92 * s), Panel);
        Label(new Rect(W - 256 * s, 20 * s, 240 * s, 26 * s), $"{enemy.label}  {d:F0} m", txt, new Color(1f, 0.75f, 0.65f));
        Bar(W - 256 * s, 48 * s, 150 * s, "Hull", enemy.hull, new Color(0.85f, 0.4f, 0.3f));
        Bar(W - 256 * s, 66 * s, 150 * s, "Sails", enemy.sails, new Color(0.92f, 0.88f, 0.75f));
        Bar(W - 256 * s, 84 * s, 150 * s, "Crew", enemy.crew, new Color(0.6f, 0.8f, 0.55f));
        if (d > 80f){   // off-screen pointer towards the enemy
          var dir = enemy.Pos - player.Pos; var sp = new Vector2(dir.x, -dir.z).normalized; var c = new Vector2(W / 2f, H / 2f);
          float r = Mathf.Min(W, H) * 0.42f; var p = c + sp * r; GUI.color = new Color(1f, 0.55f, 0.45f, 0.9f);
          GUIUtility.RotateAroundPivot(Mathf.Atan2(sp.x, -sp.y) * Mathf.Rad2Deg, p); GUI.DrawTexture(new Rect(p.x - 14 * s, p.y - 20 * s, 28 * s, 40 * s), arrow); GUI.matrix = m; GUI.color = o;
        }
      }
      // touch controls (also clickable)
      DrawFire(RPort, "PORT\n(Q)", player.portReady); DrawFire(RStar, "STARBOARD\n(E)", player.starReady);
      var rs = RShot; Box(rs, Panel); Label(rs, $"Shot: {shot}  (Tab)", new GUIStyle(txt){ alignment = TextAnchor.MiddleCenter }, Parchment);
      var sl = RSlider; Box(sl, Panel); Box(new Rect(sl.center.x - 3 * s, sl.y + 10 * s, 6 * s, sl.height - 20 * s), new Color(1, 1, 1, 0.3f));
      float ky = sl.y + 10 * s + (1f - player.sail) * (sl.height - 20 * s); Box(new Rect(sl.x + 6 * s, ky - 9 * s, sl.width - 12 * s, 18 * s), Parchment);
      Label(new Rect(sl.x - 10 * s, sl.y - 24 * s, sl.width + 20 * s, 22 * s), "SAILS", new GUIStyle(small){ alignment = TextAnchor.MiddleCenter }, Parchment);
      var rp = RPause; Box(rp, Panel); Label(rp, "II", new GUIStyle(txt){ alignment = TextAnchor.MiddleCenter }, Parchment);

      if (paused || state == State.Won || state == State.Lost){
        string head = paused ? "Paused" : state == State.Won ? "Victory!" : "Your sloop went down";
        string sub = paused ? "Esc, Enter or tap to resume" : stateT > 2.5f ? (state == State.Won ? $"Enemy sunk ({victories} so far). Enter or tap to hunt the next one" : "Enter or tap to try again") : "";
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

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SaltGlory;

/// <summary>Headless scene setup + WebGL build (same setup as Neon Hive). Usage:
/// Unity -batchmode -nographics -projectPath . -buildTarget WebGL -executeMethod BuildWeb.Build -logFile build.log
/// (no -quit; this calls EditorApplication.Exit). SG_DEV=1 makes a development build in Builds/WebGLDev.</summary>
public static class BuildWeb {
  const string ScenePath = "Assets/Scenes/Main.unity", Gen = "Assets/Generated";

  static T Load<T>(string path) where T : Object { var a = AssetDatabase.LoadAssetAtPath<T>(path); if (!a) Debug.LogWarning("[Setup] missing " + path); return a; }

  static UniversalRenderPipelineAsset EnsureURP(){
    var path = Gen + "/URP_Asset.asset";
    var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
    if (!asset){
      var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
      AssetDatabase.CreateAsset(rd, Gen + "/URP_Renderer.asset");
      asset = UniversalRenderPipelineAsset.Create(rd);
      AssetDatabase.CreateAsset(asset, path);
    }
    asset.supportsHDR = false; asset.msaaSampleCount = 2; EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
    GraphicsSettings.defaultRenderPipeline = asset;
    for (int i = 0; i < QualitySettings.names.Length; i++){ QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = asset; }
    return asset;
  }
  static Material Mat(string name, string shader, Texture tex, Color c){
    var path = $"{Gen}/{name}.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path); var sh = Shader.Find(shader);
    if (!sh) throw new System.Exception("shader missing: " + shader);
    if (!m){ m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
    m.shader = sh; if (tex && m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex); if (m.HasProperty("_Tint")) m.SetColor("_Tint", c);
    EditorUtility.SetDirty(m); return m;
  }
  static Mesh SaveMesh(string name, Mesh mesh){
    var path = $"{Gen}/{name}.asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
    if (old){ old.Clear(); EditorUtility.CopySerialized(mesh, old); EditorUtility.SetDirty(old); return old; }
    AssetDatabase.CreateAsset(mesh, path); return mesh;
  }
  static Mesh SeaQuad(float size){
    var m = new Mesh{ name = "SeaQuad" }; float h = size / 2f;
    m.vertices = new[]{ new Vector3(-h, 0, -h), new Vector3(-h, 0, h), new Vector3(h, 0, h), new Vector3(h, 0, -h) };
    m.triangles = new[]{ 0, 1, 2, 0, 2, 3 }; m.RecalculateBounds(); m.bounds = new Bounds(Vector3.zero, new Vector3(size, 2, size)); return m;
  }
  static Mesh Sphere(int lon, int lat){   // low-poly cannonball (no runtime CreatePrimitive)
    var m = new Mesh{ name = "Ball" }; var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>();
    for (int y = 0; y <= lat; y++) for (int x = 0; x <= lon; x++){
      float a = x * Mathf.PI * 2f / lon, b = y * Mathf.PI / lat; v.Add(new Vector3(Mathf.Sin(b) * Mathf.Cos(a), Mathf.Cos(b), Mathf.Sin(b) * Mathf.Sin(a)) * 0.5f);
    }
    for (int y = 0; y < lat; y++) for (int x = 0; x < lon; x++){ int i = y * (lon + 1) + x; t.AddRange(new[]{ i, i + 1, i + lon + 1, i + 1, i + lon + 2, i + lon + 1 }); }
    m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateBounds(); return m;
  }

  static Mesh ChainQuad(){   // flat quad (XZ) for the chain-shot sprite: 2.4 m wide across the flight, as seen top-down
    var m = new Mesh{ name = "ChainQuad" }; float w = 1.2f, d = 0.6f;
    m.vertices = new[]{ new Vector3(-w, 0, -d), new Vector3(-w, 0, d), new Vector3(w, 0, d), new Vector3(w, 0, -d) };
    m.uv = new[]{ new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
    m.triangles = new[]{ 0, 1, 2, 0, 2, 3 }; m.RecalculateBounds(); return m;
  }
  /// <summary>Land mask from Art Dev's chart: half resolution (World.MW x World.MH, rows from the south), land where the paint
  /// is warm (red well above blue: sand, hills and the ink coastline), sea where it is green-blue. The compass rose is cleared.</summary>
  static TextAsset LandMask(){
    const string src = "Assets/Art/Map/SG_Map_Caribbean.png", dst = Gen + "/LandMask.bytes";
    if (!File.Exists(src)){ Debug.LogWarning("[Setup] no chart, land mask falls back to polygons"); return null; }
    var t = new Texture2D(2, 2); t.LoadImage(File.ReadAllBytes(src)); int w = t.width, h = t.height; var px = t.GetPixels32();
    int MW = World.MW, MH = World.MH; var b = new byte[MW * MH];
    for (int y = 0; y < MH; y++) for (int x = 0; x < MW; x++){
      int sx = x * w / MW, sy = y * h / MH, warm = 0;
      for (int j = 0; j < 2; j++) for (int i = 0; i < 2; i++){ var c = px[Mathf.Min(h - 1, sy + j) * w + Mathf.Min(w - 1, sx + i)]; if (c.r - c.b > 40) warm++; }
      float u = (x + 0.5f) / MW, v = 1f - (y + 0.5f) / MH;   // v from the top, like the chart's pixel coordinates
      bool rose = Mathf.Pow((u - 450f / 2560f) * 2560f, 2) + Mathf.Pow((v - 338f / 1600f) * 1600f, 2) < 150f * 150f;
      b[y * MW + x] = (byte)(warm >= 2 && !rose ? 255 : 0);
    }
    Object.DestroyImmediate(t);
    // close channels narrower than a ship (~3 px = 7 m radius): fills the inlet north of Port Royal where ships got pinned (M2 QA)
    b = Close(b, MW, MH, 3);
    File.WriteAllBytes(dst, b); AssetDatabase.ImportAsset(dst); return AssetDatabase.LoadAssetAtPath<TextAsset>(dst);
  }
  static byte[] Close(byte[] m, int w, int h, int r){
    byte[] Morph(byte[] src, bool dilate){
      var o = new byte[src.Length];
      for (int y = 0; y < h; y++) for (int x = 0; x < w; x++){
        bool hit = !dilate;
        for (int j = -r; j <= r && hit != dilate; j++) for (int i = -r; i <= r; i++){
          if (i * i + j * j > r * r) continue; int xx = x + i, yy = y + j;
          bool land = xx < 0 || yy < 0 || xx >= w || yy >= h || src[yy * w + xx] > 0;
          if (dilate && land){ hit = true; break; } if (!dilate && !land){ hit = false; break; }
        }
        o[y * w + x] = (byte)(hit ? 255 : 0);
      }
      return o;
    }
    return Morph(Morph(m, true), false);
  }
  /// <summary>First texture in a folder whose file name contains the key (so Art Dev's files drop in by name), or null.</summary>
  static Texture2D Art(string folder, string key){
    if (!AssetDatabase.IsValidFolder(folder)){ Debug.LogWarning($"[Setup] missing folder {folder} ({key})"); return null; }
    foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[]{ folder })){
      var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p).Contains(key)) return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
    }
    Debug.LogWarning($"[Setup] no art for {key} in {folder} (fallback)"); return null;
  }
  static Texture2D ArtAny(string key){
    foreach (var gd in AssetDatabase.FindAssets("t:Texture2D", new[]{ "Assets/Art" })){
      var p = AssetDatabase.GUIDToAssetPath(gd); if (Path.GetFileNameWithoutExtension(p).Contains(key)) return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
    }
    Debug.LogWarning($"[Setup] no art for {key} (fallback)"); return null;
  }
  static Vector3 Anchor(string json, string file){
    var m = System.Text.RegularExpressions.Regex.Match(json ?? "", "\"" + System.Text.RegularExpressions.Regex.Escape(file) + "\"\\s*:\\s*\\{\\s*\"hoist\"\\s*:\\s*\\[\\s*([\\d.]+)\\s*,\\s*([\\d.]+)\\s*\\]\\s*,\\s*\"scale\"\\s*:\\s*([\\d.]+)");
    if (!m.Success){ Debug.LogWarning("[Setup] no flag anchor for " + file); return Vector3.zero; }
    var ci = System.Globalization.CultureInfo.InvariantCulture;
    return new Vector3(float.Parse(m.Groups[1].Value, ci), float.Parse(m.Groups[2].Value, ci), float.Parse(m.Groups[3].Value, ci));
  }

  [MenuItem("Salt and Glory/Rebuild Main Scene")]
  public static void SetupScene(){
    Directory.CreateDirectory(Gen); Directory.CreateDirectory("Assets/Scenes");
    EnsureURP();
    var sloop = Load<GameObject>("Assets/Art/Ships/Sloop/Sloop.fbx"); if (!sloop) throw new System.Exception("Sloop.fbx missing");
    var albedo = Load<Texture2D>("Assets/Art/Ships/Sloop/Sloop_Albedo.png");
    var frame = Load<Texture2D>("Assets/Art/StyleFrames/SG_StyleFrame_ShipBattle.png");
    var playerMat = Mat("SloopMat", "SaltGlory/Unlit", albedo, Color.white);
    var ballMat = Mat("BallMat", "SaltGlory/Unlit", Texture2D.whiteTexture, new Color(0.11f, 0.1f, 0.09f));
    var softMat = Mat("SoftParticle", "SaltGlory/SoftParticle", null, Color.white);
    var seaMat = Mat("SeaMat", "SaltGlory/Sea", frame, new Color(1.3f, 1.36f, 1.32f));
    var chainTex = Load<Texture2D>("Assets/Art/Projectiles/SG_Shot_Chain.png");
    var chainMat = chainTex ? Mat("ChainShotMat", "SaltGlory/UnlitAlpha", chainTex, Color.white) : null;
    var seaMesh = SaveMesh("SeaQuad", SeaQuad(700f)); var ballMesh = SaveMesh("Ball", Sphere(10, 7)); var chainMesh = SaveMesh("ChainQuad", ChainQuad());
    var landMask = LandMask();
    AssetDatabase.SaveAssets();

    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.white; RenderSettings.fog = false;

    var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
    var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(0x0E, 0x34, 0x3C, 255);
    cam.fieldOfView = 45; cam.nearClipPlane = 0.5f; cam.farClipPlane = 500; cam.allowHDR = false;
    var cd = camGo.AddComponent<UniversalAdditionalCameraData>(); cd.renderPostProcessing = false; cd.renderShadows = false;
    camGo.AddComponent<AudioListener>();

    var sea = new GameObject("Sea"); sea.AddComponent<MeshFilter>().sharedMesh = seaMesh; var sr = sea.AddComponent<MeshRenderer>(); sr.sharedMaterial = seaMat;
    sr.shadowCastingMode = ShadowCastingMode.Off; sr.receiveShadows = false;

    var g = new GameObject("Game").AddComponent<Game>();
    g.sloopModel = sloop; g.playerMat = playerMat; g.ballMat = ballMat; g.softMat = softMat; g.ballMesh = ballMesh;
    g.cam = cam; g.sea = sea.transform; g.seaMat = seaMat; g.chainMesh = chainMesh; g.chainMat = chainMat; g.landMask = landMask;
    // M2 art by file name (Art Dev's M2 set); anything missing stays null and the game falls back
    g.portsJson = Load<TextAsset>("Assets/Art/Map/SG_Map_Ports.json");
    g.titleTex = Load<Texture2D>("Assets/Art/StyleFrames/SG_StyleFrame_OpenSea.png");
    g.mapTex = Art("Assets/Art/Map", "SG_Map_Caribbean"); g.portMarkerTex = Art("Assets/Art/Map", "PortMarker"); g.portRingTex = Art("Assets/Art/Map", "PortMarker_Ring");
    if (g.portMarkerTex == g.portRingTex) g.portMarkerTex = Load<Texture2D>("Assets/Art/Map/SG_Map_PortMarker.png");
    g.shipMarkerTex = Art("Assets/Art/Map", "ShipMarker");
    g.panelTex = Art("Assets/Art/UI", "Panel_Parchment"); g.buttonTex = Art("Assets/Art/UI", "Button_Normal"); g.buttonHiTex = Art("Assets/Art/UI", "Button_Highlighted"); g.coinTex = Art("Assets/Art/UI", "Icon_Coin");
    g.tavernTex = Art("Assets/Art/Towns", "Town_Tavern"); g.governorTex = Art("Assets/Art/Towns", "Town_Governor"); g.merchantTex = Art("Assets/Art/Towns", "Town_Merchant"); g.shipwrightTex = Art("Assets/Art/Towns", "Town_Shipwright");
    string[] nat = { "England", "Spain", "France", "Dutch" };
    var anchors = Load<TextAsset>("Assets/Art/Towns/SG_Town_FlagAnchors.json"); var aj = anchors ? anchors.text : "";
    g.harborTex = new Texture2D[4]; g.flagTex = new Texture2D[4]; g.flagIconTex = new Texture2D[4]; g.flagAnchors = new Vector3[5];
    for (int i = 0; i < 4; i++){
      g.harborTex[i] = Art("Assets/Art/Towns", "Harbor_" + nat[i]) ?? Load<Texture2D>("Assets/Art/StyleFrames/SG_StyleFrame_PortTown.png");
      g.flagTex[i] = Art("Assets/Art/Towns/Flags", "Flag_" + nat[i]); g.flagIconTex[i] = Art("Assets/Art/UI", "Icon_Flag_" + nat[i]);
      g.flagAnchors[i] = Anchor(aj, $"SG_Harbor_{nat[i]}.png");
    }
    g.flagAnchors[4] = Anchor(aj, "SG_Town_Governor.png");
    // M3 ship classes: Assets/Art/Ships/<Class>/<Class>.fbx (+ <Class>_Albedo.png) when Art Dev delivers them; else the sloop stands in
    g.classModels = new GameObject[ShipClass.All.Length]; g.classMats = new Material[ShipClass.All.Length];
    g.classModels[0] = sloop; g.classMats[0] = playerMat;
    for (int i = 1; i < ShipClass.All.Length; i++){
      string n = ShipClass.All[i].name, dir = $"Assets/Art/Ships/{n}";
      var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{dir}/{n}.fbx"); if (!fbx){ Debug.LogWarning($"[Setup] no model for {n} (sloop placeholder)"); continue; }
      g.classModels[i] = fbx; var alb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{n}_Albedo.png"); g.classMats[i] = Mat(n + "Mat", "SaltGlory/Unlit", alb ? alb : albedo, Color.white);
    }
    // M3 duel and treasure art, anywhere under Assets/Art by name (fallback: drawn shapes)
    g.duelSkyTex = ArtAny("SG_Duel_Sky"); g.duelSeaTex = ArtAny("SG_Duel_Sea"); g.duelDeckTex = ArtAny("SG_Duel_Deck");
    string[] v6 = { "Base", "England", "Spain", "France", "Dutch", "Pirate" };
    g.captainPlayer = v6.Select(v => ArtAny($"SG_Captain_Player_{v}")).ToArray(); g.captainEnemy = v6.Select(v => ArtAny($"SG_Captain_Enemy_{v}")).ToArray();
    g.chestTex = ArtAny("SG_Treasure_Chest"); g.digMarkTex = ArtAny("SG_Treasure_DigSpot"); g.mapFullTex = ArtAny("SG_Treasure_Map_Full");
    g.mapPieceTex = Enumerable.Range(1, 4).Select(i => ArtAny($"SG_Treasure_MapPiece_{i}")).ToArray();
    g.rankTex = Career.Ranks.Select((r, i) => ArtAny($"SG_Icon_Rank_{i + 1}_{r}")).ToArray(); g.logTex = ArtAny("SG_UI_CaptainsLog");
    g.stageJson = Load<TextAsset>("Assets/Art/Duel/Stage/SG_Duel_Stage.json"); g.framesJson = Load<TextAsset>("Assets/Art/Duel/Captains/SG_Captain_Frames.json");
    g.treasureJson = Load<TextAsset>("Assets/Art/Treasure/SG_Treasure_Map.json"); g.logJson = Load<TextAsset>("Assets/Art/UI/SG_UI_CaptainsLog.json");
    // nation / pirate albedos per class ([class * 6 + v]); the sloop's too
    g.shipAlbedo = new Texture2D[ShipClass.All.Length * 6];
    for (int c = 0; c < ShipClass.All.Length; c++) for (int v = 0; v < 6; v++){
      string n = ShipClass.All[c].name; g.shipAlbedo[c * 6 + v] = Load<Texture2D>($"Assets/Art/Ships/{n}/{n}_Albedo{(v == 0 ? "" : "_" + v6[v])}.png");
    }
    g.cargoTex = new Texture2D[5]; for (int i = 0; i < 5; i++) g.cargoTex[i] = Art("Assets/Art/UI", "Icon_Cargo_" + World.Goods[i]);

    var ga = new GameObject("GameAudio").AddComponent<GameAudio>();
    ga.music = Load<AudioClip>("Assets/Audio/Music/sailing_loop.ogg"); ga.waves = Load<AudioClip>("Assets/Audio/SFX/ocean_waves_loop.ogg");
    ga.cannon = Load<AudioClip>("Assets/Audio/SFX/cannon_shot.ogg");
    ga.townMusic = Load<AudioClip>("Assets/Audio/Music/town_loop.ogg"); ga.chatter = Load<AudioClip>("Assets/Audio/SFX/tavern_chatter_loop.ogg");
    ga.hullHit = Load<AudioClip>("Assets/Audio/SFX/hull_hit.ogg"); ga.splash = Load<AudioClip>("Assets/Audio/SFX/cannonball_splash.ogg");
    ga.coins = Load<AudioClip>("Assets/Audio/SFX/coins.ogg"); ga.door = Load<AudioClip>("Assets/Audio/SFX/door.ogg"); ga.bell = Load<AudioClip>("Assets/Audio/SFX/ships_bell.ogg");
    ga.mapUnroll = Load<AudioClip>("Assets/Audio/SFX/map_unroll.ogg"); ga.sailsFilling = Load<AudioClip>("Assets/Audio/SFX/sails_filling.ogg");
    // M3 audio (PR #7)
    ga.battleMusic = Load<AudioClip>("Assets/Audio/Music/battle_loop.ogg"); ga.duelMusic = Load<AudioClip>("Assets/Audio/Music/duel_loop.ogg");
    ga.clash = new[]{ "02", "03", "04" }.Select(n => Load<AudioClip>($"Assets/Audio/SFX/sword_clash_{n}.ogg")).Where(c => c).ToArray();
    AudioClip Sfx(string n) => Load<AudioClip>($"Assets/Audio/SFX/{n}.ogg");
    ga.parry = Sfx("parry_ring"); ga.whoosh = Sfx("sword_whoosh"); ga.bodyHit = Sfx("body_hit"); ga.overboard = Sfx("splash_overboard");
    ga.mastCrack = Sfx("mast_crack"); ga.creak = Sfx("timber_creak_loop"); ga.cheer = Sfx("crew_cheer"); ga.grapple = Sfx("grappling_hooks");
    ga.dig = Sfx("digging_shovel"); ga.chest = Sfx("chest_open");

    EditorSceneManager.SaveScene(scene, ScenePath);
    EditorBuildSettings.scenes = new[]{ new EditorBuildSettingsScene(ScenePath, true) };
    Debug.Log("[Setup] scene saved");
  }

  /// <summary>Page hardening (same as Neon Hive): canvas touch-action:none + preventDefault on touch events, no context menu on
  /// the canvas, Space / arrow keys / Tab don't scroll or move focus. Unity still receives all events. Title = Salt and Glory.</summary>
  static void PatchIndexHtml(string outDir){
    var p = Path.Combine(outDir, "index.html"); if (!File.Exists(p)) return;
    var html = File.ReadAllText(p); if (html.Contains("sg-input-guard")) return;
    html = System.Text.RegularExpressions.Regex.Replace(html, "<title>.*?</title>", "<title>Salt and Glory</title>");
    const string css = "<style id=\"sg-input-guard-css\">html,body{overscroll-behavior:none;background:#0E343C}#unity-canvas{touch-action:none;-ms-touch-action:none;user-select:none;-webkit-user-select:none;-webkit-touch-callout:none;-webkit-tap-highlight-color:transparent;outline:none}</style>";
    const string js = @"<script id=""sg-input-guard"">(function(){
  var c=document.getElementById('unity-canvas'); if(!c) return;
  var stop=function(e){ if(e.cancelable) e.preventDefault(); };
  ['touchstart','touchmove','touchend','touchcancel'].forEach(function(t){ c.addEventListener(t,stop,{passive:false}); });
  ['gesturestart','gesturechange','gestureend','dblclick','contextmenu'].forEach(function(t){ c.addEventListener(t,stop,{passive:false}); });
  document.addEventListener('contextmenu',function(e){ if(e.target===c) e.preventDefault(); });
  window.addEventListener('keydown',function(e){
    var k=e.key||'', tag=(e.target&&e.target.tagName)||'';
    if((k===' '||k==='Spacebar'||k==='Tab'||k.indexOf('Arrow')===0) && !/INPUT|TEXTAREA|SELECT/.test(tag)) e.preventDefault();
  },{passive:false});
  if(!c.hasAttribute('tabindex')) c.setAttribute('tabindex','-1');
  c.addEventListener('pointerdown',function(){ try{ c.focus(); }catch(_){} });
})();</script>";
    html = html.Replace(".then((unityInstance) => {", ".then((unityInstance) => { window.sgUnity = unityInstance;");   // lets the smoke test call Game.QAStatus
    html = html.Replace("</head>", css + "\n  </head>").Replace("</body>", js + "\n  </body>");
    File.WriteAllText(p, html);
  }

  [MenuItem("Salt and Glory/Build WebGL to docs")]
  public static void Build(){
    int code = 1;
    try {
      SetupScene();
      PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
      PlayerSettings.WebGL.decompressionFallback = true;
      PlayerSettings.WebGL.threadsSupport = false;
      PlayerSettings.productName = "Salt and Glory"; PlayerSettings.companyName = "mikenev";
      PlayerSettings.defaultWebScreenWidth = 1280; PlayerSettings.defaultWebScreenHeight = 720;
      bool dev = System.Environment.GetEnvironmentVariable("SG_DEV") == "1";
      if (dev){ PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace; PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.Full); }
      else { PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly; PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly); }
      var outDir = Path.GetFullPath(dev ? "Builds/WebGLDev" : "docs");
      var r = BuildPipeline.BuildPlayer(new[]{ ScenePath }, outDir, BuildTarget.WebGL, dev ? BuildOptions.Development : BuildOptions.None);
      Debug.Log("BUILD RESULT: " + r.summary.result + " size=" + r.summary.totalSize);
      if (r.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded){ File.WriteAllText(Path.Combine(outDir, ".nojekyll"), ""); PatchIndexHtml(outDir); code = 0; }
    } catch (System.Exception e){ Debug.LogException(e); }
    if (Application.isBatchMode) EditorApplication.Exit(code);
  }
}

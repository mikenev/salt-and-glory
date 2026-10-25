using System.IO;
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

  [MenuItem("Salt and Glory/Rebuild Main Scene")]
  public static void SetupScene(){
    Directory.CreateDirectory(Gen); Directory.CreateDirectory("Assets/Scenes");
    EnsureURP();
    var sloop = Load<GameObject>("Assets/Art/Ships/Sloop/Sloop.fbx"); if (!sloop) throw new System.Exception("Sloop.fbx missing");
    var albedo = Load<Texture2D>("Assets/Art/Ships/Sloop/Sloop_Albedo.png");
    var frame = Load<Texture2D>("Assets/Art/StyleFrames/SG_StyleFrame_ShipBattle.png");
    var playerMat = Mat("SloopMat", "SaltGlory/Unlit", albedo, Color.white);
    var enemyMat = Mat("EnemySloopMat", "SaltGlory/Unlit", albedo, new Color(0.8f, 0.66f, 0.64f));   // placeholder: the same sloop, darker red-brown
    var ballMat = Mat("BallMat", "SaltGlory/Unlit", Texture2D.whiteTexture, new Color(0.11f, 0.1f, 0.09f));
    var softMat = Mat("SoftParticle", "SaltGlory/SoftParticle", null, Color.white);
    var seaMat = Mat("SeaMat", "SaltGlory/Sea", frame, new Color(1.3f, 1.36f, 1.32f));
    var seaMesh = SaveMesh("SeaQuad", SeaQuad(700f)); var ballMesh = SaveMesh("Ball", Sphere(10, 7));
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
    g.sloopModel = sloop; g.playerMat = playerMat; g.enemyMat = enemyMat; g.ballMat = ballMat; g.softMat = softMat; g.ballMesh = ballMesh;
    g.cam = cam; g.sea = sea.transform;

    var ga = new GameObject("GameAudio").AddComponent<GameAudio>();
    ga.music = Load<AudioClip>("Assets/Audio/Music/sailing_loop.ogg"); ga.waves = Load<AudioClip>("Assets/Audio/SFX/ocean_waves_loop.ogg");
    ga.cannon = Load<AudioClip>("Assets/Audio/SFX/cannon_shot.ogg");

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

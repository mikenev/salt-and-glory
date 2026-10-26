using System.Collections.Generic;
using UnityEngine;
namespace SaltGlory {
  public enum Shot { Round, Chain, Grape }

  /// <summary>M3: the five ship classes (plan table: speed, guns, cargo, role). hullK divides damage taken, gunK multiplies
  /// damage dealt, complement is the full crew. scale is only used while a class has no model of its own (the sloop stands in).</summary>
  public class ShipClass {
    public string name; public float speed, turn, reload, gunK, hullK, scale; public int hold, complement, price; public Color tint;
    public static readonly ShipClass[] All = {
      new(){ name = "Sloop",      speed = 11f,  turn = 40f, reload = 3.2f, gunK = 0.85f, hullK = 1.0f, hold = 40,  complement = 100, price = 900,  scale = 1.0f,  tint = Color.white },
      new(){ name = "Brigantine", speed = 10.3f, turn = 35f, reload = 3.3f, gunK = 1.1f,  hullK = 1.35f, hold = 60,  complement = 140, price = 2200, scale = 1.18f, tint = new Color(0.86f, 0.8f, 0.72f) },
      new(){ name = "Fluyt",      speed = 8.2f, turn = 27f, reload = 3.8f, gunK = 0.8f,  hullK = 1.5f, hold = 120, complement = 90,  price = 2600, scale = 1.3f,  tint = new Color(0.95f, 0.86f, 0.66f) },
      new(){ name = "Frigate",    speed = 9.6f, turn = 30f, reload = 3.0f, gunK = 1.55f, hullK = 2.0f, hold = 70,  complement = 220, price = 5200, scale = 1.45f, tint = new Color(0.72f, 0.74f, 0.82f) },
      new(){ name = "Galleon",    speed = 7.4f, turn = 22f, reload = 3.6f, gunK = 1.8f,  hullK = 2.8f, hold = 160, complement = 300, price = 8600, scale = 1.7f,  tint = new Color(0.9f, 0.72f, 0.5f) },
    };
    public const int Sloop = 0, Brigantine = 1, Fluyt = 2, Frigate = 3, Galleon = 4;
  }

  /// <summary>One sailing ship: wind-driven speed, rudder, sail level, hull / sails (0..100), crew (absolute, up to the class
  /// complement), two broadside gun decks and sinking. Heading in degrees, 0 = +Z (screen up). Starboard = +X local.</summary>
  public class Ship : MonoBehaviour {
    public string label = "Ship";
    public float hull = 100, sails = 100, crew = 100;
    public float sail = 0.6f;          // 0 = furled, 1 = all sail set
    public float rudder;               // -1 port .. +1 starboard
    public float heading, speed;
    public float maxSpeed = 11f, turnRate = 40f, reloadBase = 3.2f;
    public float portReady, starReady; // Time.time when each broadside is loaded again
    public bool sinking, gone, mastDown; public float sinkT;
    public int cls; public ShipClass C => ShipClass.All[cls];
    public readonly List<Vector3> portGuns = new(), starGuns = new();   // gun muzzle points in ship-root space
    Transform model; GameObject modelSrc; readonly List<(Transform t, Vector3 s, int ax)> sailParts = new();
    float heel, bobSeed; Color tint = Color.white; Texture look;
    public float halfW = 2.9f, halfL = 7.2f, height = 14f;   // hull extents (root space, unscaled) for hits, spacing, the bow probe and labels

    public Vector3 Pos => transform.position;
    public Vector3 Fwd => transform.forward;
    public Vector3 Right => transform.right;
    public float Complement => C.complement;
    public float ReloadTime => reloadBase * (1f + 0.8f * (1f - Mathf.Clamp01(crew / Complement)));   // fewer hands, slower guns

    public void Init(GameObject modelPrefab, Material mat, string name){ label = name; bobSeed = Random.value * 10f; SetModel(modelPrefab, mat); }
    void SetModel(GameObject prefab, Material mat){
      if (model && modelSrc == prefab) return;
      if (model) Destroy(model.gameObject);
      sailParts.Clear(); portGuns.Clear(); starGuns.Clear(); modelSrc = prefab;
      model = Instantiate(prefab, transform).transform; model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity; model.localScale = Vector3.one;
      if (mat) foreach (var r in model.GetComponentsInChildren<Renderer>()){ var m = r.sharedMaterials; for (int i = 0; i < m.Length; i++) m[i] = mat; r.sharedMaterials = m; }
      var keep = transform.localScale; transform.localScale = Vector3.one;   // gun points in unscaled root space
      foreach (var t in model.GetComponentsInChildren<Transform>()){
        if (t.name.StartsWith("Sail_")){
          var up = t.InverseTransformDirection(model.up); up = new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z));
          sailParts.Add((t, t.localScale, up.x > up.y && up.x > up.z ? 0 : up.y > up.z ? 1 : 2));
        }
        else if (t.name.StartsWith("Gun_") && t.TryGetComponent<Renderer>(out var gr)){   // node origins sit at mid-hull, so use the mesh bounds
          var lp = transform.InverseTransformPoint(gr.bounds.center); (lp.x < 0 ? portGuns : starGuns).Add(lp);
        }
      }
      var b = new Bounds(transform.position, Vector3.zero); foreach (var r in model.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
      if (b.size.x > 0.5f){ var hullB = b; halfW = Mathf.Clamp(hullB.extents.x * 0.75f, 2.2f, 9f); halfL = Mathf.Clamp(hullB.extents.z * 0.85f, 5f, 24f); height = Mathf.Max(8f, hullB.max.y - transform.position.y); }
      transform.localScale = keep;
      if (portGuns.Count == 0 || starGuns.Count == 0){   // a model without Gun_ nodes: four guns a side along the hull
        Debug.LogWarning($"[SG] {label}: gun nodes missing, using hull points");
        portGuns.Clear(); starGuns.Clear(); for (int i = 0; i < 4; i++){ float z = -3f + i * 2f; portGuns.Add(new Vector3(-2.2f, 1.2f, z)); starGuns.Add(new Vector3(2.2f, 1.2f, z)); }
      }
      SetLook(look, tint);
    }
    /// <summary>Make this ship a given class: speed, turning, reload, its model if Art Dev has delivered one (else the sloop scaled).</summary>
    public void SetClass(int c, GameObject prefab, Material mat, bool placeholder){
      cls = Mathf.Clamp(c, 0, ShipClass.All.Length - 1); var k = C;
      maxSpeed = k.speed; turnRate = k.turn; reloadBase = k.reload;
      SetModel(prefab, mat);
      transform.localScale = Vector3.one * (placeholder ? k.scale : 1f);
    }

    /// <summary>Put the ship back afloat at a position and heading with full hull, sails and crew.</summary>
    public void Reset(Vector3 pos, float hdg, float sailLevel){
      gameObject.SetActive(true); sinking = gone = mastDown = false; sinkT = 0f;
      hull = sails = 100f; crew = Complement; sail = sailLevel; rudder = 0f; speed = 0f; heading = hdg; portReady = starReady = 0f;
      transform.SetPositionAndRotation(new Vector3(pos.x, 0f, pos.z), Quaternion.Euler(0f, hdg, 0f));
    }

    /// <summary>Take the ship off the sea (no encounter).</summary>
    public void Hide(){ gone = true; sinking = false; gameObject.SetActive(false); }
    /// <summary>Per-ship tint over the shared material (nation and class tints until each class has its own art).</summary>
    public void SetTint(Color c) => SetLook(look, c);
    /// <summary>M3: the albedo for this ship's nation (Art Dev's baked variants: nation trim, ensign, tarred pirate sails) and a tint.</summary>
    public void SetLook(Texture albedo, Color c){
      tint = c; look = albedo; var mpb = new MaterialPropertyBlock(); mpb.SetColor("_Tint", c); if (albedo) mpb.SetTexture("_MainTex", albedo);
      foreach (var r in GetComponentsInChildren<Renderer>(true)) r.SetPropertyBlock(mpb);
    }

    /// <summary>Point-of-sail efficiency. off = angle between the bow and where the wind comes FROM (0 = head to wind, 180 = dead run).</summary>
    public static float WindFactor(float off){
      off = Mathf.Abs(off);
      if (off < 30f) return 0.12f;                                    // in irons: barely drifting
      if (off < 45f) return Mathf.Lerp(0.12f, 0.65f, (off - 30f) / 15f);   // close-hauled
      if (off < 100f) return Mathf.Lerp(0.65f, 1f, (off - 45f) / 55f);     // reaching, best on a beam / broad reach
      return Mathf.Lerp(1f, 0.78f, (off - 100f) / 80f);                    // running downwind
    }
    public static string PointOfSail(float off){
      off = Mathf.Abs(off);
      return off < 30f ? "In irons" : off < 60f ? "Close-hauled" : off < 110f ? "Beam reach" : off < 150f ? "Broad reach" : "Running";
    }
    public float OffWind(float windFrom) => Mathf.DeltaAngle(heading, windFrom);

    public void Tick(float dt, float windFrom){
      if (gone) return;
      if (sinking){
        sinkT += dt; speed = Mathf.MoveTowards(speed, 0f, 2f * dt);
        transform.position += Fwd * speed * dt;
        model.localPosition = new Vector3(0f, -Mathf.Pow(sinkT, 1.5f) * 0.6f, 0f);
        model.localRotation = Quaternion.Euler(-Mathf.Min(sinkT * 5f, 22f), 0f, Mathf.Min(sinkT * 9f, 38f));
        if (sinkT > 7f){ gone = true; gameObject.SetActive(false); }
        return;
      }
      float target = maxSpeed * WindFactor(OffWind(windFrom)) * sail * (0.45f + 0.55f * sails / 100f) * (mastDown ? 0.75f : 1f);
      speed = Mathf.MoveTowards(speed, target, (target > speed ? 1.3f : 1.8f) * dt);
      heading = Mathf.Repeat(heading + rudder * turnRate * (0.35f + 0.65f * Mathf.Clamp01(speed / 6f)) * dt, 360f);
      transform.rotation = Quaternion.Euler(0f, heading, 0f);
      transform.position += Fwd * speed * dt;
      // painted look: gentle bob, heel away from the wind and into turns, sails shortened with the sail level
      float side = Mathf.Sign(OffWind(windFrom));
      heel = Mathf.Lerp(heel, side * 7f * sail * Mathf.Clamp01(speed / maxSpeed) - rudder * 4f * Mathf.Clamp01(speed / 6f), dt * 2f);
      float t = Time.time + bobSeed;
      model.localPosition = new Vector3(0f, Mathf.Sin(t * 1.3f) * 0.12f, 0f);
      model.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.8f) * 1.5f, 0f, heel + Mathf.Sin(t * 1.1f) * 2f);
      float k = sail < 0.03f ? 0.0001f : Mathf.Lerp(0.35f, 1f, sail);   // reefed sails shrink toward the deck (sail pivots sit at the waterline)
      foreach (var (st, s, ax) in sailParts){ var v = s; v[ax] *= k; st.localScale = v; }
    }
  }
}

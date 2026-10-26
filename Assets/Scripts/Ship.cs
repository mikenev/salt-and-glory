using System.Collections.Generic;
using UnityEngine;
namespace SaltGlory {
  public enum Shot { Round, Chain, Grape }

  /// <summary>One sailing ship: wind-driven speed, rudder, sail level, hull / sails / crew, two broadside gun decks and sinking.
  /// Heading is in degrees, 0 = +Z (screen up), 90 = +X. Starboard = +X local (bow faces +Z).</summary>
  public class Ship : MonoBehaviour {
    public string label = "Ship";
    public float hull = 100, sails = 100, crew = 100;
    public float sail = 0.6f;          // 0 = furled, 1 = all sail set
    public float rudder;               // -1 port .. +1 starboard
    public float heading, speed;
    public float maxSpeed = 11f, turnRate = 40f, reloadBase = 3.2f;
    public float portReady, starReady; // Time.time when each broadside is loaded again
    public bool sinking, gone; public float sinkT;
    public readonly List<Vector3> portGuns = new(), starGuns = new();   // gun muzzle points in ship-root space
    Transform model; readonly List<(Transform t, Vector3 s, int ax)> sailParts = new();   // ax = the local axis that points up in the model
    float heel, bobSeed;

    public Vector3 Pos => transform.position;
    public Vector3 Fwd => transform.forward;
    public Vector3 Right => transform.right;
    public float ReloadTime => reloadBase * (1f + 0.8f * (1f - crew / 100f));   // fewer hands, slower guns

    public void Init(GameObject modelPrefab, Material mat, string name){
      label = name;
      model = Instantiate(modelPrefab, transform).transform; model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity;
      foreach (var r in model.GetComponentsInChildren<Renderer>()){ var m = r.sharedMaterials; for (int i = 0; i < m.Length; i++) m[i] = mat; r.sharedMaterials = m; }
      foreach (var t in model.GetComponentsInChildren<Transform>()){
        if (t.name.StartsWith("Sail_")){
          var up = t.InverseTransformDirection(model.up); up = new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z));
          sailParts.Add((t, t.localScale, up.x > up.y && up.x > up.z ? 0 : up.y > up.z ? 1 : 2));
        }
        else if (t.name.StartsWith("Gun_") && t.TryGetComponent<Renderer>(out var gr)){   // node origins sit at mid-hull, so use the mesh bounds
          var lp = transform.InverseTransformPoint(gr.bounds.center); (lp.x < 0 ? portGuns : starGuns).Add(lp);
        }
      }
      bobSeed = Random.value * 10f;
      if (portGuns.Count == 0 || starGuns.Count == 0) Debug.LogWarning($"[SG] {name}: gun nodes missing (port {portGuns.Count}, starboard {starGuns.Count})");
    }

    /// <summary>Put the ship back afloat at a position and heading with full hull, sails and crew.</summary>
    public void Reset(Vector3 pos, float hdg, float sailLevel){
      gameObject.SetActive(true); sinking = gone = false; sinkT = 0f;
      hull = sails = crew = 100f; sail = sailLevel; rudder = 0f; speed = 0f; heading = hdg; portReady = starReady = 0f;
      transform.SetPositionAndRotation(new Vector3(pos.x, 0f, pos.z), Quaternion.Euler(0f, hdg, 0f));
    }

    /// <summary>Take the ship off the sea (no encounter).</summary>
    public void Hide(){ gone = true; sinking = false; gameObject.SetActive(false); }
    /// <summary>Per-ship tint over the shared material (ships met at sea are tinted by nation until each nation has its own ship art).</summary>
    public void SetTint(Color c){
      var mpb = new MaterialPropertyBlock(); mpb.SetColor("_Tint", c);
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
      float target = maxSpeed * WindFactor(OffWind(windFrom)) * sail * (0.45f + 0.55f * sails / 100f);
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

using System.Runtime.InteropServices;
using UnityEngine;
namespace SaltGlory {
  /// <summary>Everything that persists in a save slot. Reputation is -100..100 per nation; at or below Hostile a nation's
  /// ports refuse entry and its warships attack on sight.</summary>
  [System.Serializable]
  public class Career {
    public const int Hostile = -25;
    public int gold = 600, victories, lastPort, marque = -1;   // marque = patron nation index, -1 none
    public int[] cargo = new int[5], rep = { 10, -10, 0, 0 };
    public float x, z, heading = 150f, hull = 100, sails = 100, crew = 70;
    public int missionFrom = -1, missionTarget = -1, missionReward; public bool missionDone;   // "sink one <target> ship" for <from>
    public string savedAt = "", where = "";
    // M3 (old M2 saves load with these defaults: a sloop, no fame, no rank, no map pieces, prices at rest)
    public int shipClass, fame, captures, duelsWon, treasures, missionsDone, mapPieces;
    public int[] service = new int[4], rankSeen = new int[4];   // service under a letter of marque, per nation; the rank each governor last announced
    public bool prize;   // M4: sailing a captured ship (worth less in trade)
    public bool digActive; public float digX, digZ; public string digNear = "";
    public float[] market = new float[0];   // per port x good price pressure: + after buying, - after selling, drifts back to 0
    public static readonly string[] Ranks = { "Ensign", "Lieutenant", "Captain", "Commodore", "Admiral" };   // matches Art Dev's rank icons
    static readonly int[] RankAt = { 0, 25, 60, 120, 200 };
    /// <summary>Rank index under a nation (0 Ensign .. 4 Admiral), or -1 if you have never served it under a letter of marque.</summary>
    public int Rank(int n){ if (marque != n && service[n] <= 0) return -1; int r = 0; for (int i = 0; i < RankAt.Length; i++) if (service[n] >= RankAt[i]) r = i; return r; }
    public int NextRankAt(int n){ int r = Mathf.Max(0, Rank(n)); return r + 1 < RankAt.Length ? RankAt[r + 1] : -1; }
    public void Serve(int pts){ if (marque >= 0) service[marque] += pts; }
    public static string FameTitle(int f) => f < 20 ? "Unknown" : f < 60 ? "Promising" : f < 150 ? "Renowned" : f < 300 ? "Famous" : "Legendary";
    public void FixUp(int ports){
      if (cargo == null || cargo.Length != 5) cargo = new int[5];
      if (rep == null || rep.Length != 4) rep = new[]{ 0, 0, 0, 0 };
      if (service == null || service.Length != 4) service = new int[4];
      if (rankSeen == null || rankSeen.Length != 4) rankSeen = new int[4];
      if (market == null || market.Length != ports * 5) market = new float[ports * 5];
      shipClass = Mathf.Clamp(shipClass, 0, ShipClass.All.Length - 1); digNear ??= "";
    }
    public int HoldMax => ShipClass.All[Mathf.Clamp(shipClass, 0, ShipClass.All.Length - 1)].hold;
    public int Hold { get { int n = 0; foreach (var c in cargo) n += c; return n; } }
    public bool IsHostile(Nation n) => rep[(int)n] <= Hostile;
    public void AddRep(Nation n, int d){ rep[(int)n] = Mathf.Clamp(rep[(int)n] + d, -100, 100); }
    public static string Standing(int r) => r <= Hostile ? "Hostile" : r < 0 ? "Wary" : r < 30 ? "Neutral" : r < 60 ? "Friendly" : "Honoured";
  }

  /// <summary>3 browser save slots in localStorage (jslib on WebGL, PlayerPrefs in the editor).</summary>
  public static class Saves {
    public const int Slots = 3;
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern int SG_SaveSet(string k, string v);
    [DllImport("__Internal")] static extern string SG_SaveGet(string k);
    static void Set(string k, string v) => SG_SaveSet(k, v);
    static string Get(string k) => SG_SaveGet(k);
#else
    static void Set(string k, string v){ PlayerPrefs.SetString(k, v); PlayerPrefs.Save(); }
    static string Get(string k) => PlayerPrefs.GetString(k, "");
#endif
    static string Key(int slot) => "saltglory.slot" + (slot + 1);
    public static void Save(int slot, Career c){
      c.savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); Set(Key(slot), JsonUtility.ToJson(c));
    }
    public static Career Load(int slot){
      var s = Get(Key(slot)); if (string.IsNullOrEmpty(s)) return null;
      try { var c = JsonUtility.FromJson<Career>(s); c.FixUp(World.Ports.Count); return c; }
      catch { return null; }
    }
    /// <summary>The most recently saved slot, or -1.</summary>
    public static int Latest(){
      int best = -1; string t = "";
      for (int i = 0; i < Slots; i++){ var c = Load(i); if (c != null && string.CompareOrdinal(c.savedAt, t) > 0){ t = c.savedAt; best = i; } }
      return best;
    }
  }
}

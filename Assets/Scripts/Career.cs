using System.Runtime.InteropServices;
using UnityEngine;
namespace SaltGlory {
  /// <summary>Everything that persists in a save slot. Reputation is -100..100 per nation; at or below Hostile a nation's
  /// ports refuse entry and its warships attack on sight.</summary>
  [System.Serializable]
  public class Career {
    public const int Hostile = -25, HoldSize = 40, MaxCrew = 100;
    public int gold = 600, victories, lastPort, marque = -1;   // marque = patron nation index, -1 none
    public int[] cargo = new int[5], rep = { 10, -10, 0, 0 };
    public float x, z, heading = 150f, hull = 100, sails = 100, crew = 70;
    public int missionFrom = -1, missionTarget = -1, missionReward; public bool missionDone;   // "sink one <target> ship" for <from>
    public string savedAt = "", where = "";
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
      try { var c = JsonUtility.FromJson<Career>(s); if (c.cargo == null || c.cargo.Length != 5) c.cargo = new int[5]; if (c.rep == null || c.rep.Length != 4) c.rep = new[]{ 0, 0, 0, 0 }; return c; }
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

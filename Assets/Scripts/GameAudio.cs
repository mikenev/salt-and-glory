using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace SaltGlory {
  /// <summary>Music, ambient beds and one-shot SFX (GameAudio pattern, as in Neon Hive): the sfx source stays at volume 1
  /// (one-shots scale by PlayOneShot volume), nothing plays until the first key / click / touch / gamepad button (WebGL autoplay),
  /// a per-id minimum gap, and a voice cap (MaxSame of one clip, MaxTotal at once) so a broadside exchange doesn't clip.
  /// M2: sea or town music, the wave bed at sea and the tavern chatter bed in the tavern. M3: battle music while a fight is on,
  /// the duel loop with a quiet timber-creak bed on deck, and the duel / treasure SFX. Missing clips are skipped.</summary>
  public class GameAudio : MonoBehaviour {
    public static GameAudio I;
    public AudioClip music, townMusic, waves, chatter, cannon, hullHit, splash, coins, door, bell, mapUnroll, sailsFilling;
    public AudioClip battleMusic, duelMusic, creak, parry, whoosh, bodyHit, overboard, mastCrack, cheer, grapple, dig, chest;
    public AudioClip[] clash = new AudioClip[0];
    [Range(0, 1)] public float musicVolume = 0.45f, wavesVolume = 0.7f, chatterVolume = 0.5f, creakVolume = 0.6f, sfxVolume = 0.85f;
    const int MaxSame = 4, MaxTotal = 8;
    public static bool Unlocked { get; private set; }
    AudioSource mus, amb, bed, sfx, low;   // "low" plays pitched one-shots so pitch changes don't bend the others
    struct Rule { public AudioClip clip; public float vol, gap, pitch; }
    readonly Dictionary<string, Rule> rules = new(); readonly Dictionary<string, float> last = new();
    readonly List<(float end, AudioClip clip)> voices = new();
    bool inTown, inTavern, inBattle, inDuel;

    void Awake(){
      I = this;
      AudioSource Src(bool loop){ var s = gameObject.AddComponent<AudioSource>(); s.loop = loop; s.playOnAwake = false; s.volume = 1f; return s; }
      mus = Src(true); mus.clip = music; mus.volume = musicVolume;
      amb = Src(true); amb.clip = waves; amb.volume = wavesVolume;
      bed = Src(true); bed.clip = chatter; bed.volume = chatterVolume;
      sfx = Src(false); low = Src(false); low.pitch = 0.62f;
      void R(string id, AudioClip c, float v, float g, float p = 1f){ rules[id] = new Rule{ clip = c, vol = v, gap = g, pitch = p }; }
      R("cannon", cannon, 0.8f, 0.07f);
      R("enemyCannon", cannon, 0.55f, 0.07f);
      if (hullHit) R("hit", hullHit, 0.7f, 0.1f); else R("hit", cannon, 0.45f, 0.12f, 0.62f);   // fallback: the cannon pitched down
      R("splash", splash, 0.5f, 0.12f);
      R("coins", coins, 0.8f, 0.08f);
      R("door", door, 0.8f, 0.3f);
      R("bell", bell, 0.6f, 0.5f);
      R("map", mapUnroll, 0.7f, 0.3f);
      R("sails", sailsFilling, 0.7f, 0.5f);
      R("parry", parry, 0.8f, 0.1f); R("whoosh", whoosh, 0.6f, 0.08f); R("body", bodyHit, 0.8f, 0.1f); R("overboard", overboard, 0.9f, 0.5f);
      R("mast", mastCrack, 0.8f, 0.5f); R("cheer", cheer, 0.8f, 1f); R("grapple", grapple, 0.85f, 0.5f); R("dig", dig, 0.8f, 0.3f); R("chest", chest, 0.85f, 0.5f);
      for (int i = 0; i < clash.Length; i++) R("clash" + i, clash[i], 0.75f, 0.08f);
    }
    static bool AnyInput(){
      var kb = Keyboard.current; if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
      var m = Mouse.current; if (m != null && m.leftButton.wasPressedThisFrame) return true;
      var t = Touchscreen.current; if (t != null && t.primaryTouch.press.wasPressedThisFrame) return true;
      var g = Gamepad.current; return g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame);
    }
    void Update(){
      if (Unlocked || !AnyInput()) return;
      Unlocked = true; Apply();
      Debug.Log("[Audio] unlocked on first input");
    }
    /// <summary>Town (town loop, tavern chatter in the tavern) or sea (sailing loop and the wave bed).</summary>
    public void SetScene(bool town, bool tavern){ inTown = town; inTavern = town && tavern; inDuel = false; if (Unlocked) Apply(); }
    /// <summary>Battle music while a fight is on at sea (falls back to the sailing loop).</summary>
    public void SetBattle(bool on){ if (inBattle == on) return; inBattle = on; if (Unlocked) Apply(); }
    /// <summary>On deck for a duel: the duel loop and the quiet timber-creak bed; the wave bed carries on underneath.</summary>
    public void SetDuel(bool on){ inDuel = on; if (Unlocked) Apply(); }
    void Apply(){
      var want = inDuel && duelMusic ? duelMusic : inTown && townMusic ? townMusic : !inTown && inBattle && battleMusic ? battleMusic : music;
      if (mus.clip != want || !mus.isPlaying){ mus.clip = want; if (want) mus.Play(); }
      if (inTown) amb.Stop(); else if (amb.clip && !amb.isPlaying) amb.Play();
      var bedClip = inDuel ? creak : inTavern ? chatter : null;
      if (bedClip){ if (bed.clip != bedClip || !bed.isPlaying){ bed.clip = bedClip; bed.volume = inDuel ? creakVolume : chatterVolume; bed.Play(); } } else bed.Stop();
    }
    /// <summary>One of the sword clashes at random.</summary>
    public void Clash(){ if (clash.Length > 0) Play("clash" + Random.Range(0, clash.Length)); }
    /// <summary>Play a one-shot; vol01 scales it further (e.g. distance falloff for the enemy's guns).</summary>
    public void Play(string id, float vol01 = 1f){
      if (!Unlocked || !rules.TryGetValue(id, out var r) || !r.clip || vol01 <= 0.01f) return;
      float now = Time.unscaledTime;
      if (last.TryGetValue(id, out var t) && now - t < r.gap) return;
      voices.RemoveAll(v => v.end <= now);
      int same = 0; foreach (var v in voices) if (v.clip == r.clip) same++;
      if (same >= MaxSame || voices.Count >= MaxTotal) return;
      voices.Add((now + r.clip.length / r.pitch, r.clip)); last[id] = now;
      (r.pitch < 1f ? low : sfx).PlayOneShot(r.clip, r.vol * vol01 * sfxVolume);
    }
  }
}

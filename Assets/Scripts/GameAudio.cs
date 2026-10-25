using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace SaltGlory {
  /// <summary>Music, ambient wave bed and one-shot SFX (GameAudio pattern, as in Neon Hive): the sfx source stays at volume 1
  /// (one-shots scale by PlayOneShot volume), nothing plays until the first key / click / touch / gamepad button (WebGL autoplay),
  /// a per-id minimum gap, and a voice cap (MaxSame of one clip, MaxTotal at once) so a broadside exchange doesn't clip.</summary>
  public class GameAudio : MonoBehaviour {
    public static GameAudio I;
    public AudioClip music, waves, cannon;
    [Range(0, 1)] public float musicVolume = 0.45f, wavesVolume = 0.7f, sfxVolume = 0.85f;
    const int MaxSame = 4, MaxTotal = 8;
    public static bool Unlocked { get; private set; }
    AudioSource mus, amb, sfx, low;   // "low" plays pitched-down cannon one-shots (hits), so pitch changes don't bend the shots
    struct Rule { public AudioClip clip; public float vol, gap, pitch; }
    readonly Dictionary<string, Rule> rules = new(); readonly Dictionary<string, float> last = new();
    readonly List<(float end, AudioClip clip)> voices = new();

    void Awake(){
      I = this;
      AudioSource Src(bool loop){ var s = gameObject.AddComponent<AudioSource>(); s.loop = loop; s.playOnAwake = false; s.volume = 1f; return s; }
      mus = Src(true); mus.clip = music; mus.volume = musicVolume;
      amb = Src(true); amb.clip = waves; amb.volume = wavesVolume;
      sfx = Src(false); low = Src(false); low.pitch = 0.62f;
      void R(string id, AudioClip c, float v, float g, float p = 1f){ rules[id] = new Rule{ clip = c, vol = v, gap = g, pitch = p }; }
      R("cannon", cannon, 0.8f, 0.07f);
      R("enemyCannon", cannon, 0.55f, 0.07f);
      R("hit", cannon, 0.45f, 0.12f, 0.62f);   // placeholder hull-hit thud: the cannon shot pitched down
    }
    static bool AnyInput(){
      var kb = Keyboard.current; if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
      var m = Mouse.current; if (m != null && m.leftButton.wasPressedThisFrame) return true;
      var t = Touchscreen.current; if (t != null && t.primaryTouch.press.wasPressedThisFrame) return true;
      var g = Gamepad.current; return g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame);
    }
    void Update(){
      if (Unlocked || !AnyInput()) return;
      Unlocked = true; if (mus.clip) mus.Play(); if (amb.clip) amb.Play();
      Debug.Log("[Audio] unlocked on first input");
    }
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

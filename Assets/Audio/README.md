# Salt and Glory: audio sample pack

This is original procedural synthesis (oscillators, filtered noise, synthetic membranes). It uses no samples, and it doesn't quote any existing shanty or melody.
Format: OGG Vorbis, 44.1 kHz. Peaks are measured on the decoded files, and nothing clips.

| File | Length | Peak dBFS | Loop | Notes |
|---|---|---|---|---|
| `Music/sailing_loop.ogg` | 35.56 s (32 bars) | -3.0 | yes, seamless | Stereo. **108 BPM** (dotted quarter) in 6/8, A Dorian. Concertina, fiddle, boot stomps, bodhran-style drum, claps, drone, and a synthetic "oh" choir |
| `SFX/ocean_waves_loop.ogg` | 12.00 s | -11.9 | yes, seamless | Mono ambient bed, deliberately quieter than the other SFX |
| `SFX/cannon_shot.ogg` | 1.60 s | -6.0 | no | Mono one-shot |
| `SFX/sword_clash.ogg` | 0.90 s | -6.3 | no | Mono one-shot, two-hit clash with a blade scrape |

Looping is set on the AudioSource (`loop = true`), not in the importer.
`Previews/sailing_loop.mp3` (repo root, outside Assets) is a review copy only. MP3 adds encoder padding, so it isn't gapless; use the OGG in game.

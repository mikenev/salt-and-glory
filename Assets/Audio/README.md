# Salt and Glory: audio sample pack

This is original procedural synthesis (oscillators, filtered noise, synthetic membranes). It uses no samples, and it doesn't quote any existing shanty or melody.
Format: OGG Vorbis, 44.1 kHz. Peaks are measured on the decoded files, and nothing clips.

| File | Length | Peak dBFS | Loop | Notes |
|---|---|---|---|---|
| `Music/sailing_loop.ogg` | 35.56 s (32 bars) | -3.0 | yes, seamless | Stereo. **108 BPM** (dotted quarter) in 6/8, A Dorian. Concertina, fiddle, boot stomps, bodhran-style drum, claps, drone, and a synthetic "oh" choir |
| `SFX/ocean_waves_loop.ogg` | 12.00 s | -11.9 | yes, seamless | Mono ambient bed, deliberately quieter than the other SFX |
| `SFX/cannon_shot.ogg` | 1.60 s | -6.0 | no | Mono one-shot |
| `SFX/sword_clash.ogg` | 0.90 s | -6.3 | no | Mono one-shot, two-hit clash with a blade scrape |
| `Music/town_loop.ogg` | 32.73 s (24 bars) | -3.0 | yes, seamless | Stereo. **132 BPM** in 3/4, D major with a Mixolydian C natural. Plucked guitar (bass and strums), tin whistle, and a soft low drum. The tune was written for this game |
| `SFX/tavern_chatter_loop.ogg` | 10.00 s | -14.0 | yes, seamless | Mono bed, deliberately quieter. Wordless synthetic babble from 9 talkers plus mug clinks, muffled |
| `SFX/cannonball_splash.ogg` | 1.50 s | -5.7 | no | Incoming whistle, splash, droplets |
| `SFX/hull_hit.ogg` | 0.76 s | -6.2 | no | Wooden crunch and splinters, high-passed so it has no boom (unlike `cannon_shot`) |
| `SFX/coins.ogg` | 0.78 s | -5.9 | no | Coin jingle |
| `SFX/ships_bell.ogg` | 3.20 s | -5.9 | no | Two strikes |
| `SFX/door.ogg` | 1.30 s | -5.9 | no | Hinge creak, thud, latch |
| `SFX/map_unroll.ogg` | 1.00 s | -6.1 | no | Paper rustle, then it lands flat |
| `SFX/sails_filling.ogg` | 1.55 s | -6.0 | no | Air swell, the canvas snaps taut, rope creak |
| `Music/battle_loop.ogg` | 38.10 s (40 bars) | -3.1 | yes, seamless | Stereo. **126 BPM** (dotted quarter) in 6/8, E minor. A fast jig-pulse with war drums, snare, claps, brass stabs and a brass lead, joined by fiddle in the second half |
| `Music/duel_loop.ogg` | 37.89 s (24 bars) | -3.0 | yes, seamless | Stereo. **152 BPM** in 4/4, D minor with a Phrygian E flat. Quick 16th-note spiccato strings, stamps, claps, and a tense high fiddle line |
| `SFX/sword_clash_02.ogg` | 1.20 s | -6.1 | no | Single heavy, lower-pitched clash with a scrape |
| `SFX/sword_clash_03.ogg` | 1.20 s | -6.3 | no | Three quick, bright exchanges |
| `SFX/sword_clash_04.ogg` | 1.20 s | -6.2 | no | Hit with a long blade-on-blade slide |
| `SFX/parry_ring.ogg` | 1.40 s | -5.9 | no | Clean ringing parry |
| `SFX/sword_whoosh.ogg` | 0.38 s | -6.0 | no | Blade swing |
| `SFX/body_hit.ogg` | 0.34 s | -6.0 | no | Thud and cloth slap |
| `SFX/splash_overboard.ogg` | 1.28 s | -6.1 | no | Body plunge with bubbles |
| `SFX/mast_crack.ogg` | 2.39 s | -6.0 | no | Splitting cracks, a groan, the crash onto deck, splinters |
| `SFX/timber_creak_loop.ogg` | 14.00 s | -14.0 | yes, seamless | Mono bed, deliberately quieter. Irregular hull creaks over a low swaying groan |
| `SFX/crew_cheer.ogg` | 2.00 s | -6.1 | no | Wordless synthetic crowd "ah/ey" with claps |
| `SFX/grappling_hooks.ogg` | 1.50 s | -6.1 | no | Two rope throws, iron bites, rope pulls taut |
| `SFX/digging_shovel.ogg` | 1.50 s | -6.1 | no | Two shovel strikes with dirt toss |
| `SFX/chest_open.ogg` | 1.40 s | -6.0 | no | Latch, hinge creak, lid rests open |

Looping is set on the AudioSource (`loop = true`), not in the importer.
`Previews/*.mp3` (repo root, outside Assets: sailing, town, battle and duel loops) are review copies only. MP3 adds encoder padding, so they aren't gapless; use the OGG in game.
`.meta` files: music is Streaming (M2 and M3 music also load in the background), SFX and the short loop beds are DecompressOnLoad, and `normalize` is off on every clip so the authored levels hold. The sample-pack metas were updated to match in PR #4 (normalize off, sailing_loop Streaming).

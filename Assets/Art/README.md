# Salt and Glory: art sample pack (Checkpoint 2)

Painterly storybook look: soft brushwork, warm parchment light, turquoise Caribbean water. All art is original; no existing IP, flags or logos.

| Piece | Path | Notes |
|---|---|---|
| Style frame: open sea | `StyleFrames/SG_StyleFrame_OpenSea.png` | 1280×720. Small gaff-rigged boat, golden afternoon, palm island. Mood target for sailing. |
| Style frame: port town | `StyleFrames/SG_StyleFrame_PortTown.png` | 1280×720. Spanish-colonial harbor with fort, docks and two moored ships. Mood target for ports and the world map. |
| Style frame: ship battle | `StyleFrames/SG_StyleFrame_ShipBattle.png` | 1280×720. Tilted top-down view: sloop trading broadsides with a galleon. Camera and mood target for battles. |
| Sloop model | `Ships/Sloop/Sloop.fbx` + `Ships/Sloop/Sloop_Albedo.png` | Starter ship. ~2.4k tris, 1 material, 1 hand-painted 1024² texture with baked painted shadows and ambient occlusion, warm light and cool-violet shadows, and painted edge lines. Plank detail on the hull and deck; cap rails, wales, keel and stem, crosstrees, shrouds, forestay, sheet and halyards. Real-world scale (~12 m hull, ~17 m to masthead), waterline at y = 0, origin at mid-hull, bow faces +Z. Separate child meshes (hull, deck, spars, Sail_Main, Sail_Jib, pennant, 6 guns, rails, rigging, rudder) so sails can be scaled or hidden. Lighting is baked into the texture, so it reads best with soft scene light or an unlit/toon shader. |
| Town screen: tavern | `Towns/SG_Town_Tavern.png` | 1280×720 painted background (imported as Sprite, no resize), no people or text; calm floor area at the bottom for UI panels. |

Sample only: the full ship set, other town screens, map and characters wait for Mike's notes.

Unity import checked in a throwaway Unity 6000.3.26f1 project: FBX and PNGs import with no errors. The `.meta` files are committed (style frames and tavern are Sprites with NPOT scaling off). The sloop material came in on the Built-in Standard shader there; in a URP project, remap it to URP Lit.

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

## M2: map, towns, UI and chain shot

All textures below import as Sprites (no mipmaps, no NPOT resize). Every `.meta` file is committed. Nothing has baked-in text.

### Map (`Map/`)
| Piece | Path | Notes |
|---|---|---|
| World map | `SG_Map_Caribbean.png` | 2560×1600 hand-drawn chart on the shared parchment. Real coastlines come from Natural Earth 10 m land (public domain), painted with ink outlines, offshore ripple lines, painted hill marks, a compass rose with rhumb lines and a degree-tick border. It has no labels. Covers lon −98…−58 and lat ≈6.8…30.5 (Gulf of Mexico to Barbados, Florida to Panama). Max import size is 4096. |
| Port coordinates | `SG_Map_Ports.json` | The 11 ports from the plan with nation, lat/lon, pixel position (origin top-left) and UV (origin bottom-left). Data only, not code. |
| Port marker | `SG_Map_PortMarker.png` | 128² parchment disc with a small fort glyph. |
| Port marker ring | `SG_Map_PortMarker_Ring.png` | 128² white ring that sits behind the marker. Tint it with the nation colour, for example England red, Spain gold, France blue, Dutch orange. |
| Ship marker | `SG_Map_ShipMarker.png` | 128² top-down sloop with the bow pointing up (+Y). Rotate it to the heading. |

### Towns (`Towns/`), all 1280×720 with a calm strip across the bottom fifth for UI
| Piece | Path | Notes |
|---|---|---|
| Harbor: England | `SG_Harbor_England.png` | Brick and timber warehouses, a gun battery, deep-red doors and shutters. |
| Harbor: Spain | `SG_Harbor_Spain.png` | Stucco arcades, a stone bastion and watchtower, crimson and gold awnings. |
| Harbor: France | `SG_Harbor_France.png` | Rustic wooden jetty town, a hill fort, royal-blue shutters. |
| Harbor: Dutch Republic | `SG_Harbor_Dutch.png` | Pastel gabled merchant houses, a squat fort, orange doors. |
| Governor's office | `SG_Town_Governor.png` | Desk, globe, chart and window to the harbor, plus an empty flagstand for the nation flag. |
| Merchant | `SG_Town_Merchant.png` | Scales, ledger, coins and shelves of sugar, rum, tobacco, cloth and spices. |
| Shipwright | `SG_Town_Shipwright.png` | Hull on a slipway, timber, tools and a spar on trestles. |
| Tavern | `SG_Town_Tavern.png` | The approved tavern from the sample pack, unchanged. |
| Flags | `Flags/SG_Flag_{England,Spain,France,Dutch}.png` | 128×91 waving flags with the pivot at the top-left (hoist). Designs: England, St George's cross; Spain, Cross of Burgundy; France, royal white with gold fleurs-de-lis; Dutch Republic, red-white-blue Statenvlag. Every harbor and the governor's room has a bare flagpole, so the game can place any flag there to re-dress a port or show who holds it. |
| Flag anchors | `SG_Town_FlagAnchors.json` | Pixel hoist position and suggested scale for the flag on each harbor and the governor's office. |

### UI (`UI/`)
| Piece | Path | Notes |
|---|---|---|
| Parchment panel | `SG_UI_Panel_Parchment.png` | 512², 9-slice border 64 px on all sides (set in the .meta, Full Rect). Burnt edges, an ink double rule and red corner diamonds sit inside the border; the centre is plain parchment that stretches cleanly. |
| Buttons | `SG_UI_Button_Normal.png`, `SG_UI_Button_Highlighted.png` | 256×80, 9-slice border 24 px on all sides. The highlighted state is warmer and brighter with a gold inner rule. |
| Coin | `SG_Icon_Coin.png` | 256² gold coin with a compass-star face, no frame. |
| Cargo icons | `SG_Icon_Cargo_{Sugar,Rum,Tobacco,Cloth,Spices}.png` | 256² on the shared parchment medallion frame. |
| Nation flag icons | `SG_Icon_Flag_{England,Spain,France,Dutch}.png` | 256², the same flag art on a short pole, in the same medallion frame. |

### Projectiles (`Projectiles/`)
| Piece | Path | Notes |
|---|---|---|
| Chain shot | `SG_Shot_Chain.png` | 256×128 top-down: two iron balls joined by chunky chain links, with a warm halo so it reads on dark sea. Spin it around its centre in flight. |

All of M2 shares one parchment texture: map, panel, buttons, medallions and marker discs.

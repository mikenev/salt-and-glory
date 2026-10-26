# Salt and Glory: art sample pack (Checkpoint 2)

Painterly storybook look: soft brushwork, warm parchment light, turquoise Caribbean water. All art is original; no existing IP, flags or logos.

| Piece | Path | Notes |
|---|---|---|
| Style frame: open sea | `StyleFrames/SG_StyleFrame_OpenSea.png` | 1280×720. Small gaff-rigged boat, golden afternoon, palm island. Mood target for sailing. |
| Style frame: port town | `StyleFrames/SG_StyleFrame_PortTown.png` | 1280×720. Spanish-colonial harbor with fort, docks and two moored ships. Mood target for ports and the world map. |
| Style frame: ship battle | `StyleFrames/SG_StyleFrame_ShipBattle.png` | 1280×720. Tilted top-down view: sloop trading broadsides with a galleon. Camera and mood target for battles. |
| Sloop model | `Ships/Sloop/Sloop.fbx` + `Ships/Sloop/Sloop_Albedo.png` | Starter ship. ~2.4k tris, 1 material, 1 hand-painted 1024² texture with baked painted shadows and ambient occlusion, warm light and cool-violet shadows, and painted edge lines. Plank detail on the hull and deck; cap rails, wales, keel and stem, crosstrees, shrouds, forestay, sheet and halyards. Real-world scale (~12 m hull, ~17 m to masthead), waterline at y = 0, origin at mid-hull, bow faces +Z. Separate child meshes (hull, deck, spars, Sail_Main, Sail_Jib, pennant, 6 guns, rails, rigging, rudder) so sails can be scaled or hidden. Lighting is baked into the texture, so it reads best with soft scene light or an unlit/toon shader. |
| Town screen: tavern | `Towns/SG_Town_Tavern.png` | 1280×720 painted background (imported as Sprite, no resize), no people or text; calm floor area at the bottom for UI panels. |

The sample pack above was approved at Checkpoint 2. The M2 set is below.

Unity import checked in a throwaway Unity 6000.3.26f1 project: FBX and PNGs import with no errors. The `.meta` files are committed (style frames and tavern are Sprites with NPOT scaling off). The sloop material came in on the Built-in Standard shader there; in a URP project, remap it to URP Lit.

## M2: map, towns, UI and chain shot

All textures below import as Sprites (no mipmaps, no NPOT resize). Every `.meta` file is committed. Nothing has baked-in text.

### Map (`Map/`)
| Piece | Path | Notes |
|---|---|---|
| World map | `SG_Map_Caribbean.png` | 2560×1600 hand-drawn chart on the shared parchment. Real coastlines come from Natural Earth 10 m land (public domain), painted with ink outlines, offshore ripple lines, painted hill marks, a compass rose with rhumb lines and a degree-tick border. It has no labels. St. Kitts was widened about 2x across the island chain (same paint) so it reads on the chart and in the land mask. Covers lon −98…−58 and lat ≈6.8…30.5 (Gulf of Mexico to Barbados, Florida to Panama). Max import size is 4096. |
| Port coordinates | `SG_Map_Ports.json` | The 11 ports from the plan, plus St. Kitts (English, Old Road; added in M2) with nation, lat/lon, pixel position (origin top-left) and UV (origin bottom-left). Data only, not code. |
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

## M3: ships, duel, treasure and ranks

Same painterly storybook style as the approved sloop: hand-painted texture, baked soft shadows, dark painted edge lines. No text or logos. All 2D textures import as Sprites with no mipmaps and no NPOT resize. Every `.meta` file is committed and was checked in a throwaway Unity 6000.3.26f1 project.

### Ships (`Ships/`)
Each ship is built the same way as the Sloop: real-world scale, origin at mid-hull, waterline at y = 0, bow faces +Z, 1 material and 1 1024² texture.

| Ship | Folder | Tris | Size | Notes |
|---|---|---|---|---|
| Sloop | `Sloop/` | 2,432 | ~12 m | Unchanged mesh and albedo. M3 adds a mask and the nation variants. There is no ensign, only the pennant. |
| Brigantine | `Brigantine/` | 2,998 | ~22 m | Square-rigged foremast, gaff main with a topsail, jib. |
| Fluyt | `Fluyt/` | 2,998 | ~26 m | Wide-bellied trader with strong tumblehome and a narrow deck. Two square-rigged masts and a lateen mizzen. |
| Frigate | `Frigate/` | 3,822 | ~34 m | Three square-rigged masts, a painted gun stripe, 9 guns a side, forecastle. |
| Galleon | `Galleon/` | 3,788 | ~40 m | Tall two-tier stern castle with windows, forecastle, lateen mizzen, 10 guns a side. |

Files in each folder:
- `<Ship>.fbx`
- `<Ship>_Albedo.png` (neutral base)
- `<Ship>_Albedo_{England,Spain,France,Dutch,Pirate}.png`
- `<Ship>_Mask.png`

**Nation variants.** There are two ways to show a nation:
- **Baked variants (works today).** The game's ships use the unlit shader, which shows `_MainTex` times `_Tint`. Swap the albedo for the nation's variant. Nation trim is repainted in the nation colour with the painted light and shade kept. Pirate sails are dark and tarred. The stern ensign shows the nation flag, or a generic black skull-and-crossbones for pirates.
- **Shared tint mask (needs a shader).** One mask layout is shared by every ship, the Sloop included. Colours are in `Ships/SG_Nation_Palette.json`.

| Mask channel | Covers |
|---|---|
| R | Sails |
| G | Nation trim (sheer strake, gun stripe, pennants) |
| B | Stern ensign rect |

A shader would recolour G to the nation `trim` colour while keeping luminance, multiply R by `sail_mul`, and draw the flag into the B rect. That shader is game code and is not included. Masks import with sRGB off.

### Duel stage (`Duel/Stage/`)
The stage is three 1280×720 parallax layers. Every layer has a bottom-centre pivot.

| Layer | File | Parallax | Notes |
|---|---|---|---|
| 0 | `SG_Duel_Sky.png` | 0.1 | Opaque. Sky, island and horizon. |
| 1 | `SG_Duel_Sea.png` | 0.35 | Sea from the horizon (row 386) down. Transparent above. |
| 2 | `SG_Duel_Deck.png` | 1.0 | Side-on deck with a rail at each end. Transparent around it. |

`SG_Duel_Stage.json` holds:
- the deck stand line (y 380 in the layer, back floor 335, front floor 418)
- the inner rail x positions (270 and 1044)
- a suggested deck offset of about 180 px down

### Captains (`Duel/Captains/`)
There are two captains:
- **Player:** cavalier hat with a plume, rapier.
- **Enemy:** stockier, bearded, wears a bandana under a tall hat, longer coat, cutlass.

Both are rendered from a single rig, so the character stays identical across frames.

Sheet details:
- Each sheet is 3584×2880: 7 columns × 9 rows of **512×320 cells**.
- One row per animation, top row first, with frames left to right. Unused cells are empty.
- **The pivot is bottom-centre (0.5, 0) in every frame.** The feet stand on the bottom edge at x = 256.
- 100 pixels per unit, so 1 unit = 1 m.
- The captains face +X. Flip X for the opponent.
- The `.meta` files slice every sheet into 49 sprites named `<sheet>_<anim>_<i>`, for example `SG_Captain_Player_England_high_2`.

Sheets per captain:
- `SG_Captain_<Who>_Base.png` (neutral)
- `_England`, `_Spain`, `_France`, `_Dutch` and `_Pirate` (palette swaps)
- `_Mask.png` (R = coat, G = sash, hat band, plume, cuffs and bandana)

| Animation | Row | Frames | Suggested fps |
|---|---|---|---|
| idle | 0 | 6 | 8 |
| step | 1 | 6 | 12 |
| high (attack) | 2 | 6 | 14 |
| mid (attack) | 3 | 6 | 14 |
| low (attack) | 4 | 6 | 14 |
| block | 5 | 3 | 12 |
| parry | 6 | 5 | 14 |
| hit | 7 | 4 | 12 |
| fall | 8 | 7 | 10 |
| **Total** | | **49** | |

The same data is in `SG_Captain_Frames.json`.

### Treasure (`Treasure/`)
| Piece | File | Pivot | Notes |
|---|---|---|---|
| Combined map | `SG_Treasure_Map_Full.png` | Centre | 1024×768 torn parchment island chart with a red X. |
| Map pieces | `SG_Treasure_MapPiece_{1..4}.png` | Centre | Cut from the full map along jagged tears. The X lies wholly inside piece 2 (top-right). `SG_Treasure_Map.json` gives each piece's offset so the four reassemble into the full map. |
| Dig spot | `SG_Treasure_DigSpot.png` | Centre | 128² red X in a dotted ring, for the world map. |
| Chest of gold | `SG_Treasure_Chest.png` | Bottom-centre | 512². The open chest sits on the bottom edge. |

### UI (`UI/`)
| Piece | File | Pivot | Notes |
|---|---|---|---|
| Rank icons | `SG_Icon_Rank_{1_Ensign,2_Lieutenant,3_Captain,4_Commodore,5_Admiral}.png` | Centre | 256² gold insignia on the M2 parchment medallion frame. Ensign: 1 chevron. Lieutenant: 2 chevrons. Captain: anchor. Commodore: anchor and 2 stars. Admiral: crossed anchors and a star. |
| Captain's log | `SG_UI_CaptainsLog.png` | Centre | 1280×720 **full-screen background, not 9-slice**: an open blank logbook on a desk. `SG_UI_CaptainsLog.json` gives the text rects for both pages. |

Generator scripts and Blender sources are kept outside the repo, in the art workspace.

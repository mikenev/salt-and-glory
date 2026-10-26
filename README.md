# Salt and Glory

A captain's career in the Caribbean, 1660: trade between ports, fight broadside battles under sail, board ships and duel their captains, dig up buried treasure, and rise in rank under a letter of marque. It's a Unity 6 WebGL game that runs in the browser with a keyboard, a gamepad or touch.

## How to play

- **Title:** start a **New career**, **Continue** your newest save, or **Load** one of three slots. Saves stay in this browser (localStorage). Saves from M2 and M3 still load.
- **Ports:** dock near a port (the prompt at the bottom of the screen) to visit the town:
  - **Tavern:** rumours, crew and treasure-map pieces.
  - **Governor:** letters of marque and missions.
  - **Merchant:** trade. Prices move as you buy and sell, then settle back over a few minutes.
  - **Shipwright:** repairs, and refits into the Sloop, Brigantine, Fluyt, Frigate or Galleon. A trade-in pays 75% of what your ship is worth as she is, or 45% for a captured prize.
  - You can also save your game and read the **Captain's log**.
- **Wind:** the needle on the gauge (top right) points where the wind blows, relative to your bow. Pointed too close to the wind you stall ("in irons"). To reach a port that is upwind, zig-zag (tack) across the wind. A hint appears when you need it.
- **Battles:** fire broadsides from either side, and switch between round shot (hull), chain shot (sails) and grape shot (crew). Attacking a ship of a nation your patron is at war with is lawful. Otherwise it's piracy, and that nation remembers.
- **Boarding:** come alongside and press Enter / A / tap the prompt. If the ship hasn't fired on you, you're asked first. Then duel her captain on deck:
  - Attacks go high, mid or low. A high attack gets past a low guard, and a low attack gets past a high guard.
  - Hold block to guard. A block timed just before the hit is a parry, and it leaves the attacker open.
  - Push the enemy captain over the rail to win the ship and her cargo, then keep your ship or take hers.
- **Treasure:** four map pieces (from taverns or from prizes) mark a dig spot. The torn chart in the Captain's log shows where it is. Sail there and dig.
- **Fame and rank:** fame and your rank under each letter of marque (Ensign to Admiral) are kept in the Captain's log. **Retire** from the log to end the career and see your final fame score; its save slot is then closed (it can be saved over by a new career, not loaded).

## Controls

| | Keyboard | Gamepad | Touch / mouse |
|---|---|---|---|
| Menus (title, town, log, prompts) | Arrows / WASD to move, Enter or Space to choose, Esc back | D-pad / stick, A, B back | Tap / click a button |
| Steer | A / D or Left / Right | Left stick | Drag on the sea |
| Sails up / down | W / S or Up / Down | RT / LT | Sail slider (right) |
| Broadsides | Q (port) / E (starboard) | LB / RB | PORT / STARBOARD buttons |
| Shot type | Tab | Y | Shot slots (bottom) |
| Dock, board, dig | Enter | A | The prompt button |
| Map / log / pause | M / L / Esc or P | Back (Select) / D-pad up / Start | MAP / LOG / II buttons |
| Duel: step | A / D | Stick / D-pad left-right | Drag on the left 30% of the screen |
| Duel: attack | J (hold W for high, S for low) | X (stick up = high, down = low) | Swipe up / across / down, or the High / Mid / Low buttons |
| Duel: block / parry | Hold K / tap K just before a hit | Hold B / tap B | Hold / tap, or hold the Block button |

## Building

Open in Unity 6000.3, or build headless with `Unity -batchmode -nographics -projectPath . -buildTarget WebGL -executeMethod BuildWeb.Build`. This sets up the scene from `Assets/Art` and writes the release build to `docs/`. `SG_DEV=1` makes a development build in `Builds/WebGLDev`. `SG_QA=1` makes a release-like build with the self-check hook in `Builds/WebGLQA`.

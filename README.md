# Solo Knight

A real-time action RPG and dungeon crawler for PC (keyboard and mouse), built in Unity 6000.4.0f1 with the Built-in Render Pipeline.

## Quick start
1. Open this folder in **Unity Hub** with editor **6000.4.0f1**.
2. The scenes have already been generated. To rebuild them at any time, use **Solo Knight → Generate Complete Playable Prototype** (or **Tools → Solo Knight → Build Game Scene**).
3. Open `Assets/_Project/Scenes/MainMenu.unity`, press **Play**, then click **New Game**.
   You can also open `DungeonFloor_01.unity` and press Play to go straight into the dungeon.

Other menu items:
- **Solo Knight → Validate Generated Content** checks every prefab and scene for missing references.
- **Solo Knight → Delete Save Data** and **Open Save Folder** manage the save file.

## Controls
| Input | Action |
|---|---|
| WASD / Arrow keys | Move (8 directions, scaled by Move Speed) |
| Mouse | Aim (the knight faces the cursor) |
| Left mouse (hold) | 3-hit combo slash (the 3rd hit is a heavy finisher) |
| Right mouse / Q | Whirlwind AoE cleave (6 s cooldown) |
| Space | Dash with i-frames (1 s cooldown) |
| F | Interact (chests, exit portal) |
| 1 / 2 | Health Potion (35%) / Greater Potion (70%) |
| Tab / I | Character panel: spend stat points, equip, salvage (pauses the game) |
| Esc | Pause menu (Resume, Save, Main Menu, Quit) |

## Gameplay loop
Enter floor → kill mobs (Skeletons, Brutes, Cultist casters) → collect gold, potions and gear (Common / Rare / Epic) → level up (+3 stat points, full heal) → open the 2 alcove chests and complete the floor mission → enter the boss room. The entrance seals behind you and the boss bar appears. Bosses use Cleave, Ground Slam, Charge and Projectile Nova, and become enraged below 50% HP → beat the boss to open the exit portal and save → move on to the next floor.

Floors: **1 Stone Catacombs**, **2 Sunken Crypt**, **3 Ember Foundry**. Floor 4 and beyond are generated procedurally, with stats scaling up about 30% per floor. When you die you retry the same floor and keep your level, stats, gold and gear.

The save file is JSON at `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Solo Knight\soloknight_save.json`.

## Code map (`Assets/_Project/Scripts`)
- **Core**: `GameManager` (persistent state and scene flow), `SaveSystem`, `AudioManager` (sound effects are synthesized in code, no audio files), `CameraFollow`, `SimpleVfx`, `MaterialUtil`, `CombatTypes` (`DamageInfo`, `IDamageable`, `IInteractable`)
- **Player**: `PlayerController`, `PlayerStats` (5 stats, crit 1.75x capped at 85%), `PlayerCombat`, `InventorySystem`
- **Enemies**: `EnemyStats`, `EnemyAI` (CharacterController and steering, so no NavMesh package is needed), `BossAI`, `Projectile`, `LootDropper`, `EnemyHealthBar`
- **Items**: `ItemSO`, `ItemDatabaseSO`, `WorldPickup`, `TreasureChest`
- **World**: `FloorManager`, `FloorDataSO`, `FloorDatabaseSO`, `MissionManager`, `EnemySpawnPoint`, `BossRoomTrigger`, `FloorPortal`
- **UI**: `UIManager` (HUD, damage numbers, toasts, boss bar, character panel, modals), `MainMenuUI`, `UIFactory`

Tuning lives in the inspector: item stats in `ScriptableObjects/Items`, floor difficulty and colours in `ScriptableObjects/Floors`, enemy stats on the prefabs. If you re-run the generator, it overwrites the generated assets and scenes.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SoloKnight.EditorTools
{
    /// <summary>
    /// One-click generator for the whole playable prototype:
    /// materials, item + floor ScriptableObjects, Player/Enemy/Boss/Chest/Pickup/Projectile prefabs,
    /// the DungeonFloor_01 and MainMenu scenes (lighting, colliders, UI) and Build Settings.
    /// Every serialized reference is assigned in code, so nothing needs to be dragged in the Inspector.
    /// Safe to re-run: assets are updated in place and scenes are regenerated.
    /// </summary>
    public static class SoloKnightSceneBuilder
    {
        const string Root = "Assets/_Project";
        const string MatDir = Root + "/Materials";
        const string PrefabDir = Root + "/Prefabs";
        const string ItemDir = Root + "/ScriptableObjects/Items";
        const string FloorDir = Root + "/ScriptableObjects/Floors";
        const string SceneDir = Root + "/Scenes";
        public const string DungeonScenePath = SceneDir + "/DungeonFloor_01.unity";
        public const string MenuScenePath = SceneDir + "/MainMenu.unity";

        const float WallHeight = 2.4f;
        const float WallThickness = 1f;
        const float CorridorWidth = 4f;

        // =====================================================================
        // Menu entries
        // =====================================================================

        [MenuItem("Solo Knight/Generate Complete Playable Prototype", priority = 0)]
        public static void GenerateCompletePrototype() => Generate(true);

        [MenuItem("Tools/Solo Knight/Build Game Scene", priority = 0)]
        public static void BuildGameScene() => Generate(true);

        [MenuItem("Solo Knight/Delete Save Data", priority = 20)]
        public static void DeleteSaveData()
        {
            SaveSystem.Delete();
            Debug.Log("[SoloKnight] Save data deleted: " + SaveSystem.SavePath);
        }

        [MenuItem("Solo Knight/Open Save Folder", priority = 21)]
        public static void OpenSaveFolder() => EditorUtility.RevealInFinder(Application.persistentDataPath);

        /// <summary>Entry point for command-line builds: Unity -batchmode -executeMethod SoloKnight.EditorTools.SoloKnightSceneBuilder.GenerateBatch</summary>
        public static void GenerateBatch() => Generate(false);

        static void Generate(bool interactive)
        {
            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                Progress(interactive, "Folders & materials", 0.05f);
                EnsureFolders();
                var mats = CreateMaterials();

                Progress(interactive, "Items & floors", 0.2f);
                var db = CreateItems();
                var floorDb = CreateFloors();

                Progress(interactive, "Prefabs", 0.4f);
                var prefabs = CreatePrefabs(mats, db);

                Progress(interactive, "Dungeon scene", 0.6f);
                BuildDungeonScene(mats, prefabs, db, floorDb);

                Progress(interactive, "Main menu scene", 0.85f);
                BuildMainMenuScene(mats, db, floorDb);

                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(MenuScenePath, true),
                    new EditorBuildSettingsScene(DungeonScenePath, true),
                };
                PlayerSettings.productName = "Solo Knight";
                PlayerSettings.defaultScreenWidth = 1920;
                PlayerSettings.defaultScreenHeight = 1080;
                PlayerSettings.resizableWindow = true;

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[SoloKnight] Prototype generated successfully. Open Assets/_Project/Scenes/MainMenu.unity and press Play.");

                if (interactive)
                {
                    EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Solo Knight",
                        "Prototype generated!\n\nMainMenu and DungeonFloor_01 scenes, prefabs, items and floors were created and wired.\n\nThe MainMenu scene is now open - press Play to start.",
                        "Let's go");
                }
            }
            finally
            {
                if (interactive) EditorUtility.ClearProgressBar();
            }
        }

        static void Progress(bool interactive, string step, float p)
        {
            if (interactive) EditorUtility.DisplayProgressBar("Solo Knight Builder", step, p);
            else Debug.Log("[SoloKnight] Builder: " + step);
        }

        static void EnsureFolders()
        {
            string[] dirs = { MatDir, PrefabDir, ItemDir, FloorDir, SceneDir, Root + "/UI" };
            foreach (var d in dirs)
            {
                if (AssetDatabase.IsValidFolder(d)) continue;
                string parent = Path.GetDirectoryName(d).Replace('\\', '/');
                string leaf = Path.GetFileName(d);
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    string gp = Path.GetDirectoryName(parent).Replace('\\', '/');
                    AssetDatabase.CreateFolder(gp, Path.GetFileName(parent));
                }
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        // =====================================================================
        // Materials
        // =====================================================================

        class Mats
        {
            public Material floor, wall, door, player, playerAccent, sword, skeleton, brute, caster, boss, bossAccent,
                eyes, chest, chestTrim, gold, gear, projectile, portal, unlit, menuFloor;
        }

        static bool IsURP => GraphicsSettings.currentRenderPipeline != null;

        static Material Mat(string name, Color color, float smoothness = 0.2f, float metallic = 0f, Color? emission = null)
        {
            string path = $"{MatDir}/{name}.mat";
            Shader shader = IsURP ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;

            MaterialUtil.SetColor(m, color);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", emission.Value);
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material UnlitMat(string name, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            Shader shader = IsURP ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Unlit/Color");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            MaterialUtil.SetColor(m, color);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Mats CreateMaterials()
        {
            return new Mats
            {
                floor = Mat("M_Floor", new Color(0.3f, 0.3f, 0.32f), 0.1f),
                wall = Mat("M_Wall", new Color(0.45f, 0.43f, 0.4f), 0.05f),
                door = Mat("M_BossDoor", new Color(0.35f, 0.08f, 0.06f), 0.3f, 0.2f, new Color(0.4f, 0.05f, 0.02f)),
                player = Mat("M_Player", new Color(0.55f, 0.6f, 0.7f), 0.6f, 0.6f),
                playerAccent = Mat("M_PlayerAccent", new Color(0.12f, 0.25f, 0.55f), 0.4f, 0.2f),
                sword = Mat("M_Sword", new Color(0.85f, 0.88f, 0.95f), 0.85f, 0.9f),
                skeleton = Mat("M_Skeleton", new Color(0.85f, 0.82f, 0.7f), 0.15f),
                brute = Mat("M_Brute", new Color(0.35f, 0.45f, 0.25f), 0.2f),
                caster = Mat("M_Caster", new Color(0.35f, 0.18f, 0.5f), 0.3f),
                boss = Mat("M_Boss", new Color(0.3f, 0.12f, 0.12f), 0.35f, 0.3f),
                bossAccent = Mat("M_BossAccent", new Color(0.15f, 0.15f, 0.15f), 0.6f, 0.7f),
                eyes = Mat("M_Eyes", new Color(1f, 0.25f, 0.1f), 0.5f, 0f, new Color(2f, 0.4f, 0.1f)),
                chest = Mat("M_Chest", new Color(0.45f, 0.27f, 0.12f), 0.2f),
                chestTrim = Mat("M_ChestTrim", new Color(0.75f, 0.6f, 0.25f), 0.7f, 0.8f),
                gold = Mat("M_Gold", new Color(1f, 0.8f, 0.2f), 0.8f, 0.9f, new Color(0.5f, 0.35f, 0.05f)),
                gear = Mat("M_Gear", new Color(0.85f, 0.85f, 0.85f), 0.6f, 0.5f, new Color(0.25f, 0.25f, 0.25f)),
                projectile = Mat("M_Projectile", new Color(0.7f, 0.35f, 1f), 0.5f, 0f, new Color(1.2f, 0.5f, 2f)),
                portal = Mat("M_Portal", new Color(0.55f, 0.35f, 1f), 0.8f, 0f, new Color(1f, 0.6f, 2.5f)),
                unlit = UnlitMat("M_UnlitTemplate", Color.white),
                menuFloor = Mat("M_MenuFloor", new Color(0.15f, 0.15f, 0.17f), 0.3f),
            };
        }

        // =====================================================================
        // Items
        // =====================================================================

        static ItemSO Item(string id, string name, ItemType type, ItemRarity rarity, int atk = 0, int def = 0, float crit = 0f, float speed = 0f, int hp = 0, float heal = 0f, string desc = "")
        {
            var it = CreateOrLoad<ItemSO>($"{ItemDir}/{id}.asset");
            it.itemId = id;
            it.displayName = name;
            it.description = desc;
            it.itemType = type;
            it.rarity = rarity;
            it.bonusAttack = atk;
            it.bonusDefense = def;
            it.bonusCritChance = crit;
            it.bonusMoveSpeed = speed;
            it.bonusMaxHP = hp;
            it.healPercent = heal;
            it.healFlat = 0;
            it.sellValue = type == ItemType.Potion ? 5 : rarity == ItemRarity.Epic ? 80 : rarity == ItemRarity.Rare ? 30 : 10;
            EditorUtility.SetDirty(it);
            return it;
        }

        static ItemDatabaseSO CreateItems()
        {
            var items = new List<ItemSO>
            {
                // Weapons
                Item("wpn_common_sword", "Rusty Shortsword", ItemType.Weapon, ItemRarity.Common, atk: 3, desc: "Every knight starts somewhere."),
                Item("wpn_common_axe", "Iron Hatchet", ItemType.Weapon, ItemRarity.Common, atk: 5, crit: 0.01f),
                Item("wpn_common_spear", "Militia Spear", ItemType.Weapon, ItemRarity.Common, atk: 4, speed: 0.2f),
                Item("wpn_rare_blade", "Knight's Longsword", ItemType.Weapon, ItemRarity.Rare, atk: 9, crit: 0.03f),
                Item("wpn_rare_mace", "Crypt Mace", ItemType.Weapon, ItemRarity.Rare, atk: 11, def: 2),
                Item("wpn_epic_ember", "Emberbrand", ItemType.Weapon, ItemRarity.Epic, atk: 18, crit: 0.08f, desc: "Forged in the heart of the foundry."),
                Item("wpn_epic_reaper", "Gravewhisper Scythe", ItemType.Weapon, ItemRarity.Epic, atk: 15, crit: 0.12f, speed: 0.5f),
                // Armor
                Item("arm_common_leather", "Patched Leather", ItemType.Armor, ItemRarity.Common, def: 3, hp: 10),
                Item("arm_common_chain", "Rusted Chainmail", ItemType.Armor, ItemRarity.Common, def: 5),
                Item("arm_rare_plate", "Sentinel Plate", ItemType.Armor, ItemRarity.Rare, def: 9, hp: 30),
                Item("arm_rare_cloak", "Shadowstep Cloak", ItemType.Armor, ItemRarity.Rare, def: 5, crit: 0.03f, speed: 0.8f),
                Item("arm_epic_aegis", "Aegis of Dawn", ItemType.Armor, ItemRarity.Epic, def: 16, hp: 60),
                Item("arm_epic_wyrm", "Wyrmscale Mail", ItemType.Armor, ItemRarity.Epic, def: 12, hp: 40, speed: 0.5f),
            };
            var small = Item("pot_small", "Health Potion", ItemType.Potion, ItemRarity.Common, heal: 0.35f, desc: "Restores 35% of max HP.");
            var large = Item("pot_large", "Greater Potion", ItemType.Potion, ItemRarity.Rare, heal: 0.7f, desc: "Restores 70% of max HP.");
            items.Add(small);
            items.Add(large);

            var db = CreateOrLoad<ItemDatabaseSO>($"{Root}/ScriptableObjects/ItemDatabase.asset");
            db.items = items;
            db.smallPotion = small;
            db.largePotion = large;
            EditorUtility.SetDirty(db);
            return db;
        }

        // =====================================================================
        // Floors
        // =====================================================================

        static FloorDataSO Floor(int n, string name, string boss, Color floor, Color wall, Color ambient, Color fog, Color torch,
            float hp, float dmg, float bossHp, float bossDmg, int extra, float brute, float caster, float exp, float gold, float rarity)
        {
            var f = CreateOrLoad<FloorDataSO>($"{FloorDir}/Floor_{n:00}_{name.Replace(" ", "")}.asset");
            f.floorNumber = n;
            f.floorName = name;
            f.bossName = boss;
            f.floorColor = floor;
            f.wallColor = wall;
            f.ambientColor = ambient;
            f.fogColor = fog;
            f.torchColor = torch;
            f.enemyHealthMultiplier = hp;
            f.enemyDamageMultiplier = dmg;
            f.bossHealthMultiplier = bossHp;
            f.bossDamageMultiplier = bossDmg;
            f.extraEnemiesPerSpawn = extra;
            f.bruteChance = brute;
            f.casterChance = caster;
            f.expMultiplier = exp;
            f.goldMultiplier = gold;
            f.rarityBonus = rarity;
            EditorUtility.SetDirty(f);
            return f;
        }

        static FloorDatabaseSO CreateFloors()
        {
            var f1 = Floor(1, "Stone Catacombs", "Bone Warden",
                new Color(0.3f, 0.3f, 0.32f), new Color(0.45f, 0.43f, 0.4f), new Color(0.28f, 0.28f, 0.33f), new Color(0.04f, 0.04f, 0.06f), new Color(1f, 0.72f, 0.42f),
                1f, 1f, 1f, 1f, 0, 0.15f, 0.15f, 1f, 1f, 0f);
            var f2 = Floor(2, "Sunken Crypt", "Drowned Abbot",
                new Color(0.16f, 0.26f, 0.27f), new Color(0.25f, 0.38f, 0.38f), new Color(0.16f, 0.28f, 0.3f), new Color(0.02f, 0.07f, 0.08f), new Color(0.45f, 0.9f, 1f),
                1.6f, 1.35f, 1.6f, 1.3f, 1, 0.2f, 0.25f, 1.5f, 1.5f, 0.3f);
            var f3 = Floor(3, "Ember Foundry", "Forge Tyrant",
                new Color(0.25f, 0.14f, 0.1f), new Color(0.42f, 0.22f, 0.14f), new Color(0.35f, 0.18f, 0.1f), new Color(0.1f, 0.03f, 0.01f), new Color(1f, 0.45f, 0.15f),
                2.5f, 1.8f, 2.4f, 1.7f, 1, 0.3f, 0.25f, 2.2f, 2.2f, 0.6f);

            var db = CreateOrLoad<FloorDatabaseSO>($"{Root}/ScriptableObjects/FloorDatabase.asset");
            db.floors = new List<FloorDataSO> { f1, f2, f3 };
            EditorUtility.SetDirty(db);
            return db;
        }

        // =====================================================================
        // Prefabs
        // =====================================================================

        class Prefabs
        {
            public PlayerController player;
            public EnemyStats skeleton, brute, caster, boss;
            public TreasureChest chest;
            public WorldPickup pickup;
            public Projectile projectile;
        }

        static GameObject V(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, string name, Vector3? euler = null)
        {
            var go = MaterialUtil.Visual(t, parent, pos, scale, m, name);
            if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
            return go;
        }

        static T SavePrefab<T>(GameObject go, string name) where T : Component
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(go, path, out bool ok);
            Object.DestroyImmediate(go);
            if (!ok || saved == null) throw new System.Exception("Failed to save prefab " + path);
            return saved.GetComponent<T>();
        }

        /// <summary>Builds the knight's primitive body under parent. Returns the sword pivot.</summary>
        static Transform BuildKnightVisual(Transform parent, Mats m)
        {
            var vis = new GameObject("Visual").transform;
            vis.SetParent(parent, false);
            V(PrimitiveType.Capsule, vis, new Vector3(0f, 1f, 0f), new Vector3(0.9f, 1f, 0.9f), m.player, "Body");
            V(PrimitiveType.Cube, vis, new Vector3(0f, 1.05f, 0f), new Vector3(0.95f, 0.5f, 0.95f), m.playerAccent, "Tabard");
            V(PrimitiveType.Cube, vis, new Vector3(0f, 1.72f, 0f), new Vector3(0.7f, 0.5f, 0.7f), m.player, "Helmet");
            V(PrimitiveType.Cube, vis, new Vector3(0f, 1.74f, 0.33f), new Vector3(0.5f, 0.09f, 0.1f), m.eyes, "Visor");
            V(PrimitiveType.Cube, vis, new Vector3(0f, 2.06f, -0.05f), new Vector3(0.12f, 0.25f, 0.55f), m.playerAccent, "Plume");
            V(PrimitiveType.Cube, vis, new Vector3(-0.55f, 1.05f, 0.15f), new Vector3(0.12f, 0.85f, 0.65f), m.playerAccent, "Shield");
            V(PrimitiveType.Cube, vis, new Vector3(-0.62f, 1.05f, 0.15f), new Vector3(0.04f, 0.4f, 0.3f), m.chestTrim, "ShieldEmblem");

            var pivot = new GameObject("SwordPivot").transform;
            pivot.SetParent(vis, false);
            pivot.localPosition = new Vector3(0.5f, 1.05f, 0.1f);
            pivot.localEulerAngles = new Vector3(0f, 45f, 0f);
            V(PrimitiveType.Cube, pivot, new Vector3(0f, 0f, 0.85f), new Vector3(0.12f, 0.06f, 1.5f), m.sword, "Blade");
            V(PrimitiveType.Cube, pivot, new Vector3(0f, 0f, 0.1f), new Vector3(0.4f, 0.1f, 0.1f), m.chestTrim, "Guard");
            V(PrimitiveType.Cube, pivot, new Vector3(0f, 0f, -0.08f), new Vector3(0.08f, 0.08f, 0.25f), m.playerAccent, "Grip");
            return pivot;
        }

        static Prefabs CreatePrefabs(Mats m, ItemDatabaseSO db)
        {
            var p = new Prefabs();

            // ---------- Projectile ----------
            {
                var go = new GameObject("Projectile");
                var proj = go.AddComponent<Projectile>();
                proj.radius = 0.25f;
                V(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * 0.45f, m.projectile, "Orb");
                V(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0f, -0.3f), Vector3.one * 0.25f, m.projectile, "Trail");
                p.projectile = SavePrefab<Projectile>(go, "Projectile");
            }

            // ---------- Pickup ----------
            {
                var go = new GameObject("Pickup");
                var wp = go.AddComponent<WorldPickup>();
                wp.orbVisual = V(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0.45f, 0f), Vector3.one * 0.45f, m.gold, "Orb").GetComponent<Renderer>();
                wp.gearVisual = V(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.45f, 0f), Vector3.one * 0.5f, m.gear, "Gear", new Vector3(45f, 0f, 45f)).GetComponent<Renderer>();
                wp.gearVisual.gameObject.SetActive(false);
                p.pickup = SavePrefab<WorldPickup>(go, "Pickup");
            }

            // ---------- Player ----------
            {
                var go = new GameObject("Player");
                go.tag = "Player";
                var cc = go.AddComponent<CharacterController>();
                cc.height = 2f;
                cc.radius = 0.45f;
                cc.center = new Vector3(0f, 1f, 0f);
                cc.stepOffset = 0.3f;
                cc.skinWidth = 0.05f;
                var inv = go.AddComponent<InventorySystem>();
                inv.potionSlot1 = db.smallPotion;
                inv.potionSlot2 = db.largePotion;
                go.AddComponent<PlayerStats>();
                var combat = go.AddComponent<PlayerCombat>();
                go.AddComponent<PlayerController>();
                combat.swordPivot = BuildKnightVisual(go.transform, m);
                combat.swordRestEuler = new Vector3(0f, 45f, 0f);
                p.player = SavePrefab<PlayerController>(go, "Player");
            }

            // ---------- Enemies ----------
            p.skeleton = SaveEnemy("Enemy_Skeleton", m, p, ccHeight: 2f, ccRadius: 0.45f, build: vis =>
            {
                V(PrimitiveType.Capsule, vis, new Vector3(0f, 0.95f, 0f), new Vector3(0.7f, 0.95f, 0.6f), m.skeleton, "Body");
                V(PrimitiveType.Sphere, vis, new Vector3(0f, 1.85f, 0f), Vector3.one * 0.55f, m.skeleton, "Skull");
                V(PrimitiveType.Cube, vis, new Vector3(0f, 1.88f, 0.24f), new Vector3(0.34f, 0.08f, 0.1f), m.eyes, "Eyes");
                V(PrimitiveType.Cube, vis, new Vector3(0.45f, 1f, 0.45f), new Vector3(0.08f, 0.08f, 1f), m.bossAccent, "Blade");
            }, setup: (s, ai, loot) =>
            {
                s.enemyName = "Skeleton"; s.maxHP = 40; s.damage = 8; s.expReward = 15;
                s.deathBurstColor = new Color(0.85f, 0.82f, 0.7f);
                ai.attackType = EnemyAttackType.Melee; ai.moveSpeed = 3.4f;
                loot.goldRange = new Vector2Int(3, 8); loot.potionChance = 0.1f; loot.equipmentChance = 0.08f;
            });

            p.brute = SaveEnemy("Enemy_Brute", m, p, ccHeight: 2.6f, ccRadius: 0.8f, build: vis =>
            {
                V(PrimitiveType.Cube, vis, new Vector3(0f, 1.25f, 0f), new Vector3(1.6f, 1.6f, 1.2f), m.brute, "Body");
                V(PrimitiveType.Cube, vis, new Vector3(0f, 2.3f, 0.1f), new Vector3(0.8f, 0.6f, 0.7f), m.brute, "Head");
                V(PrimitiveType.Cube, vis, new Vector3(0f, 2.32f, 0.46f), new Vector3(0.5f, 0.1f, 0.05f), m.eyes, "Eyes");
                V(PrimitiveType.Cube, vis, new Vector3(-1.05f, 1.1f, 0.2f), new Vector3(0.5f, 1.5f, 0.5f), m.brute, "ArmL");
                V(PrimitiveType.Cube, vis, new Vector3(1.05f, 1.1f, 0.2f), new Vector3(0.5f, 1.5f, 0.5f), m.brute, "ArmR");
                V(PrimitiveType.Cube, vis, new Vector3(1.05f, 0.4f, 0.6f), new Vector3(0.7f, 0.7f, 0.9f), m.bossAccent, "Fist");
            }, setup: (s, ai, loot) =>
            {
                s.enemyName = "Brute"; s.maxHP = 110; s.damage = 16; s.defense = 2; s.expReward = 35; s.knockbackResistance = 0.65f;
                s.deathBurstColor = new Color(0.35f, 0.45f, 0.25f);
                ai.attackType = EnemyAttackType.Melee; ai.moveSpeed = 2.5f; ai.attackRange = 2.4f; ai.meleeHitRange = 3f;
                ai.attackWindup = 0.7f; ai.attackCooldown = 1.6f; ai.playerKnockback = 11f;
                loot.goldRange = new Vector2Int(8, 16); loot.potionChance = 0.2f; loot.equipmentChance = 0.22f;
            });

            p.caster = SaveEnemy("Enemy_Caster", m, p, ccHeight: 2f, ccRadius: 0.45f, build: vis =>
            {
                V(PrimitiveType.Cylinder, vis, new Vector3(0f, 0.9f, 0f), new Vector3(0.85f, 0.9f, 0.85f), m.caster, "Robe");
                V(PrimitiveType.Sphere, vis, new Vector3(0f, 1.95f, 0f), Vector3.one * 0.62f, m.caster, "Hood");
                V(PrimitiveType.Cube, vis, new Vector3(0f, 1.95f, 0.27f), new Vector3(0.3f, 0.08f, 0.1f), m.eyes, "Eyes");
                V(PrimitiveType.Cylinder, vis, new Vector3(0.5f, 1.1f, 0.2f), new Vector3(0.07f, 1f, 0.07f), m.chest, "Staff");
                V(PrimitiveType.Sphere, vis, new Vector3(0.5f, 2.15f, 0.2f), Vector3.one * 0.3f, m.projectile, "Orb");
            }, setup: (s, ai, loot) =>
            {
                s.enemyName = "Cultist"; s.maxHP = 30; s.damage = 10; s.expReward = 20;
                s.deathBurstColor = new Color(0.6f, 0.3f, 0.9f);
                ai.attackType = EnemyAttackType.Ranged; ai.moveSpeed = 3f; ai.preferredRange = 8f;
                ai.attackWindup = 0.6f; ai.attackCooldown = 1.8f; ai.projectileSpeed = 11f; ai.aggroRange = 12f;
                ai.projectilePrefab = p.projectile;
                loot.goldRange = new Vector2Int(4, 10); loot.potionChance = 0.15f; loot.equipmentChance = 0.1f;
            });

            // ---------- Boss ----------
            {
                var go = new GameObject("Boss");
                var cc = go.AddComponent<CharacterController>();
                cc.height = 4f; cc.radius = 1.3f; cc.center = new Vector3(0f, 2f, 0f); cc.stepOffset = 0.3f;
                var loot = go.AddComponent<LootDropper>();
                var stats = go.AddComponent<EnemyStats>();
                var boss = go.AddComponent<BossAI>();
                var vis = new GameObject("Visual").transform;
                vis.SetParent(go.transform, false);
                V(PrimitiveType.Cube, vis, new Vector3(0f, 2f, 0f), new Vector3(2.6f, 2.8f, 1.8f), m.boss, "Body");
                V(PrimitiveType.Cube, vis, new Vector3(0f, 2.4f, 0.92f), new Vector3(1.6f, 1.2f, 0.1f), m.bossAccent, "Chestplate");
                V(PrimitiveType.Cube, vis, new Vector3(0f, 3.85f, 0.2f), new Vector3(1.3f, 1f, 1.2f), m.boss, "Head");
                V(PrimitiveType.Cube, vis, new Vector3(0f, 3.9f, 0.82f), new Vector3(0.9f, 0.15f, 0.05f), m.eyes, "Eyes");
                V(PrimitiveType.Cube, vis, new Vector3(-0.6f, 4.55f, 0.2f), new Vector3(0.2f, 0.8f, 0.2f), m.bossAccent, "HornL", new Vector3(0f, 0f, 25f));
                V(PrimitiveType.Cube, vis, new Vector3(0.6f, 4.55f, 0.2f), new Vector3(0.2f, 0.8f, 0.2f), m.bossAccent, "HornR", new Vector3(0f, 0f, -25f));
                V(PrimitiveType.Cube, vis, new Vector3(-1.75f, 2.2f, 0.3f), new Vector3(0.9f, 2.5f, 0.9f), m.boss, "ArmL");
                V(PrimitiveType.Cube, vis, new Vector3(1.75f, 2.2f, 0.3f), new Vector3(0.9f, 2.5f, 0.9f), m.boss, "ArmR");
                V(PrimitiveType.Cube, vis, new Vector3(1.9f, 1.4f, 1.5f), new Vector3(0.35f, 0.35f, 2.8f), m.bossAccent, "Greatsword");
                V(PrimitiveType.Cube, vis, new Vector3(1.9f, 1.4f, 0.2f), new Vector3(1f, 0.2f, 0.2f), m.chestTrim, "Guard");

                stats.enemyName = "Floor Boss"; stats.isBoss = true; stats.maxHP = 900; stats.damage = 18; stats.defense = 3;
                stats.expReward = 300; stats.knockbackResistance = 1f; stats.deathBurstColor = new Color(0.8f, 0.2f, 0.15f);
                boss.projectilePrefab = p.projectile;
                boss.visual = vis;
                loot.pickupPrefab = p.pickup;
                loot.goldChance = 1f; loot.goldRange = new Vector2Int(25, 45); loot.goldPiles = 4;
                loot.potionChance = 1f; loot.largePotionShare = 0.6f;
                loot.guaranteedEquipment = 2; loot.equipmentChance = 0.5f;
                loot.commonWeight = 25f; loot.rareWeight = 55f; loot.epicWeight = 20f;
                p.boss = SavePrefab<EnemyStats>(go, "Boss");
            }

            // ---------- Chest ----------
            {
                var go = new GameObject("TreasureChest");
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.55f, 0f);
                box.size = new Vector3(1.45f, 1.1f, 1.05f);
                var loot = go.AddComponent<LootDropper>();
                var chest = go.AddComponent<TreasureChest>();
                V(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.35f, 0f), new Vector3(1.4f, 0.7f, 1f), m.chest, "Base");
                V(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.38f, 0f), new Vector3(1.46f, 0.12f, 1.06f), m.chestTrim, "Band");
                var lid = new GameObject("LidPivot").transform;
                lid.SetParent(go.transform, false);
                lid.localPosition = new Vector3(0f, 0.7f, -0.5f);
                V(PrimitiveType.Cube, lid, new Vector3(0f, 0.15f, 0.5f), new Vector3(1.4f, 0.3f, 1f), m.chest, "Lid");
                V(PrimitiveType.Cube, lid, new Vector3(0f, 0.15f, 0.5f), new Vector3(1.46f, 0.1f, 1.06f), m.chestTrim, "LidBand");
                V(PrimitiveType.Cube, lid, new Vector3(0f, 0f, 1.03f), new Vector3(0.22f, 0.28f, 0.08f), m.gold, "Lock");
                chest.lidPivot = lid;
                loot.pickupPrefab = p.pickup;
                loot.goldChance = 1f; loot.goldRange = new Vector2Int(12, 24); loot.goldPiles = 2;
                loot.potionChance = 0.65f; loot.largePotionShare = 0.3f;
                loot.guaranteedEquipment = 1; loot.equipmentChance = 0.35f;
                loot.commonWeight = 50f; loot.rareWeight = 38f; loot.epicWeight = 12f;
                p.chest = SavePrefab<TreasureChest>(go, "TreasureChest");
            }

            return p;
        }

        static T LoadPrefab<T>(string name) where T : Component
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab");
            if (go == null) throw new System.Exception($"Prefab {name} not found");
            return go.GetComponent<T>();
        }

        /// <summary>NewScene unloads unused assets, so in-memory references must be re-fetched by path.</summary>
        static Prefabs LoadPrefabs() => new Prefabs
        {
            player = LoadPrefab<PlayerController>("Player"),
            skeleton = LoadPrefab<EnemyStats>("Enemy_Skeleton"),
            brute = LoadPrefab<EnemyStats>("Enemy_Brute"),
            caster = LoadPrefab<EnemyStats>("Enemy_Caster"),
            boss = LoadPrefab<EnemyStats>("Boss"),
            chest = LoadPrefab<TreasureChest>("TreasureChest"),
            pickup = LoadPrefab<WorldPickup>("Pickup"),
            projectile = LoadPrefab<Projectile>("Projectile"),
        };

        static void ReloadAssets(ref Mats m, ref ItemDatabaseSO db, ref FloorDatabaseSO floorDb)
        {
            m = CreateMaterials();
            db = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>($"{Root}/ScriptableObjects/ItemDatabase.asset");
            floorDb = AssetDatabase.LoadAssetAtPath<FloorDatabaseSO>($"{Root}/ScriptableObjects/FloorDatabase.asset");
        }

        static EnemyStats SaveEnemy(string name, Mats m, Prefabs p, float ccHeight, float ccRadius,
            System.Action<Transform> build, System.Action<EnemyStats, EnemyAI, LootDropper> setup)
        {
            var go = new GameObject(name);
            var cc = go.AddComponent<CharacterController>();
            cc.height = ccHeight;
            cc.radius = ccRadius;
            cc.center = new Vector3(0f, ccHeight * 0.5f, 0f);
            cc.stepOffset = 0.3f;
            var loot = go.AddComponent<LootDropper>();
            var stats = go.AddComponent<EnemyStats>();
            var ai = go.AddComponent<EnemyAI>();
            var bar = go.AddComponent<EnemyHealthBar>();
            bar.height = ccHeight + 0.5f;
            var vis = new GameObject("Visual").transform;
            vis.SetParent(go.transform, false);
            build(vis);
            ai.visual = vis;
            loot.pickupPrefab = p.pickup;
            setup(stats, ai, loot);
            return SavePrefab<EnemyStats>(go, name);
        }

        // =====================================================================
        // Dungeon scene
        // =====================================================================

        class Level
        {
            public Transform root;
            public Mats m;
            public readonly List<Renderer> floors = new List<Renderer>();
            public readonly List<Renderer> walls = new List<Renderer>();
            public readonly List<Light> torches = new List<Light>();
        }

        enum Side { North, South, East, West }

        static void FloorTile(Level lv, string name, float cx, float cz, float sx, float sz)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(lv.root, false);
            go.transform.localPosition = new Vector3(cx, -0.5f, cz);
            go.transform.localScale = new Vector3(sx, 1f, sz);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = lv.m.floor;
            lv.floors.Add(r);
        }

        static void WallBlock(Level lv, string name, Vector3 center, Vector3 size)
        {
            if (size.x <= 0.01f || size.z <= 0.01f) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(lv.root, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = lv.m.wall;
            lv.walls.Add(r);
        }

        static void WallX(Level lv, float z, float x0, float x1) =>
            WallBlock(lv, "Wall", new Vector3((x0 + x1) * 0.5f, WallHeight * 0.5f, z), new Vector3(x1 - x0, WallHeight, WallThickness));

        static void WallZ(Level lv, float x, float z0, float z1) =>
            WallBlock(lv, "Wall", new Vector3(x, WallHeight * 0.5f, (z0 + z1) * 0.5f), new Vector3(WallThickness, WallHeight, z1 - z0));

        static bool Has(Side[] s, Side x) { foreach (var v in s) if (v == x) return true; return false; }

        /// <summary>Rectangular room with walls on all sides and centred doorways on the listed sides.</summary>
        static void Room(Level lv, string name, float cx, float cz, float sx, float sz, params Side[] openings)
        {
            FloorTile(lv, name + "_Floor", cx, cz, sx, sz);
            float hx = sx * 0.5f, hz = sz * 0.5f, t = WallThickness * 0.5f, g = CorridorWidth * 0.5f;

            foreach (var side in new[] { Side.North, Side.South })
            {
                float z = side == Side.North ? cz + hz : cz - hz;
                float x0 = cx - hx - t, x1 = cx + hx + t;
                if (Has(openings, side)) { WallX(lv, z, x0, cx - g); WallX(lv, z, cx + g, x1); }
                else WallX(lv, z, x0, x1);
            }
            foreach (var side in new[] { Side.East, Side.West })
            {
                float x = side == Side.East ? cx + hx : cx - hx;
                float z0 = cz - hz, z1 = cz + hz;
                if (Has(openings, side)) { WallZ(lv, x, z0, cz - g); WallZ(lv, x, cz + g, z1); }
                else WallZ(lv, x, z0, z1);
            }
        }

        static void CorridorZ(Level lv, float x, float z0, float z1)
        {
            FloorTile(lv, "Corridor_Floor", x, (z0 + z1) * 0.5f, CorridorWidth, z1 - z0);
            float off = CorridorWidth * 0.5f + WallThickness * 0.5f;
            WallZ(lv, x - off, z0, z1);
            WallZ(lv, x + off, z0, z1);
        }

        static void CorridorX(Level lv, float z, float x0, float x1)
        {
            FloorTile(lv, "Corridor_Floor", (x0 + x1) * 0.5f, z, x1 - x0, CorridorWidth);
            float off = CorridorWidth * 0.5f + WallThickness * 0.5f;
            WallX(lv, z - off, x0, x1);
            WallX(lv, z + off, x0, x1);
        }

        static void Pillar(Level lv, float x, float z) =>
            WallBlock(lv, "Pillar", new Vector3(x, WallHeight * 0.5f + 0.3f, z), new Vector3(1.4f, WallHeight + 0.6f, 1.4f));

        static void Torch(Level lv, string name, Vector3 pos, float range = 16f, float intensity = 1.6f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(lv.root, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.color = new Color(1f, 0.72f, 0.42f);
            l.shadows = LightShadows.None;
            lv.torches.Add(l);
        }

        static EnemySpawnPoint Spawn(Transform parent, string name, Vector3 pos, EnemyKind kind, int count, bool extra = true)
        {
            var go = new GameObject("Spawn_" + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var sp = go.AddComponent<EnemySpawnPoint>();
            sp.kind = kind;
            sp.count = count;
            sp.allowExtraEnemies = extra;
            return sp;
        }

        static Camera CreateCamera(Vector3 pos, Quaternion rot, Color bg)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetPositionAndRotation(pos, rot);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;
            camGo.AddComponent<AudioListener>();
            return cam;
        }

        static void CreateSun(Vector3 euler, float intensity, Color color)
        {
            var go = new GameObject("Directional Light");
            go.transform.rotation = Quaternion.Euler(euler);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = color;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.6f;
        }

        static GameManager CreateGameManager(Mats m, ItemDatabaseSO db, FloorDatabaseSO floorDb)
        {
            var go = new GameObject("GameManager");
            var gm = go.AddComponent<GameManager>(); // AudioManager is added via RequireComponent
            gm.itemDatabase = db;
            gm.floorDatabase = floorDb;
            gm.unlitMaterial = m.unlit;
            gm.mainMenuScene = Path.GetFileNameWithoutExtension(MenuScenePath);
            gm.dungeonScene = Path.GetFileNameWithoutExtension(DungeonScenePath);
            return gm;
        }

        static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        static void ConfigureRenderSettings(Color ambient, Color fog)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 45f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        }

        static void BuildDungeonScene(Mats m, Prefabs p, ItemDatabaseSO db, FloorDatabaseSO floorDb)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ReloadAssets(ref m, ref db, ref floorDb);
            p = LoadPrefabs();
            var f1 = floorDb.floors[0];
            ConfigureRenderSettings(f1.ambientColor, f1.fogColor);

            CreateGameManager(m, db, floorDb);
            CreateSun(new Vector3(55f, -35f, 0f), 0.45f, new Color(0.75f, 0.78f, 1f));
            var cam = CreateCamera(new Vector3(0f, 16f, -14f), Quaternion.Euler(55f, 0f, 0f), f1.fogColor);
            var follow = cam.gameObject.AddComponent<CameraFollow>();

            // ---------------- Geometry ----------------
            var lv = new Level { m = m, root = new GameObject("Dungeon").transform };

            Room(lv, "StartRoom", 0f, 0f, 16f, 16f, Side.North);
            CorridorZ(lv, 0f, 8f, 19f);
            Room(lv, "MobRoomA", 0f, 28f, 20f, 18f, Side.South, Side.North, Side.East);
            CorridorX(lv, 28f, 10f, 17f);
            Room(lv, "TreasureAlcoveEast", 22f, 28f, 10f, 10f, Side.West);
            CorridorZ(lv, 0f, 37f, 45f);
            Room(lv, "MobRoomB", 0f, 54f, 22f, 18f, Side.South, Side.North, Side.West);
            CorridorX(lv, 54f, -18f, -11f);
            Room(lv, "TreasureAlcoveWest", -23f, 54f, 10f, 10f, Side.East);
            CorridorZ(lv, 0f, 63f, 74f);
            Room(lv, "BossRoom", 0f, 86f, 28f, 24f, Side.South);

            Pillar(lv, -6f, 32f); Pillar(lv, 6f, 24f);
            Pillar(lv, -7f, 58f); Pillar(lv, 7f, 58f); Pillar(lv, -8f, 48f); Pillar(lv, 8f, 48f);
            Pillar(lv, -10f, 80f); Pillar(lv, 10f, 80f); Pillar(lv, -10f, 93f); Pillar(lv, 10f, 93f);

            Torch(lv, "Light_Start", new Vector3(0f, 4f, 0f));
            Torch(lv, "Light_RoomA", new Vector3(0f, 4.5f, 28f), 18f);
            Torch(lv, "Light_AlcoveEast", new Vector3(22f, 3.5f, 28f), 10f, 2f);
            Torch(lv, "Light_RoomB", new Vector3(0f, 4.5f, 54f), 18f);
            Torch(lv, "Light_AlcoveWest", new Vector3(-23f, 3.5f, 54f), 10f, 2f);
            Torch(lv, "Light_BossW", new Vector3(-7f, 5f, 86f), 18f, 1.8f);
            Torch(lv, "Light_BossE", new Vector3(7f, 5f, 86f), 18f, 1.8f);
            Torch(lv, "Light_Corridors", new Vector3(0f, 4f, 68f), 10f, 1f);

            // Boss door (sealed when the fight starts)
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "BossDoor";
            door.transform.SetParent(lv.root, false);
            door.transform.localPosition = new Vector3(0f, WallHeight * 0.5f, 74f);
            door.transform.localScale = new Vector3(CorridorWidth + 0.2f, WallHeight, WallThickness);
            door.GetComponent<Renderer>().sharedMaterial = m.door;
            door.SetActive(false);

            var trigger = new GameObject("BossRoomTrigger");
            trigger.transform.position = new Vector3(0f, 2f, 79f);
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(26f, 4f, 6f);
            var bossTrigger = trigger.AddComponent<BossRoomTrigger>();

            // Exit portal
            var portalGo = new GameObject("ExitPortal");
            portalGo.transform.position = new Vector3(0f, 0f, 95f);
            var portal = portalGo.AddComponent<FloorPortal>();
            var pv = new GameObject("Visuals").transform;
            pv.SetParent(portalGo.transform, false);
            V(PrimitiveType.Cylinder, pv, new Vector3(0f, 0.05f, 0f), new Vector3(3.2f, 0.05f, 3.2f), m.portal, "Base");
            var spinner = new GameObject("Spinner").transform;
            spinner.SetParent(pv, false);
            spinner.localPosition = new Vector3(0f, 1.7f, 0f);
            V(PrimitiveType.Cube, spinner, Vector3.zero, new Vector3(1.2f, 2.6f, 0.15f), m.portal, "RuneA");
            V(PrimitiveType.Cube, spinner, Vector3.zero, new Vector3(0.15f, 2.6f, 1.2f), m.portal, "RuneB");
            var plGo = new GameObject("PortalLight");
            plGo.transform.SetParent(portalGo.transform, false);
            plGo.transform.localPosition = new Vector3(0f, 2f, 0f);
            var pl = plGo.AddComponent<Light>();
            pl.type = LightType.Point; pl.range = 9f; pl.intensity = 2.5f; pl.color = new Color(0.65f, 0.45f, 1f);
            portal.visuals = pv.gameObject;
            portal.spinner = spinner;
            portal.portalLight = pl;
            pv.gameObject.SetActive(false);
            pl.enabled = false;

            // Chests
            var chestEast = (GameObject)PrefabUtility.InstantiatePrefab(p.chest.gameObject);
            chestEast.transform.SetPositionAndRotation(new Vector3(24.5f, 0f, 28f), Quaternion.Euler(0f, -90f, 0f));
            chestEast.name = "Chest_EastAlcove";
            var chestWest = (GameObject)PrefabUtility.InstantiatePrefab(p.chest.gameObject);
            chestWest.transform.SetPositionAndRotation(new Vector3(-25.5f, 0f, 54f), Quaternion.Euler(0f, 90f, 0f));
            chestWest.name = "Chest_WestAlcove";

            // Spawn markers
            var markers = new GameObject("Markers").transform;
            var playerSpawn = new GameObject("PlayerSpawn").transform;
            playerSpawn.SetParent(markers, false);
            playerSpawn.localPosition = new Vector3(0f, 0.05f, -3f);
            var bossSpawn = new GameObject("BossSpawn").transform;
            bossSpawn.SetParent(markers, false);
            bossSpawn.localPosition = new Vector3(0f, 0.05f, 89f);
            bossSpawn.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var spawns = new[]
            {
                Spawn(markers, "A_West", new Vector3(-5f, 0.05f, 25f), EnemyKind.Melee, 2),
                Spawn(markers, "A_East", new Vector3(5f, 0.05f, 31f), EnemyKind.Melee, 1),
                Spawn(markers, "A_Back", new Vector3(0f, 0.05f, 34f), EnemyKind.Caster, 1),
                Spawn(markers, "AlcoveEast_Guard", new Vector3(20.5f, 0.05f, 30f), EnemyKind.Melee, 1, false),
                Spawn(markers, "B_West", new Vector3(-5f, 0.05f, 51f), EnemyKind.Brute, 1),
                Spawn(markers, "B_East", new Vector3(5f, 0.05f, 52f), EnemyKind.Melee, 2),
                Spawn(markers, "B_Back", new Vector3(0f, 0.05f, 59f), EnemyKind.Random, 2),
                Spawn(markers, "AlcoveWest_Guard", new Vector3(-21.5f, 0.05f, 56f), EnemyKind.Caster, 1, false),
            };

            // Player
            var playerGo = (GameObject)PrefabUtility.InstantiatePrefab(p.player.gameObject);
            playerGo.transform.SetPositionAndRotation(playerSpawn.position, Quaternion.identity);
            follow.target = playerGo.transform;
            follow.SnapToTarget();

            // Managers
            var managers = new GameObject("FloorManagers");
            var fm = managers.AddComponent<FloorManager>();
            managers.AddComponent<MissionManager>();
            fm.meleePrefab = p.skeleton;
            fm.brutePrefab = p.brute;
            fm.casterPrefab = p.caster;
            fm.bossPrefab = p.boss;
            fm.playerSpawn = playerSpawn;
            fm.bossSpawn = bossSpawn;
            fm.spawnPoints = spawns;
            fm.bossDoor = door;
            fm.bossTrigger = bossTrigger;
            fm.exitPortal = portal;
            fm.floorRenderers = lv.floors.ToArray();
            fm.wallRenderers = lv.walls.ToArray();
            fm.torchLights = lv.torches.ToArray();
            fm.fallbackFloor = f1;

            // UI
            var uiGo = new GameObject("UI");
            var ui = uiGo.AddComponent<UIManager>();
            ui.BuildUI();
            CreateEventSystem();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, DungeonScenePath))
                throw new System.Exception("Failed to save " + DungeonScenePath);
        }

        // =====================================================================
        // Main menu scene
        // =====================================================================

        static void BuildMainMenuScene(Mats m, ItemDatabaseSO db, FloorDatabaseSO floorDb)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ReloadAssets(ref m, ref db, ref floorDb);
            ConfigureRenderSettings(new Color(0.2f, 0.2f, 0.25f), new Color(0.03f, 0.03f, 0.05f));
            RenderSettings.fogStartDistance = 8f;
            RenderSettings.fogEndDistance = 30f;

            CreateGameManager(m, db, floorDb);
            CreateSun(new Vector3(40f, 150f, 0f), 0.35f, new Color(0.7f, 0.75f, 1f));
            CreateCamera(new Vector3(0f, 2.3f, -6.5f), Quaternion.Euler(8f, 0f, 0f), new Color(0.03f, 0.03f, 0.05f));

            var stage = new GameObject("Stage").transform;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Pedestal";
            floor.transform.SetParent(stage, false);
            floor.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            floor.transform.localScale = new Vector3(5f, 0.1f, 5f);
            floor.GetComponent<Renderer>().sharedMaterial = m.menuFloor;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(stage, false);
            ground.transform.localPosition = new Vector3(0f, -0.7f, 6f);
            ground.transform.localScale = new Vector3(60f, 1f, 40f);
            ground.GetComponent<Renderer>().sharedMaterial = m.wall;
            for (int i = -1; i <= 1; i += 2)
            {
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = "Pillar";
                pillar.transform.SetParent(stage, false);
                pillar.transform.localPosition = new Vector3(4.5f * i, 2.5f, 3f);
                pillar.transform.localScale = new Vector3(1.2f, 6f, 1.2f);
                pillar.GetComponent<Renderer>().sharedMaterial = m.wall;
                var l = new GameObject("Torch").AddComponent<Light>();
                l.transform.SetParent(stage, false);
                l.transform.localPosition = new Vector3(3.6f * i, 3.2f, 2f);
                l.type = LightType.Point; l.range = 9f; l.intensity = 2.2f; l.color = new Color(1f, 0.65f, 0.35f);
            }

            var knight = new GameObject("TitleKnight");
            knight.transform.SetParent(stage, false);
            knight.transform.localRotation = Quaternion.Euler(0f, 200f, 0f);
            var pivot = BuildKnightVisual(knight.transform, m);
            pivot.localEulerAngles = new Vector3(-60f, 20f, 0f);
            knight.AddComponent<MenuSpinner>().degreesPerSecond = 18f;

            var uiGo = new GameObject("MenuUI");
            var menu = uiGo.AddComponent<MainMenuUI>();
            menu.BuildUI();
            CreateEventSystem();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, MenuScenePath))
                throw new System.Exception("Failed to save " + MenuScenePath);
        }
    }
}

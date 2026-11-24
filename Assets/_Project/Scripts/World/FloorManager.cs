using System.Collections.Generic;
using UnityEngine;

namespace SoloKnight
{
    /// <summary>
    /// Sets up the current floor: theme colours, enemy spawns with floor scaling, the mission,
    /// the sealed boss room and the exit portal. The same scene layout is reused for every floor.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class FloorManager : MonoBehaviour
    {
        public static FloorManager Instance { get; private set; }

        [Header("Enemy Prefabs")]
        public EnemyStats meleePrefab;
        public EnemyStats brutePrefab;
        public EnemyStats casterPrefab;
        public EnemyStats bossPrefab;

        [Header("Layout")]
        public Transform playerSpawn;
        public Transform bossSpawn;
        public EnemySpawnPoint[] spawnPoints;
        public GameObject bossDoor;
        public BossRoomTrigger bossTrigger;
        public FloorPortal exitPortal;

        [Header("Theming")]
        public Renderer[] floorRenderers;
        public Renderer[] wallRenderers;
        public Light[] torchLights;

        [Header("Fallback (used if no GameManager/FloorDatabase is present)")]
        public FloorDataSO fallbackFloor;

        public int FloorNumber { get; private set; } = 1;
        public FloorDataSO CurrentFloor { get; private set; }
        public EnemyStats Boss { get; private set; }
        public bool BossFightStarted { get; private set; }
        public bool BossDefeated { get; private set; }
        public int EnemiesRemaining => alive.Count;
        public int TotalEnemies { get; private set; }

        readonly List<EnemyStats> alive = new List<EnemyStats>();

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            var gm = GameManager.Instance;
            FloorNumber = gm != null ? gm.Data.currentFloor : 1;
            if (gm != null && gm.floorDatabase != null) CurrentFloor = gm.floorDatabase.GetFloor(FloorNumber);
            if (CurrentFloor == null) CurrentFloor = fallbackFloor != null ? fallbackFloor : FloorDataSO.CreateProcedural(null, FloorNumber);

            ApplyTheme();
            if (bossDoor != null) bossDoor.SetActive(false);

            var pc = PlayerController.Instance;
            if (pc != null && playerSpawn != null) pc.Teleport(playerSpawn.position, playerSpawn.rotation);

            SpawnEnemies();
            SpawnBoss();

            int chests = FindObjectsByType<TreasureChest>().Length;
            if (MissionManager.Instance != null)
                MissionManager.Instance.SetupMission(FloorNumber, TotalEnemies, chests, CurrentFloor.goldMultiplier);

            if (UIManager.Instance != null) UIManager.Instance.ShowFloorTitle(FloorNumber, CurrentFloor.floorName);
        }

        void ApplyTheme()
        {
            var f = CurrentFloor;
            TintAll(floorRenderers, f.floorColor);
            TintAll(wallRenderers, f.wallColor);
            if (torchLights != null)
                foreach (var l in torchLights) if (l != null) l.color = f.torchColor;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = f.ambientColor;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = f.fogColor;
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 45f;
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = f.fogColor;
        }

        static void TintAll(Renderer[] renderers, Color color)
        {
            if (renderers == null || renderers.Length == 0) return;
            Material shared = null;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (shared == null) shared = MaterialUtil.CreateTinted(r.sharedMaterial, color);
                r.sharedMaterial = shared;
            }
        }

        EnemyStats PickPrefab(EnemyKind kind)
        {
            if (kind == EnemyKind.Random)
            {
                float r = Random.value;
                if (r < CurrentFloor.bruteChance) kind = EnemyKind.Brute;
                else if (r < CurrentFloor.bruteChance + CurrentFloor.casterChance) kind = EnemyKind.Caster;
                else kind = EnemyKind.Melee;
            }
            switch (kind)
            {
                case EnemyKind.Brute: return brutePrefab != null ? brutePrefab : meleePrefab;
                case EnemyKind.Caster: return casterPrefab != null ? casterPrefab : meleePrefab;
                default: return meleePrefab;
            }
        }

        void SpawnEnemies()
        {
            alive.Clear();
            TotalEnemies = 0;
            if (spawnPoints == null) return;
            var f = CurrentFloor;
            foreach (var sp in spawnPoints)
            {
                if (sp == null) continue;
                int n = sp.count + (sp.allowExtraEnemies ? f.extraEnemiesPerSpawn : 0);
                for (int i = 0; i < n; i++)
                {
                    var prefab = PickPrefab(sp.kind);
                    if (prefab == null) continue;
                    Vector2 c = Random.insideUnitCircle * sp.radius;
                    Vector3 pos = sp.transform.position + new Vector3(c.x, 0f, c.y);
                    var e = Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    e.ApplyScaling(f.enemyHealthMultiplier, f.enemyDamageMultiplier, f.expMultiplier, f.goldMultiplier, f.rarityBonus);
                    alive.Add(e);
                    TotalEnemies++;
                }
            }
        }

        void SpawnBoss()
        {
            if (bossPrefab == null || bossSpawn == null) return;
            var f = CurrentFloor;
            Boss = Instantiate(bossPrefab, bossSpawn.position, bossSpawn.rotation);
            Boss.enemyName = f.bossName;
            Boss.ApplyScaling(f.bossHealthMultiplier, f.bossDamageMultiplier, f.expMultiplier, f.goldMultiplier, f.rarityBonus + 0.5f);
            Boss.name = "Boss_" + f.bossName;
        }

        public void StartBossFight()
        {
            if (BossFightStarted || BossDefeated || Boss == null) return;
            BossFightStarted = true;
            if (bossDoor != null) bossDoor.SetActive(true);
            CameraFollow.Shake(0.3f, 0.3f);
            UIManager.Toast("The entrance seals behind you...", new Color(1f, 0.6f, 0.5f));
            var ai = Boss.GetComponent<BossAI>();
            if (ai != null) ai.Activate();
            if (UIManager.Instance != null) UIManager.Instance.ShowBossBar(Boss);
        }

        public void OnEnemyDied(EnemyStats enemy)
        {
            alive.Remove(enemy);
            if (enemy != null && enemy == Boss) OnBossDefeated();
        }

        void OnBossDefeated()
        {
            if (BossDefeated) return;
            BossDefeated = true;
            if (bossDoor != null) bossDoor.SetActive(false);
            if (exitPortal != null) exitPortal.Activate();
            if (UIManager.Instance != null) UIManager.Instance.HideBossBar();
            if (GameManager.Instance != null) GameManager.Instance.OnFloorCleared(FloorNumber);
            if (UIManager.Instance != null) UIManager.Instance.ShowVictory(FloorNumber, CurrentFloor.floorName, CurrentFloor.bossName);
        }
    }
}

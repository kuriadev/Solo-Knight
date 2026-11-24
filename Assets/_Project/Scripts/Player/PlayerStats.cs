using System;
using UnityEngine;

namespace SoloKnight
{
    /// <summary>Level, EXP, the five allocatable stats, HP and damage intake for the player.</summary>
    [RequireComponent(typeof(InventorySystem))]
    public class PlayerStats : MonoBehaviour, IDamageable
    {
        public const float CritMultiplier = 1.75f;
        public const float CritCap = 0.85f;

        [Header("Base Stats")]
        public int baseAttack = 10;
        public int baseDefense = 2;
        [Range(0f, 1f)] public float baseCritChance = 0.05f;
        public float baseMoveSpeed = 6f;
        public int baseMaxHP = 100;

        [Header("Gain per Stat Point")]
        public int attackPerPoint = 2;
        public int defensePerPoint = 1;
        public float critPerPoint = 0.02f;
        public float moveSpeedPerPoint = 0.25f;
        public int maxHPPerPoint = 15;

        [Header("Leveling")]
        public int statPointsPerLevel = 3;
        public int maxHPPerLevel = 5;

        [Header("Feedback")]
        public float hurtInvulnerability = 0.35f;

        public int Level { get; private set; } = 1;
        public int Exp { get; private set; }
        public int StatPoints { get; private set; }
        public int CurrentHP { get; private set; }
        public bool IsDead { get; private set; }

        readonly int[] allocated = new int[5];
        InventorySystem inventory;
        PlayerController controller;
        float invulnerableUntil;

        Renderer[] renderers;
        Material[] originalMaterials;
        Material hurtMaterial;
        float flashUntil;
        bool flashing;

        public event Action OnStatsChanged;
        public event Action<int> OnLevelUp;
        public event Action OnDied;

        // ---------------- Derived stats ----------------
        public int Attack => baseAttack + allocated[(int)StatType.Attack] * attackPerPoint + inventory.BonusAttack;
        public int Defense => baseDefense + allocated[(int)StatType.Defense] * defensePerPoint + inventory.BonusDefense;
        public float CritChance => Mathf.Min(CritCap, baseCritChance + allocated[(int)StatType.CritChance] * critPerPoint + inventory.BonusCritChance);
        public float MoveSpeed => Mathf.Max(1f, baseMoveSpeed + allocated[(int)StatType.MoveSpeed] * moveSpeedPerPoint + inventory.BonusMoveSpeed);
        public int MaxHP => baseMaxHP + allocated[(int)StatType.Health] * maxHPPerPoint + (Level - 1) * maxHPPerLevel + inventory.BonusMaxHP;
        public int ExpToNextLevel => ExpRequired(Level);
        public bool IsInvulnerable => Time.time < invulnerableUntil;
        public float DamageReductionPercent => 1f - 100f / (100f + Defense * 6f);

        public int GetAllocated(StatType t) => allocated[(int)t];

        public static int ExpRequired(int level) => Mathf.RoundToInt(60f + 40f * (level - 1) + 8f * Mathf.Pow(level - 1, 1.6f));

        void Awake()
        {
            inventory = GetComponent<InventorySystem>();
            controller = GetComponent<PlayerController>();
            CurrentHP = MaxHP;

            renderers = GetComponentsInChildren<Renderer>();
            originalMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) originalMaterials[i] = renderers[i].sharedMaterial;
            hurtMaterial = MaterialUtil.CreateTinted(renderers.Length > 0 ? renderers[0].sharedMaterial : null, new Color(1f, 0.25f, 0.25f));
        }

        void OnEnable() { inventory.OnEquipmentChanged += HandleEquipmentChanged; }
        void OnDisable() { inventory.OnEquipmentChanged -= HandleEquipmentChanged; }

        void Update()
        {
            if (flashing && Time.time >= flashUntil) SetFlash(false);
        }

        void HandleEquipmentChanged()
        {
            CurrentHP = Mathf.Min(CurrentHP, MaxHP);
            OnStatsChanged?.Invoke();
        }

        // ---------------- Progression ----------------

        public void AddExp(int amount)
        {
            if (amount <= 0 || IsDead) return;
            Exp += amount;
            while (Exp >= ExpToNextLevel)
            {
                Exp -= ExpToNextLevel;
                Level++;
                StatPoints += statPointsPerLevel;
                CurrentHP = MaxHP;
                OnLevelUp?.Invoke(Level);
                AudioManager.Play(Sfx.LevelUp, 1f, 0f);
                SimpleVfx.Ring(transform.position, new Color(1f, 0.85f, 0.3f), 0.5f, 4f, 0.7f);
                SimpleVfx.Burst(transform.position + Vector3.up, new Color(1f, 0.9f, 0.4f), 16, 8f, 0.18f);
                if (UIManager.Instance != null) UIManager.Instance.ShowLevelUp(Level, StatPoints);
                if (GameManager.Instance != null) GameManager.Instance.SaveGame();
            }
            OnStatsChanged?.Invoke();
        }

        public bool AllocateStat(StatType stat)
        {
            if (StatPoints <= 0 || IsDead) return false;
            StatPoints--;
            allocated[(int)stat]++;
            if (stat == StatType.Health) CurrentHP = MaxHP; // HP upgrade fully restores health
            AudioManager.Play(Sfx.UiClick);
            OnStatsChanged?.Invoke();
            return true;
        }

        // ---------------- Combat ----------------

        /// <summary>Damage roll for an outgoing attack, including variance and crits.</summary>
        public int RollDamage(float multiplier, out bool isCrit)
        {
            float dmg = Attack * multiplier * UnityEngine.Random.Range(0.9f, 1.1f);
            isCrit = UnityEngine.Random.value < CritChance;
            if (isCrit) dmg *= CritMultiplier;
            return Mathf.Max(1, Mathf.RoundToInt(dmg));
        }

        public int CalculateIncomingDamage(int raw) => Mathf.Max(1, Mathf.RoundToInt(raw * 100f / (100f + Defense * 6f)));

        public void TakeHit(DamageInfo info)
        {
            if (IsDead || IsInvulnerable) return;
            int dmg = CalculateIncomingDamage(info.amount);
            CurrentHP = Mathf.Max(0, CurrentHP - dmg);
            invulnerableUntil = Time.time + hurtInvulnerability;

            UIManager.DamageNumber(transform.position + Vector3.up * 2f, dmg, new Color(1f, 0.3f, 0.3f), false);
            AudioManager.Play(Sfx.PlayerHurt);
            CameraFollow.Shake(0.25f, 0.2f);
            SetFlash(true);
            flashUntil = Time.time + 0.12f;

            if (controller != null && info.knockback > 0f)
            {
                Vector3 dir = transform.position - info.sourcePosition;
                dir.y = 0f;
                controller.ApplyKnockback(dir.normalized * info.knockback);
            }

            OnStatsChanged?.Invoke();
            if (CurrentHP <= 0) Die();
        }

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0) return;
            int before = CurrentHP;
            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
            int healed = CurrentHP - before;
            if (healed > 0) UIManager.FloatingText(transform.position + Vector3.up * 2.2f, $"+{healed}", new Color(0.35f, 1f, 0.45f), 30);
            OnStatsChanged?.Invoke();
        }

        public void SetInvulnerable(float duration)
        {
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration);
        }

        void Die()
        {
            if (IsDead) return;
            IsDead = true;
            SetFlash(false);
            AudioManager.Play(Sfx.Death, 1f, 0f);
            SimpleVfx.Burst(transform.position + Vector3.up, new Color(0.8f, 0.15f, 0.15f), 20, 7f);
            transform.localScale = new Vector3(1f, 0.35f, 1f);
            OnDied?.Invoke();
            if (GameManager.Instance != null) GameManager.Instance.OnPlayerDied();
        }

        void SetFlash(bool on)
        {
            flashing = on;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].sharedMaterial = on ? hurtMaterial : originalMaterials[i];
        }

        // ---------------- Save / Load ----------------

        public void ImportFrom(SaveData d)
        {
            Level = Mathf.Max(1, d.level);
            Exp = Mathf.Max(0, d.exp);
            StatPoints = Mathf.Max(0, d.statPoints);
            for (int i = 0; i < 5; i++) allocated[i] = d.allocatedStats[i];
            IsDead = false;
            CurrentHP = MaxHP;
            OnStatsChanged?.Invoke();
        }

        public void ExportTo(SaveData d)
        {
            d.level = Level;
            d.exp = Exp;
            d.statPoints = StatPoints;
            for (int i = 0; i < 5; i++) d.allocatedStats[i] = allocated[i];
        }
    }
}

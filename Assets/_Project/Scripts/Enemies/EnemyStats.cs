using System;
using System.Collections;
using UnityEngine;

namespace SoloKnight
{
    /// <summary>Health, damage, rewards and hit/death feedback for regular enemies and bosses.</summary>
    [RequireComponent(typeof(LootDropper))]
    public class EnemyStats : MonoBehaviour, IDamageable
    {
        [Header("Identity")]
        public string enemyName = "Skeleton";
        public bool isBoss;

        [Header("Stats")]
        public int maxHP = 40;
        public int damage = 8;
        public int defense = 0;
        [Range(0f, 1f)] public float knockbackResistance = 0f;

        [Header("Rewards")]
        public int expReward = 15;

        [Header("Feedback")]
        public Color deathBurstColor = new Color(0.85f, 0.85f, 0.75f);

        public int CurrentHP { get; private set; }
        public bool IsDead { get; private set; }

        public event Action<EnemyStats> OnDeath;
        public event Action<EnemyStats> OnDamaged;

        LootDropper loot;
        EnemyAI ai;
        Renderer[] renderers;
        Material[] originalMaterials;
        Material flashMaterial;
        Material telegraphMaterial;
        float flashUntil;
        bool flashing;
        bool telegraphing;

        void Awake()
        {
            CurrentHP = maxHP;
            loot = GetComponent<LootDropper>();
            ai = GetComponent<EnemyAI>();
            renderers = GetComponentsInChildren<Renderer>();
            originalMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) originalMaterials[i] = renderers[i].sharedMaterial;
            Material template = renderers.Length > 0 ? renderers[0].sharedMaterial : null;
            flashMaterial = MaterialUtil.CreateTinted(template, Color.white);
            telegraphMaterial = MaterialUtil.CreateTinted(template, new Color(1f, 0.45f, 0.1f));
        }

        void Update()
        {
            if (flashing && Time.time >= flashUntil)
            {
                flashing = false;
                ApplyMaterials();
            }
        }

        /// <summary>Applies per-floor difficulty and reward scaling. Call right after spawning.</summary>
        public void ApplyScaling(float healthMult, float damageMult, float expMult, float goldMult, float rarityBonus)
        {
            maxHP = Mathf.Max(1, Mathf.RoundToInt(maxHP * healthMult));
            CurrentHP = maxHP;
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * damageMult));
            expReward = Mathf.Max(1, Mathf.RoundToInt(expReward * expMult));
            if (loot != null)
            {
                loot.goldMultiplier = goldMult;
                loot.rarityBonus = rarityBonus;
            }
        }

        public void TakeHit(DamageInfo info)
        {
            if (IsDead) return;
            int dmg = Mathf.Max(1, info.amount - defense);
            CurrentHP = Mathf.Max(0, CurrentHP - dmg);

            Color c = info.isCrit ? new Color(1f, 0.85f, 0.15f) : Color.white;
            UIManager.DamageNumber(transform.position + Vector3.up * (isBoss ? 3.6f : 2.2f), dmg, c, info.isCrit);

            flashing = true;
            flashUntil = Time.time + 0.08f;
            ApplyMaterials();

            if (ai != null)
            {
                ai.Alert();
                if (info.knockback > 0f)
                {
                    Vector3 dir = transform.position - info.sourcePosition;
                    dir.y = 0f;
                    ai.ApplyKnockback(dir.normalized * info.knockback * (1f - knockbackResistance));
                }
            }

            OnDamaged?.Invoke(this);
            if (CurrentHP <= 0) Die();
        }

        /// <summary>Orange tint while winding up an attack, so the player can react.</summary>
        public void SetTelegraph(bool on)
        {
            if (telegraphing == on) return;
            telegraphing = on;
            ApplyMaterials();
        }

        void ApplyMaterials()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].sharedMaterial = flashing ? flashMaterial : telegraphing ? telegraphMaterial : originalMaterials[i];
            }
        }

        void Die()
        {
            if (IsDead) return;
            IsDead = true;
            flashing = false;
            telegraphing = false;
            ApplyMaterials();

            var player = PlayerController.Instance;
            if (player != null && player.Stats != null)
            {
                player.Stats.AddExp(expReward);
                UIManager.FloatingText(transform.position + Vector3.up * 1.4f, $"+{expReward} EXP", new Color(0.55f, 0.8f, 1f), 22);
            }

            if (loot != null) loot.DropLoot();
            if (GameManager.Instance != null) GameManager.Instance.RecordKill();
            if (MissionManager.Instance != null) MissionManager.Instance.ReportKill();

            AudioManager.Play(Sfx.Death, isBoss ? 1f : 0.6f);
            SimpleVfx.Burst(transform.position + Vector3.up, deathBurstColor, isBoss ? 40 : 12, isBoss ? 10f : 6f, isBoss ? 0.35f : 0.22f);
            if (isBoss) CameraFollow.Shake(0.6f, 0.6f);

            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            OnDeath?.Invoke(this);
            if (FloorManager.Instance != null) FloorManager.Instance.OnEnemyDied(this);
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            Vector3 start = transform.localScale;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.35f;
                transform.localScale = Vector3.Lerp(start, new Vector3(start.x * 1.2f, 0.05f, start.z * 1.2f), t);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}

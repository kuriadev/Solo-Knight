using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoloKnight
{
    /// <summary>
    /// Left mouse: 3-hit melee combo with arc hit detection and knockback.
    /// Right mouse / Q: Whirlwind AoE cleave with cooldown.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Combo")]
        public float[] comboMultipliers = { 1f, 1.15f, 1.6f };
        public float attackRange = 2.5f;
        [Range(30f, 360f)] public float attackArc = 120f;
        public float attackInterval = 0.3f;
        public float finisherExtraDelay = 0.15f;
        public float comboResetTime = 0.8f;
        public float hitDelay = 0.08f;
        public float knockbackForce = 7f;
        [Range(0f, 1f)] public float attackMoveMultiplier = 0.45f;

        [Header("Whirlwind (Special)")]
        public float whirlwindMultiplier = 2.2f;
        public float whirlwindRadius = 3.8f;
        public float whirlwindCooldown = 6f;
        public float whirlwindDuration = 0.45f;
        public float whirlwindKnockback = 12f;

        [Header("Visuals")]
        public Transform swordPivot;
        public Vector3 swordRestEuler = new Vector3(0f, 45f, 0f);

        public int ComboStep { get; private set; }
        public float SpecialCooldownRemaining => Mathf.Max(0f, specialTimer);
        public float SpecialCooldownNormalized => whirlwindCooldown <= 0f ? 0f : Mathf.Clamp01(specialTimer / whirlwindCooldown);
        public float MoveSpeedMultiplier => Time.time < moveLockUntil ? attackMoveMultiplier : 1f;
        public bool IsBusy => Time.time < moveLockUntil;

        PlayerStats stats;
        PlayerController controller;
        float nextAttackTime;
        float lastAttackTime = -10f;
        float specialTimer;
        float moveLockUntil;
        Coroutine activeRoutine;
        readonly HashSet<EnemyStats> hitBuffer = new HashSet<EnemyStats>();
        readonly Collider[] overlap = new Collider[48];

        void Awake()
        {
            stats = GetComponent<PlayerStats>();
            controller = GetComponent<PlayerController>();
            ResetSword();
        }

        void Update()
        {
            specialTimer -= Time.deltaTime;
            if (stats.IsDead) return;
            if (GameManager.Instance != null && GameManager.Instance.IsGameplayBlocked) return;
            if (controller != null && controller.IsDashing) return;

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Q)) TryWhirlwind();
            else if (Input.GetMouseButton(0)) TryAttack();
        }

        void TryAttack()
        {
            if (Time.time < nextAttackTime) return;
            if (Time.time - lastAttackTime > comboResetTime) ComboStep = 0;

            int step = ComboStep;
            bool finisher = step == comboMultipliers.Length - 1;
            ComboStep = (ComboStep + 1) % comboMultipliers.Length;
            lastAttackTime = Time.time;
            nextAttackTime = Time.time + attackInterval + (finisher ? finisherExtraDelay : 0f);
            moveLockUntil = Time.time + attackInterval;

            if (controller != null) controller.SnapToAim();
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(SwingRoutine(step, finisher));
        }

        IEnumerator SwingRoutine(int step, bool finisher)
        {
            AudioManager.Play(Sfx.Swing, finisher ? 1f : 0.8f);
            float dir = step % 2 == 0 ? 1f : -1f;
            float duration = attackInterval * 0.9f;
            float t = 0f;
            bool hitDone = false;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - k, 3f);
                if (swordPivot != null)
                {
                    float yaw = Mathf.Lerp(-80f * dir, 80f * dir, eased);
                    float pitch = finisher ? Mathf.Lerp(-40f, 30f, eased) : 0f;
                    swordPivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);
                }
                if (!hitDone && t >= hitDelay)
                {
                    hitDone = true;
                    float range = finisher ? attackRange * 1.15f : attackRange;
                    DoHit(comboMultipliers[step], range, attackArc, knockbackForce * (finisher ? 1.6f : 1f), finisher);
                }
                yield return null;
            }
            ResetSword();
            activeRoutine = null;
        }

        void TryWhirlwind()
        {
            if (specialTimer > 0f) return;
            specialTimer = whirlwindCooldown;
            moveLockUntil = Time.time + whirlwindDuration;
            nextAttackTime = Time.time + whirlwindDuration;
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(WhirlwindRoutine());
        }

        IEnumerator WhirlwindRoutine()
        {
            AudioManager.Play(Sfx.Whirlwind, 1f, 0f);
            SimpleVfx.Ring(transform.position, new Color(0.55f, 0.85f, 1f), 0.6f, whirlwindRadius, whirlwindDuration);
            float t = 0f;
            bool hitDone = false;
            while (t < whirlwindDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / whirlwindDuration);
                if (swordPivot != null) swordPivot.localRotation = Quaternion.Euler(0f, k * 720f, 0f);
                if (!hitDone && k >= 0.3f)
                {
                    hitDone = true;
                    DoHit(whirlwindMultiplier, whirlwindRadius, 360f, whirlwindKnockback, true);
                }
                yield return null;
            }
            ResetSword();
            activeRoutine = null;
        }

        public void CancelAttack()
        {
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = null;
            moveLockUntil = 0f;
            ResetSword();
        }

        void ResetSword()
        {
            if (swordPivot != null) swordPivot.localRotation = Quaternion.Euler(swordRestEuler);
        }

        /// <summary>Damages every enemy inside a cone (or full circle) in front of the player.</summary>
        void DoHit(float multiplier, float range, float arc, float knockback, bool heavy)
        {
            hitBuffer.Clear();
            Vector3 origin = transform.position;
            int count = Physics.OverlapSphereNonAlloc(origin + Vector3.up, range, overlap, ~0, QueryTriggerInteraction.Ignore);
            int hits = 0;
            bool anyCrit = false;
            for (int i = 0; i < count; i++)
            {
                var enemy = overlap[i].GetComponentInParent<EnemyStats>();
                if (enemy == null || enemy.IsDead || hitBuffer.Contains(enemy)) continue;

                Vector3 to = enemy.transform.position - origin;
                to.y = 0f;
                if (arc < 360f && to.sqrMagnitude > 0.8f && Vector3.Angle(transform.forward, to) > arc * 0.5f) continue;

                hitBuffer.Add(enemy);
                int dmg = stats.RollDamage(multiplier, out bool crit);
                anyCrit |= crit;
                enemy.TakeHit(new DamageInfo(dmg, origin, knockback, crit));
                hits++;
            }

            if (hits > 0)
            {
                AudioManager.Play(anyCrit ? Sfx.Crit : Sfx.Hit);
                CameraFollow.Shake(heavy || anyCrit ? 0.22f : 0.1f, 0.12f);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, attackRange);
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, whirlwindRadius);
        }
    }
}

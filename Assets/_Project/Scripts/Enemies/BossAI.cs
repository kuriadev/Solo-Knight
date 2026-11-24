using System.Collections;
using UnityEngine;

namespace SoloKnight
{
    /// <summary>
    /// Multi-attack boss: Cleave (close), Ground Slam (AoE), Charge (line dash) and Projectile Nova.
    /// Enrages below 50% HP (faster attacks, double nova). Activated by the boss room trigger.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(EnemyStats))]
    public class BossAI : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 3.6f;
        public float turnSharpness = 6f;
        public float gravity = -30f;

        [Header("Pacing")]
        public float timeBetweenAttacks = 1.2f;
        public float enragedSpeedMultiplier = 1.35f;

        [Header("Cleave")]
        public float cleaveRange = 3.6f;
        [Range(30f, 360f)] public float cleaveArc = 150f;
        public float cleaveWindup = 0.55f;

        [Header("Ground Slam")]
        public float slamRadius = 5.5f;
        public float slamWindup = 1.0f;
        public float slamDamageMultiplier = 1.6f;

        [Header("Charge")]
        public float chargeSpeed = 18f;
        public float chargeDistance = 14f;
        public float chargeWindup = 0.8f;
        public float chargeDamageMultiplier = 1.3f;

        [Header("Projectile Nova")]
        public Projectile projectilePrefab;
        public int novaCount = 12;
        public float projectileSpeed = 9f;
        public float novaWindup = 0.6f;

        [Header("Visual")]
        public Transform visual;

        public bool IsActive { get; private set; }
        public bool IsEnraged { get; private set; }

        static readonly Color TelegraphColor = new Color(0.95f, 0.15f, 0.1f);

        CharacterController cc;
        EnemyStats stats;
        float verticalVelocity;
        float idleTimer;
        bool busy;
        Vector3 visualBaseScale = Vector3.one;
        Coroutine attackRoutine;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            stats = GetComponent<EnemyStats>();
            if (visual != null) visualBaseScale = visual.localScale;
        }

        public void Activate()
        {
            if (IsActive) return;
            IsActive = true;
            idleTimer = 1.5f;
            AudioManager.Play(Sfx.BossRoar, 1f, 0f);
            CameraFollow.Shake(0.4f, 0.6f);
        }

        float SpeedMult => IsEnraged ? enragedSpeedMultiplier : 1f;

        void Update()
        {
            if (stats.IsDead)
            {
                if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
                return;
            }
            if (!cc.enabled) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (!IsEnraged && stats.CurrentHP <= stats.maxHP * 0.5f && IsActive)
            {
                IsEnraged = true;
                UIManager.Toast($"{stats.enemyName} is ENRAGED!", new Color(1f, 0.35f, 0.25f));
                AudioManager.Play(Sfx.BossRoar, 1f, 0f);
                CameraFollow.Shake(0.35f, 0.5f);
                SimpleVfx.Ring(transform.position, new Color(1f, 0.3f, 0.1f), 1f, 7f, 0.6f);
            }

            Vector3 move = Vector3.zero;
            var pc = PlayerController.Instance;
            if (IsActive && !busy && pc != null && !pc.Stats.IsDead)
            {
                Vector3 to = pc.transform.position - transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                Face(to, dt);
                if (dist > cleaveRange * 0.8f) move = to.normalized * moveSpeed * SpeedMult;

                idleTimer -= dt;
                if (idleTimer <= 0f) attackRoutine = StartCoroutine(AttackRoutine(pc, dist));
            }

            if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;
            move.y = verticalVelocity;
            cc.Move(move * dt);
        }

        void Face(Vector3 dir, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-turnSharpness * dt));
        }

        IEnumerator AttackRoutine(PlayerController pc, float dist)
        {
            busy = true;
            float r = Random.value;
            if (dist <= cleaveRange + 1f)
            {
                if (r < 0.55f) yield return Cleave(pc);
                else if (r < 0.85f) yield return Slam(pc);
                else yield return Nova();
            }
            else
            {
                if (r < 0.45f) yield return Charge(pc);
                else if (r < 0.8f) yield return Nova();
                else yield return Slam(pc);
            }
            if (visual != null) visual.localScale = visualBaseScale;
            stats.SetTelegraph(false);
            idleTimer = timeBetweenAttacks / SpeedMult;
            busy = false;
            attackRoutine = null;
        }

        IEnumerator Windup(float duration, PlayerController trackTarget)
        {
            stats.SetTelegraph(true);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (visual != null) visual.localScale = visualBaseScale * (1f + 0.12f * Mathf.Clamp01(t / duration));
                if (trackTarget != null) Face(trackTarget.transform.position - transform.position, Time.deltaTime);
                yield return null;
            }
            stats.SetTelegraph(false);
            if (visual != null) visual.localScale = visualBaseScale;
        }

        bool PlayerInRange(PlayerController pc, float range, float arc)
        {
            if (pc == null || pc.Stats.IsDead) return false;
            Vector3 to = pc.transform.position - transform.position;
            to.y = 0f;
            if (to.magnitude > range) return false;
            return arc >= 360f || Vector3.Angle(transform.forward, to) <= arc * 0.5f;
        }

        IEnumerator Cleave(PlayerController pc)
        {
            int swings = IsEnraged ? 3 : 2;
            for (int i = 0; i < swings; i++)
            {
                float windup = cleaveWindup / SpeedMult;
                SimpleVfx.CircleTelegraph(transform.position + transform.forward * cleaveRange * 0.45f, cleaveRange * 0.65f, windup, TelegraphColor);
                yield return Windup(windup, pc);
                AudioManager.Play(Sfx.Swing, 1f);
                if (PlayerInRange(pc, cleaveRange + 0.6f, cleaveArc))
                    pc.Stats.TakeHit(new DamageInfo(stats.damage, transform.position, 9f));
                yield return new WaitForSeconds(0.2f);
            }
        }

        IEnumerator Slam(PlayerController pc)
        {
            float windup = slamWindup / SpeedMult;
            SimpleVfx.CircleTelegraph(transform.position, slamRadius, windup, TelegraphColor);
            yield return Windup(windup, null);
            AudioManager.Play(Sfx.Slam, 1f, 0f);
            CameraFollow.Shake(0.45f, 0.35f);
            SimpleVfx.Ring(transform.position, new Color(1f, 0.55f, 0.2f), 1f, slamRadius, 0.35f);
            if (PlayerInRange(pc, slamRadius, 360f))
                pc.Stats.TakeHit(new DamageInfo(Mathf.RoundToInt(stats.damage * slamDamageMultiplier), transform.position, 14f));
            yield return new WaitForSeconds(0.3f);
        }

        IEnumerator Charge(PlayerController pc)
        {
            Vector3 dir = pc.transform.position - transform.position;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(dir);
            float windup = chargeWindup / SpeedMult;
            SimpleVfx.LineTelegraph(transform.position, dir, chargeDistance, 2.6f, windup, TelegraphColor);
            yield return Windup(windup, null);

            AudioManager.Play(Sfx.Dash, 1f, 0f);
            float travelled = 0f;
            bool hit = false;
            while (travelled < chargeDistance)
            {
                float step = chargeSpeed * SpeedMult * Time.deltaTime;
                var flags = cc.Move(dir * step + Vector3.down * 2f * Time.deltaTime);
                travelled += step;
                if (!hit && PlayerInRange(pc, 2.2f, 360f))
                {
                    hit = true;
                    pc.Stats.TakeHit(new DamageInfo(Mathf.RoundToInt(stats.damage * chargeDamageMultiplier), transform.position, 16f));
                }
                if ((flags & CollisionFlags.Sides) != 0)
                {
                    CameraFollow.Shake(0.3f, 0.25f);
                    break;
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.45f);
        }

        IEnumerator Nova()
        {
            yield return Windup(novaWindup / SpeedMult, PlayerController.Instance);
            int waves = IsEnraged ? 2 : 1;
            for (int w = 0; w < waves; w++)
            {
                FireNova(w * (180f / novaCount));
                if (w < waves - 1) yield return new WaitForSeconds(0.35f);
            }
            yield return new WaitForSeconds(0.3f);
        }

        void FireNova(float angleOffset)
        {
            if (projectilePrefab == null) return;
            AudioManager.Play(Sfx.Whirlwind, 0.8f);
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            for (int i = 0; i < novaCount; i++)
            {
                float a = angleOffset + i * (360f / novaCount);
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                var p = Instantiate(projectilePrefab, origin + dir * 1.6f, Quaternion.LookRotation(dir));
                p.Init(dir, projectileSpeed * SpeedMult, Mathf.RoundToInt(stats.damage * 0.7f), gameObject);
            }
        }
    }
}

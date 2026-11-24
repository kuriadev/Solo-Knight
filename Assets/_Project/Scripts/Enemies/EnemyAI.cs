using UnityEngine;

namespace SoloKnight
{
    public enum EnemyAttackType { Melee, Ranged }

    /// <summary>
    /// Lightweight chase/attack AI using a CharacterController and short-range obstacle steering
    /// (no NavMesh package needed). Melee enemies wind up then strike in an arc; ranged enemies
    /// keep distance and fire projectiles.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(EnemyStats))]
    public class EnemyAI : MonoBehaviour
    {
        enum State { Idle, Chase, Windup, Recover }

        [Header("Behaviour")]
        public EnemyAttackType attackType = EnemyAttackType.Melee;
        public float moveSpeed = 3.4f;
        public float turnSharpness = 10f;
        public float aggroRange = 10f;
        public float leashRange = 26f;

        [Header("Attack")]
        public float attackRange = 1.9f;
        public float meleeHitRange = 2.4f;
        [Range(10f, 360f)] public float meleeArc = 110f;
        public float attackWindup = 0.45f;
        public float attackCooldown = 1.1f;
        public float playerKnockback = 6f;

        [Header("Ranged")]
        public Projectile projectilePrefab;
        public float preferredRange = 7f;
        public float projectileSpeed = 11f;

        [Header("Visual")]
        public Transform visual;

        [Header("Physics")]
        public float gravity = -30f;

        State state = State.Idle;
        float stateTimer;
        CharacterController cc;
        EnemyStats stats;
        Vector3 knockback;
        float verticalVelocity;
        float strafeSign = 1f;
        float strafeSwitchTimer;
        Vector3 visualBaseScale = Vector3.one;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            stats = GetComponent<EnemyStats>();
            if (visual != null) visualBaseScale = visual.localScale;
            strafeSign = Random.value < 0.5f ? -1f : 1f;
        }

        public void Alert()
        {
            if (state == State.Idle) state = State.Chase;
        }

        public void ApplyKnockback(Vector3 force)
        {
            knockback += force;
        }

        void Update()
        {
            if (stats.IsDead || !cc.enabled) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            var pc = PlayerController.Instance;
            bool hasTarget = pc != null && !pc.Stats.IsDead;
            Vector3 toPlayer = hasTarget ? pc.transform.position - transform.position : Vector3.zero;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            Vector3 dirToPlayer = dist > 0.001f ? toPlayer / dist : transform.forward;

            Vector3 desiredMove = Vector3.zero;
            Vector3 faceDir = Vector3.zero;

            if (!hasTarget && state != State.Idle) { state = State.Idle; stats.SetTelegraph(false); }

            switch (state)
            {
                case State.Idle:
                    if (hasTarget && dist <= aggroRange) state = State.Chase;
                    break;

                case State.Chase:
                    faceDir = dirToPlayer;
                    if (dist > leashRange) { state = State.Idle; break; }
                    if (attackType == EnemyAttackType.Melee)
                    {
                        if (dist <= attackRange) BeginWindup();
                        else desiredMove = dirToPlayer * moveSpeed;
                    }
                    else
                    {
                        strafeSwitchTimer -= dt;
                        if (strafeSwitchTimer <= 0f) { strafeSign = -strafeSign; strafeSwitchTimer = Random.Range(1.2f, 2.5f); }
                        if (dist > preferredRange) desiredMove = dirToPlayer * moveSpeed;
                        else if (dist < preferredRange * 0.55f) desiredMove = -dirToPlayer * moveSpeed;
                        else
                        {
                            desiredMove = Vector3.Cross(Vector3.up, dirToPlayer) * strafeSign * moveSpeed * 0.5f;
                            BeginWindup();
                        }
                    }
                    break;

                case State.Windup:
                    faceDir = dirToPlayer;
                    stateTimer -= dt;
                    if (visual != null)
                    {
                        float k = 1f - Mathf.Clamp01(stateTimer / attackWindup);
                        visual.localScale = visualBaseScale * (1f + 0.15f * k);
                    }
                    if (stateTimer <= 0f)
                    {
                        PerformAttack(pc, dist, dirToPlayer);
                        stats.SetTelegraph(false);
                        if (visual != null) visual.localScale = visualBaseScale;
                        state = State.Recover;
                        stateTimer = attackCooldown;
                    }
                    break;

                case State.Recover:
                    stateTimer -= dt;
                    faceDir = dirToPlayer;
                    if (attackType == EnemyAttackType.Melee && dist > attackRange) desiredMove = dirToPlayer * moveSpeed * 0.4f;
                    if (stateTimer <= 0f) state = State.Chase;
                    break;
            }

            if (desiredMove.sqrMagnitude > 0.001f) desiredMove = Steer(desiredMove);

            Vector3 velocity = desiredMove + knockback;
            knockback = Vector3.MoveTowards(knockback, Vector3.zero, 35f * dt);
            if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;
            velocity.y = verticalVelocity;
            cc.Move(velocity * dt);

            if (faceDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(faceDir), 1f - Mathf.Exp(-turnSharpness * dt));
        }

        void BeginWindup()
        {
            state = State.Windup;
            stateTimer = attackWindup;
            stats.SetTelegraph(true);
        }

        void PerformAttack(PlayerController pc, float dist, Vector3 dirToPlayer)
        {
            if (pc == null) return;
            if (attackType == EnemyAttackType.Melee)
            {
                AudioManager.Play(Sfx.Swing, 0.6f);
                if (dist <= meleeHitRange && Vector3.Angle(transform.forward, dirToPlayer) <= meleeArc * 0.5f)
                    pc.Stats.TakeHit(new DamageInfo(stats.damage, transform.position, playerKnockback));
            }
            else if (projectilePrefab != null)
            {
                Vector3 origin = transform.position + Vector3.up * 1.1f + transform.forward * 0.8f;
                Vector3 aim = (pc.transform.position + Vector3.up * 1f) - origin;
                aim.y = 0f;
                var p = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(aim));
                p.Init(aim.normalized, projectileSpeed, stats.damage, gameObject);
            }
        }

        /// <summary>Simple whisker steering: if the straight path is blocked by level geometry, try rotated directions.</summary>
        Vector3 Steer(Vector3 desired)
        {
            float speed = desired.magnitude;
            Vector3 dir = desired / speed;
            float[] angles = { 0f, 35f, -35f, 70f, -70f, 110f, -110f };
            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 d = Quaternion.Euler(0f, angles[i], 0f) * dir;
                if (!IsBlocked(d)) return d * speed;
            }
            return desired;
        }

        bool IsBlocked(Vector3 dir)
        {
            Vector3 origin = transform.position + Vector3.up * (cc.height * 0.5f);
            if (Physics.SphereCast(origin, cc.radius * 0.8f, dir, out RaycastHit hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                // Other characters are not obstacles to steer around (they push each other).
                return !(hit.collider is CharacterController);
            }
            return false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, aggroRange);
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}

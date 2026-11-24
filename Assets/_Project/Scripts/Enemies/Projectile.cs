using UnityEngine;

namespace SoloKnight
{
    /// <summary>Enemy/boss projectile. Swept sphere collision, passes through enemies, stops on walls.</summary>
    public class Projectile : MonoBehaviour
    {
        public float radius = 0.25f;
        public float lifetime = 4f;
        public float knockback = 4f;

        Vector3 direction;
        float speed;
        int damage;
        GameObject owner;
        float age;
        readonly RaycastHit[] hits = new RaycastHit[16];

        public void Init(Vector3 dir, float projectileSpeed, int projectileDamage, GameObject projectileOwner)
        {
            dir.y = 0f;
            direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
            speed = projectileSpeed;
            damage = projectileDamage;
            owner = projectileOwner;
            transform.rotation = Quaternion.LookRotation(direction);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age > lifetime) { Destroy(gameObject); return; }

            float step = speed * dt;
            int count = Physics.SphereCastNonAlloc(transform.position, radius, direction, hits, step, ~0, QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;
            Collider nearestCol = null;
            for (int i = 0; i < count; i++)
            {
                var col = hits[i].collider;
                if (owner != null && col.transform.IsChildOf(owner.transform)) continue;
                if (col.GetComponentInParent<EnemyStats>() != null) continue;
                if (hits[i].distance < nearest) { nearest = hits[i].distance; nearestCol = col; }
            }

            if (nearestCol != null)
            {
                var player = nearestCol.GetComponentInParent<PlayerStats>();
                if (player != null) player.TakeHit(new DamageInfo(damage, transform.position - direction, knockback));
                SimpleVfx.Burst(transform.position, new Color(0.7f, 0.4f, 1f), 5, 3f, 0.12f);
                Destroy(gameObject);
                return;
            }

            transform.position += direction * step;
        }
    }
}

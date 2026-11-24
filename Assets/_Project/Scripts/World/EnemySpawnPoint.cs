using UnityEngine;

namespace SoloKnight
{
    public enum EnemyKind { Melee, Brute, Caster, Random }

    /// <summary>Marks where (and how many) enemies spawn when the floor starts.</summary>
    public class EnemySpawnPoint : MonoBehaviour
    {
        public EnemyKind kind = EnemyKind.Random;
        [Min(0)] public int count = 1;
        public float radius = 1.5f;
        [Tooltip("If false, the floor's extra-enemies-per-spawn setting is ignored here (e.g. small alcoves).")]
        public bool allowExtraEnemies = true;

        void OnDrawGizmos()
        {
            Gizmos.color = kind == EnemyKind.Brute ? new Color(1f, 0.5f, 0f) : kind == EnemyKind.Caster ? new Color(0.6f, 0.3f, 1f) : Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, radius);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);
        }
    }
}

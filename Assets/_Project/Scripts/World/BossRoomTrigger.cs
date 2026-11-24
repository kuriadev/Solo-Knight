using UnityEngine;

namespace SoloKnight
{
    /// <summary>
    /// Volume just inside the boss room. When the player is fully inside, the entrance seals
    /// and the boss fight begins. Uses a bounds check (robust with CharacterController).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class BossRoomTrigger : MonoBehaviour
    {
        BoxCollider volume;
        bool fired;

        void Awake()
        {
            volume = GetComponent<BoxCollider>();
            volume.isTrigger = true;
        }

        void Update()
        {
            if (fired) return;
            var pc = PlayerController.Instance;
            if (pc == null || pc.Stats.IsDead) return;
            if (volume.bounds.Contains(pc.transform.position + Vector3.up * 0.5f))
            {
                fired = true;
                if (FloorManager.Instance != null) FloorManager.Instance.StartBossFight();
            }
        }

        void OnDrawGizmos()
        {
            var b = GetComponent<BoxCollider>();
            if (b == null) return;
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(b.center, b.size);
        }
    }
}

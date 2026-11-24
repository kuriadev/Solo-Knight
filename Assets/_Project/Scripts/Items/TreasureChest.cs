using System.Collections;
using UnityEngine;

namespace SoloKnight
{
    /// <summary>Interactable chest: press F nearby to burst it open and spawn guaranteed loot.</summary>
    [RequireComponent(typeof(LootDropper))]
    public class TreasureChest : MonoBehaviour, IInteractable
    {
        public Transform lidPivot;
        public float openAngle = -110f;

        LootDropper loot;
        bool opened;

        public bool IsOpened => opened;
        public string InteractPrompt => "[F] Open Chest";
        public bool CanInteract => !opened;
        public Vector3 InteractPosition => transform.position;

        void Awake()
        {
            loot = GetComponent<LootDropper>();
        }

        void OnEnable() { InteractableRegistry.Register(this); }
        void OnDisable() { InteractableRegistry.Unregister(this); }

        public void Interact(PlayerController player)
        {
            if (opened) return;
            opened = true;
            StartCoroutine(OpenRoutine());
        }

        IEnumerator OpenRoutine()
        {
            AudioManager.Play(Sfx.Chest);
            Quaternion from = lidPivot != null ? lidPivot.localRotation : Quaternion.identity;
            Quaternion to = Quaternion.Euler(openAngle, 0f, 0f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.3f;
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                if (lidPivot != null) lidPivot.localRotation = Quaternion.SlerpUnclamped(from, to, e);
                yield return null;
            }

            Vector3 burstPos = transform.position + Vector3.up * 0.8f;
            SimpleVfx.Burst(burstPos, new Color(1f, 0.85f, 0.3f), 14, 7f);
            CameraFollow.Shake(0.15f, 0.15f);
            loot.DropLoot(burstPos);
            if (MissionManager.Instance != null) MissionManager.Instance.ReportChestOpened();
        }
    }
}

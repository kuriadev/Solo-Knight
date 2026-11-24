using UnityEngine;

namespace SoloKnight
{
    /// <summary>Exit portal that appears after the boss dies. Press F to descend to the next floor.</summary>
    public class FloorPortal : MonoBehaviour, IInteractable
    {
        public GameObject visuals;
        public Transform spinner;
        public Light portalLight;

        bool active;

        public bool IsActive => active;
        public string InteractPrompt => "[F] Enter Portal to the next floor";
        public bool CanInteract => active;
        public Vector3 InteractPosition => transform.position;

        void Awake()
        {
            SetVisible(false);
        }

        void OnEnable() { InteractableRegistry.Register(this); }
        void OnDisable() { InteractableRegistry.Unregister(this); }

        public void Activate()
        {
            active = true;
            SetVisible(true);
            AudioManager.Play(Sfx.Portal, 1f, 0f);
            SimpleVfx.Ring(transform.position, new Color(0.6f, 0.4f, 1f), 0.5f, 4f, 0.8f);
        }

        void SetVisible(bool v)
        {
            if (visuals != null) visuals.SetActive(v);
            if (portalLight != null) portalLight.enabled = v;
        }

        void Update()
        {
            if (!active) return;
            if (spinner != null) spinner.Rotate(0f, 90f * Time.deltaTime, 0f, Space.Self);
            if (portalLight != null) portalLight.intensity = 2.5f + Mathf.Sin(Time.time * 3f) * 0.6f;
        }

        public void Interact(PlayerController player)
        {
            if (!active) return;
            active = false;
            if (GameManager.Instance != null) GameManager.Instance.AdvanceFloor();
        }
    }
}

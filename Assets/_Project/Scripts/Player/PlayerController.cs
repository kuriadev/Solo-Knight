using UnityEngine;

namespace SoloKnight
{
    /// <summary>
    /// WASD/arrow 8-directional movement, mouse aiming, Space dash with i-frames,
    /// F to interact and 1/2 to drink potions.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerStats), typeof(PlayerCombat))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Movement")]
        public float acceleration = 70f;
        public float rotationSharpness = 25f;
        public float gravity = -30f;

        [Header("Dash")]
        public float dashSpeed = 22f;
        public float dashDuration = 0.18f;
        public float dashCooldown = 1.0f;
        public float dashExtraInvulnerability = 0.1f;

        [Header("Interaction")]
        public float interactRange = 2.8f;

        public PlayerStats Stats { get; private set; }
        public PlayerCombat Combat { get; private set; }
        public InventorySystem Inventory { get; private set; }
        public Vector3 AimDirection { get; private set; } = Vector3.forward;
        public bool IsDashing { get; private set; }
        public float DashCooldownRemaining => Mathf.Max(0f, dashCooldownTimer);
        public float DashCooldownNormalized => dashCooldown <= 0f ? 0f : Mathf.Clamp01(dashCooldownTimer / dashCooldown);

        CharacterController cc;
        Camera cam;
        Vector3 planarVelocity;
        Vector3 knockback;
        float verticalVelocity;
        float dashTimer;
        float dashCooldownTimer;
        Vector3 dashDirection;
        IInteractable currentInteractable;

        void Awake()
        {
            Instance = this;
            cc = GetComponent<CharacterController>();
            Stats = GetComponent<PlayerStats>();
            Combat = GetComponent<PlayerCombat>();
            Inventory = GetComponent<InventorySystem>();
            AimDirection = transform.forward;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            cam = Camera.main;
            if (GameManager.Instance != null) GameManager.Instance.RegisterPlayer(Stats);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (Stats.IsDead)
            {
                ApplyGravityOnly(dt);
                return;
            }

            bool blocked = GameManager.Instance != null && GameManager.Instance.IsGameplayBlocked;
            Vector3 input = Vector3.zero;
            if (!blocked)
            {
                input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
                if (input.sqrMagnitude > 1f) input.Normalize();
                UpdateAim();
            }

            dashCooldownTimer -= dt;
            if (!blocked && Input.GetKeyDown(KeyCode.Space) && dashCooldownTimer <= 0f && !IsDashing)
                StartDash(input);

            Vector3 horizontal;
            if (IsDashing)
            {
                horizontal = dashDirection * dashSpeed;
                dashTimer -= dt;
                if (dashTimer <= 0f)
                {
                    IsDashing = false;
                    planarVelocity = dashDirection * Stats.MoveSpeed;
                }
            }
            else
            {
                Vector3 target = input * Stats.MoveSpeed * Combat.MoveSpeedMultiplier;
                planarVelocity = Vector3.MoveTowards(planarVelocity, target, acceleration * dt);
                horizontal = planarVelocity;
            }

            horizontal += knockback;
            knockback = Vector3.MoveTowards(knockback, Vector3.zero, 30f * dt);

            if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;
            cc.Move((horizontal + Vector3.up * verticalVelocity) * dt);

            Vector3 face = IsDashing ? dashDirection : AimDirection;
            if (face.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(face);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-rotationSharpness * dt));
            }

            if (!blocked)
            {
                HandleInteraction();
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Inventory.UsePotion(0);
                if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Inventory.UsePotion(1);
            }
            else if (currentInteractable != null)
            {
                currentInteractable = null;
                if (UIManager.Instance != null) UIManager.Instance.SetInteractPrompt(null);
            }
        }

        void ApplyGravityOnly(float dt)
        {
            if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;
            cc.Move(Vector3.up * verticalVelocity * dt);
        }

        void UpdateAim()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            var ground = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (ground.Raycast(ray, out float enter))
            {
                Vector3 dir = ray.GetPoint(enter) - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.04f) AimDirection = dir.normalized;
            }
        }

        /// <summary>Instantly face the mouse (used when an attack starts).</summary>
        public void SnapToAim()
        {
            if (AimDirection.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(AimDirection);
        }

        void StartDash(Vector3 input)
        {
            dashDirection = input.sqrMagnitude > 0.01f ? input.normalized : AimDirection;
            IsDashing = true;
            dashTimer = dashDuration;
            dashCooldownTimer = dashCooldown;
            knockback = Vector3.zero;
            Stats.SetInvulnerable(dashDuration + dashExtraInvulnerability);
            Combat.CancelAttack();
            AudioManager.Play(Sfx.Dash);
            SimpleVfx.Ring(transform.position, new Color(0.6f, 0.85f, 1f), 0.3f, 1.4f, 0.25f);
        }

        void HandleInteraction()
        {
            var nearest = InteractableRegistry.FindNearest(transform.position, interactRange);
            if (nearest != currentInteractable)
            {
                currentInteractable = nearest;
                if (UIManager.Instance != null) UIManager.Instance.SetInteractPrompt(nearest != null ? nearest.InteractPrompt : null);
            }
            if (currentInteractable != null && Input.GetKeyDown(KeyCode.F))
            {
                var target = currentInteractable;
                currentInteractable = null;
                if (UIManager.Instance != null) UIManager.Instance.SetInteractPrompt(null);
                target.Interact(this);
            }
        }

        public void ApplyKnockback(Vector3 force)
        {
            if (IsDashing) return;
            force.y = 0f;
            knockback += force;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            cc.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            cc.enabled = true;
            planarVelocity = Vector3.zero;
            knockback = Vector3.zero;
            AimDirection = rotation * Vector3.forward;
            if (CameraFollow.Instance != null) CameraFollow.Instance.SnapToTarget();
        }
    }
}

using UnityEngine;

namespace SoloKnight
{
    /// <summary>Angled top-down follow camera with screen shake.</summary>
    public class CameraFollow : MonoBehaviour
    {
        public static CameraFollow Instance { get; private set; }

        public Transform target;
        public Vector3 offset = new Vector3(0f, 16f, -11f);
        public float followSharpness = 10f;
        public float lookAtHeight = 0.5f;

        float shakeTimer;
        float shakeDuration;
        float shakeMagnitude;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            if (target == null && PlayerController.Instance != null) target = PlayerController.Instance.transform;
            if (target != null) SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = target.position + offset;
            transform.rotation = Quaternion.LookRotation((target.position + Vector3.up * lookAtHeight) - transform.position);
        }

        void LateUpdate()
        {
            if (target == null)
            {
                if (PlayerController.Instance == null) return;
                target = PlayerController.Instance.transform;
                SnapToTarget();
            }

            Vector3 desired = target.position + offset;
            float k = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
            Vector3 pos = Vector3.Lerp(transform.position - CurrentShakeOffset(false), desired, k);
            transform.rotation = Quaternion.LookRotation(-offset + Vector3.up * lookAtHeight);
            transform.position = pos + CurrentShakeOffset(true);
        }

        Vector3 lastShake;

        Vector3 CurrentShakeOffset(bool advance)
        {
            if (!advance) return lastShake;
            if (shakeTimer > 0f)
            {
                shakeTimer -= Time.unscaledDeltaTime;
                float strength = shakeMagnitude * Mathf.Clamp01(shakeTimer / Mathf.Max(0.0001f, shakeDuration));
                lastShake = Random.insideUnitSphere * strength;
            }
            else lastShake = Vector3.zero;
            return lastShake;
        }

        public static void Shake(float magnitude, float duration)
        {
            if (Instance == null) return;
            if (magnitude >= Instance.shakeMagnitude * Mathf.Clamp01(Instance.shakeTimer / Mathf.Max(0.0001f, Instance.shakeDuration)))
            {
                Instance.shakeMagnitude = magnitude;
                Instance.shakeDuration = duration;
                Instance.shakeTimer = duration;
            }
        }
    }
}

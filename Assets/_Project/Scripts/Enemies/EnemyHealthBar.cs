using UnityEngine;

namespace SoloKnight
{
    /// <summary>Small billboarded HP bar above regular enemies, shown once they take damage.</summary>
    [RequireComponent(typeof(EnemyStats))]
    public class EnemyHealthBar : MonoBehaviour
    {
        public float height = 2.4f;
        public float width = 1.2f;

        static Material bgMaterial;
        static Material fillMaterial;

        EnemyStats stats;
        Transform root;
        Transform fill;

        void Awake()
        {
            stats = GetComponent<EnemyStats>();
        }

        void Start()
        {
            if (bgMaterial == null) bgMaterial = MaterialUtil.CreateUnlit(new Color(0.1f, 0.05f, 0.05f));
            if (fillMaterial == null) fillMaterial = MaterialUtil.CreateUnlit(new Color(0.9f, 0.2f, 0.2f));

            root = new GameObject("HealthBar").transform;
            root.SetParent(transform, false);
            root.localPosition = Vector3.up * height;
            MaterialUtil.Visual(PrimitiveType.Quad, root, Vector3.zero, new Vector3(width + 0.08f, 0.2f, 1f), bgMaterial, "BG");
            fill = MaterialUtil.Visual(PrimitiveType.Quad, root, new Vector3(0f, 0f, -0.01f), new Vector3(width, 0.13f, 1f), fillMaterial, "Fill").transform;
            root.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (root == null) return;
            bool show = !stats.IsDead && stats.CurrentHP < stats.maxHP;
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show) return;

            var cam = Camera.main;
            if (cam != null) root.rotation = cam.transform.rotation;
            // Keep a constant world size regardless of the enemy's own scale.
            Vector3 ls = transform.lossyScale;
            root.localScale = new Vector3(1f / Mathf.Max(0.01f, ls.x), 1f / Mathf.Max(0.01f, ls.y), 1f / Mathf.Max(0.01f, ls.z));
            root.localPosition = Vector3.up * (height / Mathf.Max(0.01f, ls.y));

            float pct = Mathf.Clamp01(stats.CurrentHP / (float)Mathf.Max(1, stats.maxHP));
            fill.localScale = new Vector3(width * pct, 0.13f, 1f);
            fill.localPosition = new Vector3(-(width * (1f - pct)) * 0.5f, 0f, -0.01f);
        }
    }
}

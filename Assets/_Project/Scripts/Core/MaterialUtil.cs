using UnityEngine;

namespace SoloKnight
{
    /// <summary>Small helpers for runtime materials and primitive visuals (works in Built-in and URP).</summary>
    public static class MaterialUtil
    {
        static Material unlitTemplate;

        public static void SetUnlitTemplate(Material template)
        {
            if (template != null) unlitTemplate = template;
        }

        public static Material CreateUnlit(Color color)
        {
            Material m;
            if (unlitTemplate != null)
            {
                m = new Material(unlitTemplate);
            }
            else
            {
                Shader s = Shader.Find("Universal Render Pipeline/Unlit");
                if (s == null) s = Shader.Find("Unlit/Color");
                if (s == null) s = Shader.Find("Sprites/Default");
                m = new Material(s);
            }
            SetColor(m, color);
            return m;
        }

        public static Material CreateTinted(Material source, Color color)
        {
            if (source == null) return CreateUnlit(color);
            var m = new Material(source);
            SetColor(m, color);
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static Color GetColor(Material m)
        {
            if (m == null) return Color.white;
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("_Color")) return m.GetColor("_Color");
            return Color.white;
        }

        /// <summary>Creates a collider-less primitive used purely as a visual.</summary>
        public static GameObject Visual(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (!string.IsNullOrEmpty(name)) go.name = name;
            var col = go.GetComponent<Collider>();
            // Removed immediately (also at runtime) so VFX never collide with characters for a frame.
            if (col != null) Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }
    }
}

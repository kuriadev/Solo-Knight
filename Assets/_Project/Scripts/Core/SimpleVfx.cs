using System;
using UnityEngine;

namespace SoloKnight
{
    /// <summary>Drives a short-lived effect: calls tick(t01) every frame, then destroys itself.</summary>
    public class VfxAnimator : MonoBehaviour
    {
        public float lifetime = 0.5f;
        public bool unscaledTime;
        public Action<float> tick;
        float t;

        void Update()
        {
            t += (unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) / Mathf.Max(0.0001f, lifetime);
            tick?.Invoke(Mathf.Clamp01(t));
            if (t >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>Code-only visual effects built from primitives (no imported assets required).</summary>
    public static class SimpleVfx
    {
        /// <summary>Expanding flat ring on the ground (level-ups, slams, whirlwinds).</summary>
        public static void Ring(Vector3 position, Color color, float startRadius, float endRadius, float duration)
        {
            var root = new GameObject("VFX_Ring");
            root.transform.position = position + Vector3.up * 0.05f;
            var mat = MaterialUtil.CreateUnlit(color);
            var disc = MaterialUtil.Visual(PrimitiveType.Cylinder, root.transform, Vector3.zero, Vector3.one, mat);
            var anim = root.AddComponent<VfxAnimator>();
            anim.lifetime = duration;
            anim.tick = t =>
            {
                float r = Mathf.Lerp(startRadius, endRadius, 1f - (1f - t) * (1f - t));
                float h = Mathf.Lerp(0.06f, 0.005f, t);
                disc.transform.localScale = new Vector3(r * 2f, h, r * 2f);
            };
        }

        /// <summary>Small cubes bursting outward (deaths, chests).</summary>
        public static void Burst(Vector3 position, Color color, int count = 10, float speed = 6f, float size = 0.22f)
        {
            var root = new GameObject("VFX_Burst");
            root.transform.position = position;
            var mat = MaterialUtil.CreateUnlit(color);
            var parts = new Transform[count];
            var vels = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                parts[i] = MaterialUtil.Visual(PrimitiveType.Cube, root.transform, Vector3.zero, Vector3.one * size, mat).transform;
                Vector3 dir = UnityEngine.Random.insideUnitSphere;
                dir.y = Mathf.Abs(dir.y) + 0.6f;
                vels[i] = dir.normalized * speed * UnityEngine.Random.Range(0.6f, 1.2f);
                parts[i].rotation = UnityEngine.Random.rotation;
            }
            var anim = root.AddComponent<VfxAnimator>();
            anim.lifetime = 0.7f;
            anim.tick = t =>
            {
                float dt = Time.deltaTime;
                for (int i = 0; i < count; i++)
                {
                    vels[i] += Vector3.down * 18f * dt;
                    parts[i].localPosition += vels[i] * dt;
                    if (parts[i].position.y < 0.1f)
                    {
                        Vector3 p = parts[i].position; p.y = 0.1f; parts[i].position = p;
                        vels[i] = new Vector3(vels[i].x * 0.5f, 0f, vels[i].z * 0.5f);
                    }
                    parts[i].localScale = Vector3.one * size * (1f - t);
                }
            };
        }

        /// <summary>Ground circle warning that fills up until the attack lands.</summary>
        public static GameObject CircleTelegraph(Vector3 center, float radius, float duration, Color color)
        {
            var root = new GameObject("Telegraph_Circle");
            root.transform.position = new Vector3(center.x, 0.03f, center.z);
            var outer = MaterialUtil.Visual(PrimitiveType.Cylinder, root.transform, Vector3.zero, new Vector3(radius * 2f, 0.01f, radius * 2f), MaterialUtil.CreateUnlit(color * 0.45f));
            var inner = MaterialUtil.Visual(PrimitiveType.Cylinder, root.transform, Vector3.up * 0.01f, Vector3.zero, MaterialUtil.CreateUnlit(color));
            var anim = root.AddComponent<VfxAnimator>();
            anim.lifetime = duration;
            anim.tick = t =>
            {
                float r = radius * 2f * t;
                inner.transform.localScale = new Vector3(r, 0.012f, r);
            };
            return root;
        }

        /// <summary>Rectangular warning lane (boss charge).</summary>
        public static GameObject LineTelegraph(Vector3 start, Vector3 direction, float length, float width, float duration, Color color)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            var root = new GameObject("Telegraph_Line");
            root.transform.position = new Vector3(start.x, 0.03f, start.z);
            root.transform.rotation = Quaternion.LookRotation(direction.normalized);
            MaterialUtil.Visual(PrimitiveType.Cube, root.transform, new Vector3(0f, 0f, length * 0.5f), new Vector3(width, 0.01f, length), MaterialUtil.CreateUnlit(color * 0.45f));
            var inner = MaterialUtil.Visual(PrimitiveType.Cube, root.transform, Vector3.up * 0.01f, Vector3.zero, MaterialUtil.CreateUnlit(color));
            var anim = root.AddComponent<VfxAnimator>();
            anim.lifetime = duration;
            anim.tick = t =>
            {
                inner.transform.localScale = new Vector3(width, 0.012f, length * t);
                inner.transform.localPosition = new Vector3(0f, 0.01f, length * t * 0.5f);
            };
            return root;
        }
    }
}

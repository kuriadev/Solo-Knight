using UnityEngine;

namespace SoloKnight
{
    /// <summary>
    /// Physical loot lying in the dungeon: gold, potions or equipment.
    /// Pops out with an arc, bobs in place and is magnetised to the player when close.
    /// </summary>
    public class WorldPickup : MonoBehaviour
    {
        [Header("Visuals")]
        public Renderer orbVisual;
        public Renderer gearVisual;

        [Header("Pickup")]
        public float magnetRadius = 3f;
        public float collectRadius = 0.9f;
        public float magnetSpeed = 14f;
        public float pickupDelay = 0.45f;

        public ItemSO Item { get; private set; }
        public int GoldAmount { get; private set; }

        Vector3 launchFrom, launchTo;
        float launchT = 1f;
        float age;
        bool magnetised;
        float fullWarnCooldown;
        Transform visualRoot;

        /// <summary>Spawns a configured pickup that pops out from origin to a random nearby spot.</summary>
        public static WorldPickup Spawn(WorldPickup prefab, Vector3 origin, ItemSO item, int gold, float scatter = 1.6f)
        {
            if (prefab == null) return null;
            var p = Instantiate(prefab, origin, Quaternion.identity);
            p.Configure(item, gold);
            Vector2 c = Random.insideUnitCircle * scatter;
            p.Launch(origin, new Vector3(origin.x + c.x, 0f, origin.z + c.y));
            return p;
        }

        public void Configure(ItemSO item, int gold)
        {
            Item = item;
            GoldAmount = gold;
            bool isGear = item != null && item.IsEquipment;
            if (orbVisual != null) orbVisual.gameObject.SetActive(!isGear);
            if (gearVisual != null) gearVisual.gameObject.SetActive(isGear);

            Renderer active = isGear ? gearVisual : orbVisual;
            visualRoot = active != null ? active.transform : transform;
            if (active != null)
            {
                Color c = item != null ? item.DisplayColor : new Color(1f, 0.82f, 0.2f);
                active.sharedMaterial = MaterialUtil.CreateTinted(active.sharedMaterial, c);
                if (item == null)
                {
                    float s = Mathf.Lerp(0.35f, 0.6f, Mathf.Clamp01(gold / 60f));
                    active.transform.localScale = Vector3.one * s;
                }
            }
            name = item != null ? $"Pickup_{item.displayName}" : $"Pickup_Gold_{gold}";
        }

        void Launch(Vector3 from, Vector3 to)
        {
            launchFrom = from;
            launchTo = to;
            launchT = 0f;
            transform.position = from;
        }

        void Update()
        {
            age += Time.deltaTime;
            fullWarnCooldown -= Time.deltaTime;

            if (launchT < 1f)
            {
                launchT = Mathf.Min(1f, launchT + Time.deltaTime / 0.4f);
                Vector3 p = Vector3.Lerp(launchFrom, launchTo, launchT);
                p.y = Mathf.Lerp(launchFrom.y, 0f, launchT) + Mathf.Sin(launchT * Mathf.PI) * 1.4f;
                transform.position = p;
            }

            if (visualRoot != null && visualRoot != transform)
            {
                visualRoot.localPosition = new Vector3(0f, 0.45f + Mathf.Sin(age * 3f) * 0.12f, 0f);
                visualRoot.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
            }

            if (age < pickupDelay) return;
            var pc = PlayerController.Instance;
            if (pc == null || pc.Stats == null || pc.Stats.IsDead) return;

            Vector3 to = pc.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            if (!magnetised && dist < magnetRadius && CanBeCollected(pc)) magnetised = true;
            if (magnetised)
            {
                launchT = 1f;
                transform.position += to.normalized * Mathf.Min(dist, magnetSpeed * Time.deltaTime);
                if (dist <= collectRadius) Collect(pc);
            }
        }

        bool CanBeCollected(PlayerController pc)
        {
            if (Item == null || !Item.IsEquipment) return true;
            if (pc.Inventory.HasSpaceFor(Item)) return true;
            if (fullWarnCooldown <= 0f)
            {
                UIManager.Toast("Inventory full! Salvage items in the Character panel (Tab).", new Color(1f, 0.5f, 0.4f));
                fullWarnCooldown = 4f;
            }
            return false;
        }

        void Collect(PlayerController pc)
        {
            if (Item == null)
            {
                pc.Inventory.AddGold(GoldAmount);
                AudioManager.Play(Sfx.Coin, 0.8f);
                UIManager.FloatingText(transform.position + Vector3.up, $"+{GoldAmount}g", new Color(1f, 0.85f, 0.25f), 26);
                Destroy(gameObject);
                return;
            }

            if (pc.Inventory.TryAddItem(Item))
            {
                AudioManager.Play(Sfx.Pickup);
                UIManager.Toast($"Picked up {Item.RichName}", Color.white);
                Destroy(gameObject);
            }
            else
            {
                magnetised = false;
            }
        }
    }
}

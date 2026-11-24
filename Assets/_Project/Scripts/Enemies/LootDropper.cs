using UnityEngine;

namespace SoloKnight
{
    /// <summary>Rolls and spawns physical loot (gold, potions, tiered equipment). Used by enemies, bosses and chests.</summary>
    public class LootDropper : MonoBehaviour
    {
        public WorldPickup pickupPrefab;

        [Header("Gold")]
        [Range(0f, 1f)] public float goldChance = 0.85f;
        public Vector2Int goldRange = new Vector2Int(3, 8);
        public int goldPiles = 1;

        [Header("Potions")]
        [Range(0f, 1f)] public float potionChance = 0.12f;
        [Range(0f, 1f)] public float largePotionShare = 0.2f;

        [Header("Equipment")]
        [Range(0f, 1f)] public float equipmentChance = 0.1f;
        public int guaranteedEquipment = 0;
        public float commonWeight = 70f;
        public float rareWeight = 25f;
        public float epicWeight = 5f;

        [Header("Floor scaling (set at runtime by FloorManager)")]
        public float goldMultiplier = 1f;
        public float rarityBonus = 0f;

        public void DropLoot() => DropLoot(transform.position + Vector3.up * 0.5f);

        public void DropLoot(Vector3 origin)
        {
            if (pickupPrefab == null)
            {
                Debug.LogWarning($"[SoloKnight] {name} has no pickup prefab assigned on its LootDropper.");
                return;
            }

            var db = GameManager.Instance != null ? GameManager.Instance.itemDatabase : null;

            for (int i = 0; i < Mathf.Max(1, goldPiles); i++)
            {
                if (Random.value <= goldChance)
                {
                    int g = Mathf.Max(1, Mathf.RoundToInt(Random.Range(goldRange.x, goldRange.y + 1) * goldMultiplier));
                    WorldPickup.Spawn(pickupPrefab, origin, null, g);
                }
            }

            if (db == null) return;

            if (Random.value <= potionChance)
            {
                var potion = Random.value < largePotionShare ? db.largePotion : db.smallPotion;
                if (potion != null) WorldPickup.Spawn(pickupPrefab, origin, potion, 0);
            }

            int equipmentDrops = guaranteedEquipment + (Random.value <= equipmentChance ? 1 : 0);
            for (int i = 0; i < equipmentDrops; i++)
            {
                var rarity = ItemDatabaseSO.RollRarity(commonWeight, rareWeight, epicWeight, rarityBonus);
                var item = db.GetRandomEquipment(rarity);
                if (item != null) WorldPickup.Spawn(pickupPrefab, origin, item, 0, 2f);
            }
        }
    }
}

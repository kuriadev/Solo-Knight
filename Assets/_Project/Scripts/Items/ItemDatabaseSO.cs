using System.Collections.Generic;
using UnityEngine;

namespace SoloKnight
{
    /// <summary>Registry of all items, used for loot rolls and to resolve saved item ids.</summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "Solo Knight/Item Database")]
    public class ItemDatabaseSO : ScriptableObject
    {
        public List<ItemSO> items = new List<ItemSO>();
        public ItemSO smallPotion;
        public ItemSO largePotion;

        Dictionary<string, ItemSO> lookup;

        public ItemSO GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (lookup == null || lookup.Count != items.Count)
            {
                lookup = new Dictionary<string, ItemSO>();
                foreach (var it in items)
                    if (it != null && !string.IsNullOrEmpty(it.itemId)) lookup[it.itemId] = it;
            }
            lookup.TryGetValue(id, out var item);
            return item;
        }

        public ItemSO GetRandomEquipment(ItemRarity rarity)
        {
            var pool = new List<ItemSO>();
            foreach (var it in items)
                if (it != null && it.IsEquipment && it.rarity == rarity) pool.Add(it);
            if (pool.Count == 0)
                foreach (var it in items)
                    if (it != null && it.IsEquipment) pool.Add(it);
            return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
        }

        /// <summary>Weighted rarity roll. rarityBonus (0..1+) shifts weight from Common to Rare/Epic.</summary>
        public static ItemRarity RollRarity(float common, float rare, float epic, float rarityBonus)
        {
            float shift = Mathf.Clamp(rarityBonus, 0f, 2f);
            float c = Mathf.Max(5f, common * (1f - 0.5f * shift));
            float r = rare * (1f + 0.6f * shift);
            float e = epic * (1f + 1.5f * shift);
            float roll = Random.Range(0f, c + r + e);
            if (roll < e) return ItemRarity.Epic;
            if (roll < e + r) return ItemRarity.Rare;
            return ItemRarity.Common;
        }
    }
}

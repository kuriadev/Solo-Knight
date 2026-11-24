using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoloKnight
{
    /// <summary>Gold, potion stacks, a bag of equipment and the two equipment slots (weapon + armor).</summary>
    public class InventorySystem : MonoBehaviour
    {
        public int capacity = 20;
        [Tooltip("Potion used by key 1.")] public ItemSO potionSlot1;
        [Tooltip("Potion used by key 2.")] public ItemSO potionSlot2;

        readonly List<ItemSO> items = new List<ItemSO>();
        readonly int[] potionCounts = new int[2];
        ItemSO equippedWeapon;
        ItemSO equippedArmor;
        int gold;

        public event Action OnInventoryChanged;
        public event Action OnEquipmentChanged;

        public IReadOnlyList<ItemSO> Items => items;
        public ItemSO EquippedWeapon => equippedWeapon;
        public ItemSO EquippedArmor => equippedArmor;
        public int Gold => gold;
        public bool IsFull => items.Count >= capacity;

        public int BonusAttack => Sum(i => i.bonusAttack);
        public int BonusDefense => Sum(i => i.bonusDefense);
        public int BonusMaxHP => Sum(i => i.bonusMaxHP);
        public float BonusCritChance => (equippedWeapon ? equippedWeapon.bonusCritChance : 0f) + (equippedArmor ? equippedArmor.bonusCritChance : 0f);
        public float BonusMoveSpeed => (equippedWeapon ? equippedWeapon.bonusMoveSpeed : 0f) + (equippedArmor ? equippedArmor.bonusMoveSpeed : 0f);

        int Sum(Func<ItemSO, int> f) => (equippedWeapon ? f(equippedWeapon) : 0) + (equippedArmor ? f(equippedArmor) : 0);

        PlayerStats stats;

        void Awake()
        {
            stats = GetComponent<PlayerStats>();
        }

        public int GetPotionCount(int slot) => slot >= 0 && slot < 2 ? potionCounts[slot] : 0;
        public ItemSO GetPotionItem(int slot) => slot == 0 ? potionSlot1 : potionSlot2;

        public bool HasSpaceFor(ItemSO item)
        {
            if (item == null) return false;
            if (!item.IsEquipment) return true;
            if (item.itemType == ItemType.Weapon && equippedWeapon == null) return true;
            if (item.itemType == ItemType.Armor && equippedArmor == null) return true;
            return !IsFull;
        }

        public bool TryAddItem(ItemSO item)
        {
            if (item == null) return false;

            if (item.itemType == ItemType.Potion)
            {
                int slot = item == potionSlot2 ? 1 : 0;
                potionCounts[slot]++;
                OnInventoryChanged?.Invoke();
                return true;
            }

            // Auto-equip into an empty slot
            if (item.itemType == ItemType.Weapon && equippedWeapon == null) { equippedWeapon = item; NotifyEquipment(); return true; }
            if (item.itemType == ItemType.Armor && equippedArmor == null) { equippedArmor = item; NotifyEquipment(); return true; }

            if (IsFull) return false;
            items.Add(item);
            OnInventoryChanged?.Invoke();
            return true;
        }

        public void AddGold(int amount, bool countsForMission = true)
        {
            if (amount <= 0) return;
            gold += amount;
            OnInventoryChanged?.Invoke();
            if (countsForMission && MissionManager.Instance != null) MissionManager.Instance.ReportGold(amount);
        }

        public void EquipFromInventory(int index)
        {
            if (index < 0 || index >= items.Count) return;
            var item = items[index];
            if (!item.IsEquipment) return;
            items.RemoveAt(index);
            ItemSO previous;
            if (item.itemType == ItemType.Weapon) { previous = equippedWeapon; equippedWeapon = item; }
            else { previous = equippedArmor; equippedArmor = item; }
            if (previous != null) items.Insert(Mathf.Min(index, items.Count), previous);
            AudioManager.Play(Sfx.Pickup, 0.7f);
            NotifyEquipment();
        }

        public bool Unequip(ItemType slot)
        {
            var item = slot == ItemType.Weapon ? equippedWeapon : slot == ItemType.Armor ? equippedArmor : null;
            if (item == null) return false;
            if (IsFull)
            {
                UIManager.Toast("Inventory full - cannot unequip.", new Color(1f, 0.5f, 0.4f));
                return false;
            }
            if (slot == ItemType.Weapon) equippedWeapon = null; else equippedArmor = null;
            items.Add(item);
            NotifyEquipment();
            return true;
        }

        public int Salvage(int index)
        {
            if (index < 0 || index >= items.Count) return 0;
            var item = items[index];
            items.RemoveAt(index);
            int value = Mathf.Max(1, item.sellValue);
            AddGold(value, false);
            AudioManager.Play(Sfx.Coin);
            return value;
        }

        public bool UsePotion(int slot)
        {
            if (stats == null || stats.IsDead) return false;
            var potion = GetPotionItem(slot);
            if (potion == null) return false;
            if (potionCounts[slot] <= 0)
            {
                UIManager.Toast($"No {potion.displayName}s left.", new Color(1f, 0.6f, 0.5f));
                return false;
            }
            if (stats.CurrentHP >= stats.MaxHP)
            {
                UIManager.Toast("Already at full health.", new Color(0.8f, 0.8f, 0.8f));
                return false;
            }
            potionCounts[slot]--;
            int heal = Mathf.RoundToInt(stats.MaxHP * potion.healPercent) + potion.healFlat;
            stats.Heal(heal);
            AudioManager.Play(Sfx.Potion);
            SimpleVfx.Ring(transform.position, new Color(0.3f, 1f, 0.45f), 0.4f, 1.8f, 0.4f);
            OnInventoryChanged?.Invoke();
            return true;
        }

        void NotifyEquipment()
        {
            OnEquipmentChanged?.Invoke();
            OnInventoryChanged?.Invoke();
        }

        // ---------------- Save / Load ----------------

        public void ImportFrom(SaveData d, ItemDatabaseSO db)
        {
            items.Clear();
            gold = d.gold;
            potionCounts[0] = d.potionCounts[0];
            potionCounts[1] = d.potionCounts[1];
            equippedWeapon = db != null ? db.GetById(d.equippedWeaponId) : null;
            equippedArmor = db != null ? db.GetById(d.equippedArmorId) : null;
            if (db != null)
            {
                foreach (var id in d.inventoryItemIds)
                {
                    var it = db.GetById(id);
                    if (it != null && items.Count < capacity) items.Add(it);
                }
                if (potionSlot1 == null) potionSlot1 = db.smallPotion;
                if (potionSlot2 == null) potionSlot2 = db.largePotion;
            }
            NotifyEquipment();
        }

        public void ExportTo(SaveData d)
        {
            d.gold = gold;
            d.potionCounts[0] = potionCounts[0];
            d.potionCounts[1] = potionCounts[1];
            d.equippedWeaponId = equippedWeapon ? equippedWeapon.itemId : "";
            d.equippedArmorId = equippedArmor ? equippedArmor.itemId : "";
            d.inventoryItemIds.Clear();
            foreach (var it in items) if (it != null) d.inventoryItemIds.Add(it.itemId);
        }
    }
}

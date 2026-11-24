using System.Text;
using UnityEngine;

namespace SoloKnight
{
    public enum ItemType { Weapon, Armor, Potion }
    public enum ItemRarity { Common, Rare, Epic }

    /// <summary>Data template for every item: weapons, armor and potions.</summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "Solo Knight/Item")]
    public class ItemSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique, stable id used by the save system.")]
        public string itemId;
        public string displayName = "New Item";
        [TextArea] public string description;
        public ItemType itemType = ItemType.Weapon;
        public ItemRarity rarity = ItemRarity.Common;

        [Header("Equipment Bonuses")]
        public int bonusAttack;
        public int bonusDefense;
        [Range(0f, 0.5f)] public float bonusCritChance;
        public float bonusMoveSpeed;
        public int bonusMaxHP;

        [Header("Potion")]
        [Range(0f, 1f)] public float healPercent;
        public int healFlat;

        [Header("Economy")]
        public int sellValue = 10;

        public bool IsEquipment => itemType == ItemType.Weapon || itemType == ItemType.Armor;

        public Color DisplayColor => itemType == ItemType.Potion ? new Color(1f, 0.3f, 0.35f) : RarityColor(rarity);

        public static Color RarityColor(ItemRarity r)
        {
            switch (r)
            {
                case ItemRarity.Rare: return new Color(0.3f, 0.6f, 1f);
                case ItemRarity.Epic: return new Color(0.75f, 0.35f, 1f);
                default: return new Color(0.85f, 0.85f, 0.85f);
            }
        }

        public string RichName => $"<color=#{ColorUtility.ToHtmlStringRGB(DisplayColor)}>{displayName}</color>";

        public string GetStatSummary()
        {
            var sb = new StringBuilder();
            if (itemType == ItemType.Potion)
            {
                if (healPercent > 0f) sb.Append($"Restores {Mathf.RoundToInt(healPercent * 100f)}% HP ");
                if (healFlat > 0) sb.Append($"+{healFlat} HP");
                return sb.ToString().Trim();
            }
            if (bonusAttack != 0) sb.AppendLine($"Attack {Signed(bonusAttack)}");
            if (bonusDefense != 0) sb.AppendLine($"Defense {Signed(bonusDefense)}");
            if (bonusCritChance != 0f) sb.AppendLine($"Crit Chance {Signed(Mathf.RoundToInt(bonusCritChance * 100f))}%");
            if (bonusMoveSpeed != 0f) sb.AppendLine($"Move Speed {(bonusMoveSpeed > 0 ? "+" : "")}{bonusMoveSpeed:0.##}");
            if (bonusMaxHP != 0) sb.AppendLine($"Max HP {Signed(bonusMaxHP)}");
            return sb.ToString().TrimEnd();
        }

        static string Signed(int v) => v >= 0 ? "+" + v : v.ToString();

        void OnValidate()
        {
            if (string.IsNullOrEmpty(itemId)) itemId = name;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace SoloKnight
{
    /// <summary>Theme + difficulty definition for one dungeon floor.</summary>
    [CreateAssetMenu(fileName = "Floor_", menuName = "Solo Knight/Floor Data")]
    public class FloorDataSO : ScriptableObject
    {
        [Header("Identity")]
        public int floorNumber = 1;
        public string floorName = "Stone Catacombs";
        public string bossName = "Bone Warden";

        [Header("Theme")]
        public Color floorColor = new Color(0.3f, 0.3f, 0.32f);
        public Color wallColor = new Color(0.45f, 0.43f, 0.4f);
        public Color ambientColor = new Color(0.25f, 0.25f, 0.3f);
        public Color fogColor = new Color(0.05f, 0.05f, 0.07f);
        public Color torchColor = new Color(1f, 0.7f, 0.4f);

        [Header("Difficulty")]
        public float enemyHealthMultiplier = 1f;
        public float enemyDamageMultiplier = 1f;
        public float bossHealthMultiplier = 1f;
        public float bossDamageMultiplier = 1f;
        public int extraEnemiesPerSpawn = 0;
        [Range(0f, 1f)] public float bruteChance = 0.2f;
        [Range(0f, 1f)] public float casterChance = 0.2f;

        [Header("Rewards")]
        public float expMultiplier = 1f;
        public float goldMultiplier = 1f;
        public float rarityBonus = 0f;

        /// <summary>Generates Floor N (beyond the authored floors) by scaling the last authored floor.</summary>
        public static FloorDataSO CreateProcedural(FloorDataSO last, int floorNumber)
        {
            var f = CreateInstance<FloorDataSO>();
            int lastNumber = last != null ? last.floorNumber : 1;
            int steps = Mathf.Max(1, floorNumber - lastNumber);
            float growth = Mathf.Pow(1.3f, steps);

            string[] names = { "Abyssal Depths", "Obsidian Vault", "Hollow Spire", "Frozen Ossuary", "Venom Warrens" };
            string[] bosses = { "Abyssal Champion", "Obsidian Colossus", "Hollow King", "Rime Lich", "Broodmother" };
            int idx = (floorNumber - 4) % names.Length;
            if (idx < 0) idx = 0;

            f.floorNumber = floorNumber;
            f.floorName = names[idx];
            f.bossName = bosses[idx];

            float hue = Mathf.Repeat(0.62f + floorNumber * 0.137f, 1f);
            f.floorColor = Color.HSVToRGB(hue, 0.25f, 0.28f);
            f.wallColor = Color.HSVToRGB(hue, 0.3f, 0.42f);
            f.ambientColor = Color.HSVToRGB(hue, 0.35f, 0.28f);
            f.fogColor = Color.HSVToRGB(hue, 0.5f, 0.06f);
            f.torchColor = Color.HSVToRGB(Mathf.Repeat(hue + 0.5f, 1f), 0.55f, 1f);

            float hp = last != null ? last.enemyHealthMultiplier : 1f;
            float dmg = last != null ? last.enemyDamageMultiplier : 1f;
            f.enemyHealthMultiplier = hp * growth;
            f.enemyDamageMultiplier = dmg * Mathf.Pow(1.2f, steps);
            f.bossHealthMultiplier = (last != null ? last.bossHealthMultiplier : 1f) * growth;
            f.bossDamageMultiplier = (last != null ? last.bossDamageMultiplier : 1f) * Mathf.Pow(1.2f, steps);
            f.extraEnemiesPerSpawn = Mathf.Min(3, (last != null ? last.extraEnemiesPerSpawn : 0) + steps / 2);
            f.bruteChance = Mathf.Min(0.4f, (last != null ? last.bruteChance : 0.2f) + 0.03f * steps);
            f.casterChance = Mathf.Min(0.4f, (last != null ? last.casterChance : 0.2f) + 0.03f * steps);
            f.expMultiplier = (last != null ? last.expMultiplier : 1f) * Mathf.Pow(1.25f, steps);
            f.goldMultiplier = (last != null ? last.goldMultiplier : 1f) * Mathf.Pow(1.25f, steps);
            f.rarityBonus = Mathf.Min(2f, (last != null ? last.rarityBonus : 0f) + 0.15f * steps);
            f.name = $"Floor_{floorNumber}_Procedural";
            return f;
        }
    }
}

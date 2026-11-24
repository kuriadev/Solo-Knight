using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SoloKnight
{
    /// <summary>Everything that persists between sessions. Serialized with JsonUtility.</summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int currentFloor = 1;
        public int highestFloorCleared = 0;

        public int level = 1;
        public int exp = 0;
        public int statPoints = 0;
        public int[] allocatedStats = new int[5];

        public int gold = 0;
        public int[] potionCounts = { 3, 1 };
        public List<string> inventoryItemIds = new List<string>();
        public string equippedWeaponId = "wpn_common_sword";
        public string equippedArmorId = "";

        public int totalKills = 0;
        public string lastSaved = "";

        public static SaveData CreateNew() => new SaveData();

        /// <summary>Repairs arrays that may be missing/short in older or hand-edited saves.</summary>
        public void Validate()
        {
            if (allocatedStats == null || allocatedStats.Length != 5)
            {
                var fixedStats = new int[5];
                if (allocatedStats != null)
                    for (int i = 0; i < Mathf.Min(5, allocatedStats.Length); i++) fixedStats[i] = allocatedStats[i];
                allocatedStats = fixedStats;
            }
            if (potionCounts == null || potionCounts.Length != 2) potionCounts = new[] { 3, 1 };
            if (inventoryItemIds == null) inventoryItemIds = new List<string>();
            currentFloor = Mathf.Max(1, currentFloor);
            level = Mathf.Max(1, level);
        }
    }

    /// <summary>JSON save file in Application.persistentDataPath (plus a PlayerPrefs flag for quick checks).</summary>
    public static class SaveSystem
    {
        const string FileName = "soloknight_save.json";
        const string PrefsKey = "SoloKnight_HasSave";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool HasSave => File.Exists(SavePath);

        public static void Save(SaveData data)
        {
            if (data == null) return;
            try
            {
                data.lastSaved = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                string json = JsonUtility.ToJson(data, true);
                string tmp = SavePath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(tmp, SavePath);
                PlayerPrefs.SetInt(PrefsKey, 1);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SoloKnight] Failed to save game to {SavePath}: {e.Message}");
            }
        }

        /// <summary>Returns the saved data, or null if there is no valid save.</summary>
        public static SaveData Load()
        {
            if (!HasSave) return null;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                data?.Validate();
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SoloKnight] Save file is corrupt and was ignored ({e.Message}).");
                return null;
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
                PlayerPrefs.DeleteKey(PrefsKey);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SoloKnight] Failed to delete save: {e.Message}");
            }
        }
    }
}

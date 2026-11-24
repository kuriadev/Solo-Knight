using System;
using UnityEngine;

namespace SoloKnight
{
    public enum MissionType { DefeatMonsters, OpenChests, CollectGold }

    /// <summary>Assigns one micro-mission per floor, tracks it and pays out bonus EXP + gold.</summary>
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        [Header("Reward Formula (per floor number)")]
        public int baseRewardExp = 40;
        public int rewardExpPerFloor = 35;
        public int baseRewardGold = 25;
        public int rewardGoldPerFloor = 20;

        public MissionType Type { get; private set; }
        public int Target { get; private set; }
        public int Progress { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsCompleted { get; private set; }
        public int RewardExp { get; private set; }
        public int RewardGold { get; private set; }

        public event Action OnMissionChanged;

        public string Description
        {
            get
            {
                switch (Type)
                {
                    case MissionType.OpenChests: return $"Open {Target} Chest{(Target == 1 ? "" : "s")}";
                    case MissionType.CollectGold: return $"Collect {Target} Gold";
                    default: return $"Defeat {Target} Monsters";
                }
            }
        }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Picks a mission that is achievable with what the floor actually contains.</summary>
        public void SetupMission(int floorNumber, int enemyCount, int chestCount, float goldMultiplier)
        {
            var rng = new System.Random(floorNumber * 7919 + Environment.TickCount);
            int roll = rng.Next(3);
            if (roll == 1 && chestCount <= 0) roll = 0;
            Type = (MissionType)roll;

            switch (Type)
            {
                case MissionType.OpenChests:
                    Target = Mathf.Clamp(2, 1, chestCount);
                    break;
                case MissionType.CollectGold:
                    Target = Mathf.RoundToInt((50 + 10 * floorNumber) * goldMultiplier / 10f) * 10;
                    break;
                default:
                    Target = Mathf.Clamp(6 + floorNumber, 3, Mathf.Max(3, enemyCount));
                    break;
            }

            Progress = 0;
            IsActive = true;
            IsCompleted = false;
            RewardExp = baseRewardExp + rewardExpPerFloor * floorNumber;
            RewardGold = baseRewardGold + rewardGoldPerFloor * floorNumber;
            OnMissionChanged?.Invoke();
        }

        public void ReportKill() { if (Type == MissionType.DefeatMonsters) Advance(1); }
        public void ReportChestOpened() { if (Type == MissionType.OpenChests) Advance(1); }
        public void ReportGold(int amount) { if (Type == MissionType.CollectGold) Advance(amount); }

        void Advance(int amount)
        {
            if (!IsActive || IsCompleted) return;
            Progress = Mathf.Min(Target, Progress + amount);
            if (Progress >= Target) Complete();
            OnMissionChanged?.Invoke();
        }

        void Complete()
        {
            IsCompleted = true;
            AudioManager.Play(Sfx.Mission, 1f, 0f);
            UIManager.Toast($"Mission Complete! +{RewardExp} EXP, +{RewardGold} Gold", new Color(0.45f, 1f, 0.5f));
            var pc = PlayerController.Instance;
            if (pc != null)
            {
                pc.Inventory.AddGold(RewardGold, false);
                pc.Stats.AddExp(RewardExp);
            }
        }
    }
}

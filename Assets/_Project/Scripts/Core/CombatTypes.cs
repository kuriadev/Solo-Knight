using System.Collections.Generic;
using UnityEngine;

namespace SoloKnight
{
    /// <summary>The five core stats the player can allocate points into.</summary>
    public enum StatType { Attack = 0, Defense = 1, CritChance = 2, MoveSpeed = 3, Health = 4 }

    /// <summary>Payload passed to anything that can be damaged.</summary>
    public struct DamageInfo
    {
        public int amount;
        public bool isCrit;
        public Vector3 sourcePosition;
        public float knockback;

        public DamageInfo(int amount, Vector3 sourcePosition, float knockback = 0f, bool isCrit = false)
        {
            this.amount = amount;
            this.sourcePosition = sourcePosition;
            this.knockback = knockback;
            this.isCrit = isCrit;
        }
    }

    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeHit(DamageInfo info);
    }

    /// <summary>Anything the player can use with the F key (chests, portals).</summary>
    public interface IInteractable
    {
        string InteractPrompt { get; }
        bool CanInteract { get; }
        Vector3 InteractPosition { get; }
        void Interact(PlayerController player);
    }

    /// <summary>Global list of interactables so the player can find the nearest one cheaply.</summary>
    public static class InteractableRegistry
    {
        public static readonly List<IInteractable> All = new List<IInteractable>();

        public static void Register(IInteractable i) { if (!All.Contains(i)) All.Add(i); }
        public static void Unregister(IInteractable i) { All.Remove(i); }

        public static IInteractable FindNearest(Vector3 position, float range)
        {
            IInteractable best = null;
            float bestSqr = range * range;
            for (int k = 0; k < All.Count; k++)
            {
                var it = All[k];
                if (it == null || !it.CanInteract) continue;
                Vector3 d = it.InteractPosition - position;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr <= bestSqr) { bestSqr = sqr; best = it; }
            }
            return best;
        }
    }
}

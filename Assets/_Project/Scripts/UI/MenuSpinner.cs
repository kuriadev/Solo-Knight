using UnityEngine;

namespace SoloKnight
{
    /// <summary>Slowly rotates the decorative knight on the title screen.</summary>
    public class MenuSpinner : MonoBehaviour
    {
        public float degreesPerSecond = 20f;
        void Update() => transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
    }
}

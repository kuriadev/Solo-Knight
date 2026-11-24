using System.Collections.Generic;
using UnityEngine;

namespace SoloKnight
{
    /// <summary>Ordered list of authored floors; anything past the end is generated procedurally.</summary>
    [CreateAssetMenu(fileName = "FloorDatabase", menuName = "Solo Knight/Floor Database")]
    public class FloorDatabaseSO : ScriptableObject
    {
        public List<FloorDataSO> floors = new List<FloorDataSO>();

        public FloorDataSO GetFloor(int floorNumber)
        {
            foreach (var f in floors)
                if (f != null && f.floorNumber == floorNumber) return f;

            FloorDataSO last = null;
            foreach (var f in floors)
                if (f != null && (last == null || f.floorNumber > last.floorNumber)) last = f;
            return FloorDataSO.CreateProcedural(last, floorNumber);
        }
    }
}

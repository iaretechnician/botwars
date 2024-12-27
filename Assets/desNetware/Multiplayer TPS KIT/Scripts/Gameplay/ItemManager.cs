
using MTPSKIT.Gameplay;
using UnityEngine;

namespace MTPSKIT.Gameplay {
    public class ItemManager : MonoBehaviour
    {
        public static ItemManager Instance;

        public LodoutForSlot[] SlotsLodout;

        private void Awake()
        {
            if (Instance) 
            {
          //      Debug.LogWarning("MultiFPS WARNING: Only one Item manager instance is allowed at once");
          //      Destroy(gameObject);
                return;
            }

            Instance = this;
        }
        

        private void OnValidate()
        {
        }
    }

    [System.Serializable]
    public class LodoutForSlot 
    {
        public string SlotName; //for inspector convienience only
        public GameObject[] availableItemsForSlot;
    }
}
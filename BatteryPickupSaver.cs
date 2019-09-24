using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Battery Pickup Saver", "collect_vood", "1.0.0")]
    [Description("Makes it so batteries don't loose charge on pickup")]

    class BatteryPickupSaver : CovalencePlugin
    {
        #region Component
        public class CapacityData : MonoBehaviour
        {
            public float capacitySeconds;
            private BaseEntity Entity { get; set; }
            private Item Item { get; set; }

            public static CapacityData CreateComponent(GameObject gameObject, float capacitySeconds)
            {
                CapacityData capacityData = gameObject.AddComponent<CapacityData>();
                capacityData.capacitySeconds = capacitySeconds;
                return capacityData;
            }

            private void Start()
            {
                Entity = GetComponent<BaseEntity>();
                print(Entity.name + " was just created!");
            }
            private void OnDestroy()
            {
                Item = GetComponent<Item>();
                if (Item != null)
                    print(Item.info.name);
                else
                    print("No item info found on Death!");
            }
        }
        #endregion

        #region Hooks
        bool CanPickupEntity(BasePlayer player, BaseCombatEntity entity)
        {
            ElectricBattery battery;
            if ((battery = entity as ElectricBattery) != null)
            {
                if ((entity.gameObject?.GetComponent<CapacityData>()) != null)
                {
                    Puts("Pickup: Found capacity data");
                }
                Puts($"Pickup: Charge: {battery.capacitySeconds} | Ent Id: {entity.net.ID}");
            }
            return true;
        }
        void OnEntitySpawned(BaseNetworkable entity)
        {
            ElectricBattery battery;
            if ((battery = entity as ElectricBattery) != null)
            {
                if (!((entity.gameObject?.GetComponent<CapacityData>()) != null))
                    CapacityData.CreateComponent(battery.gameObject, battery.capacitySeconds);
                else
                    Puts("Spawned: Found data");
                //Puts($"Spawned: Charge: {battery.capacitySeconds} | Ent Id: {entity.net.ID}");
            }
        }
        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity?.GetComponent<Item>() != null)
                Puts("Kill: Found item");
        }
        /*void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container.playerOwner != null)
            {
                CapacityData capacityData;
                if ((capacityData = item.info.gameObject?.GetComponent<CapacityData>()) != null)
                {
                    Puts("Pickup: Found capacity data " + capacityData.capacitySeconds);
                }
            }
        }
        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (container.playerOwner != null)
                CapacityData.CreateComponent(item.info.gameObject, 360f);
        }*/
        #endregion
    }
}

using System.Collections.Generic;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("Item Puller", "collect_vood", "1.0.0")]
    [Description("Gives you the ability to pull items from boxes with simply double clicking the item in craft-tab")]
    class ItemPuller : RustPlugin
    {
        const string permUse = "itempuller.use";
        const string permForcePull = "itempuller.forcepull";

        #region Config
        private Configuration config;
        class Configuration
        {
            [JsonProperty("Check for Owner (save mode)")]
            public bool checkForOwner;

            public static Configuration DefaultConfig()
            {
                return new Configuration()
                {
                    checkForOwner = false
                };
            }
        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            config = Configuration.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
        }
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Data
        private StoredData storedData;
        private Dictionary<ulong, PlayerSettings> allPlayerSettings => storedData.AllPlayerSettings;

        private class PlayerSettings
        {
            public bool enabled;
            public bool autocraft;
            public bool fromTC;
            public bool fp;
        }
        private class StoredData
        {
            public Dictionary<ulong, PlayerSettings> AllPlayerSettings { get; private set; } = new Dictionary<ulong, PlayerSettings>();
        }
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }
        private void OnServerSave()
        {
            SaveData();
        }
        private void CreatePlayerSettings(BasePlayer player)
        {
            if (!allPlayerSettings.ContainsKey(player.userID))
            {
                allPlayerSettings[player.userID] = new PlayerSettings
                {
                    enabled = true,
                    autocraft = false,
                    fromTC = true,
                    fp = false
                };
            }
        }
        #endregion

        #region Hooks
        private void Loaded() => storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);

        private void OnServerInitialized()
        {
            foreach (var player in Player.Players)
            {
                CreatePlayerSettings(player);
            }

            SaveData();
            
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permForcePull, this);

            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "NoPermission", "You don't have permission to use this." },
                { "InvalidArg", "Invalid argument" },
                { "MissingItem", "Missing <color=red>{0}</color>!" },
                { "Settings", "<color=#00AAFF>Item Puller Settings:</color>\nItem Puller - <color=#32CD32>{0}</color>\nAutocraft - <color=#32CD32>{1}</color>\nFrom Toolcupboard - <color=#32CD32>{2}</color>\nForce Pulling - <color=#32CD32>{3}</color>" },
                { "ForcePulled", "Item were force pulled <color=green>sucessfully</color>!" },
                { "ItemsPulled", "Items were moved <color=green>successfully</color>!" },
                { "NotInBuildingZone", "You need to be in building priviledge zone to use item puller!" },
                { "PlayerFull", "Cannot pull items, inventory full!" },
                { "Help", "<color=#00AAFF>Item Puller Help:</color>\n<color=#32CD32>/ip</color> - toggle item puller on/off\n<color=#32CD32>/ip <autocraft></color> - toggle autocraft on/off\n<color=#32CD32>/ip <fromtc></color> - toggle tool cupboard pulling on/off\n<color=#32CD32>/ip <fp></color> - toggle force pulling on/off\n<color=#32CD32>/ip <settings></color> - show current settings" },
                { "toggleon", "<color=green>Activated</color> Item Puller" },
                { "toggleoff", "<color=red>Disabled</color> Item Puller" },
                { "fromTCon", "<color=green>Activated</color> Item Pulling from Tool Cupboard" },
                { "fromTCoff", "<color=red>Disabled</color> Item Pulling from Tool Cupboard" },
                { "autocrafton", "<color=green>Activated</color> Item Puller auto crafting" },
                { "autocraftoff", "<color=red>Disabled</color> Item Puller auto crafting" },
                { "fpon", "<color=green>Activated</color> Item force pulling" },
                { "fpoff", "<color=red>Disabled</color> Item force pulling" }
            }, this);

            LoadConfig();     
        }
        private void OnPlayerInit(BasePlayer player) { CreatePlayerSettings(player); SaveData(); }

        object CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
        {
            var player = itemCrafter.GetComponent<BasePlayer>();
            if (!allPlayerSettings[player.userID].enabled)
                return null;
            var transferItems = new Dictionary<Item, int>();
            var hasIngredient = new List<ItemAmount>();
            int check = 0;

            if (player != null)
            {
                if (!IsInBuildingZone(player) && !allPlayerSettings[player.userID].fp)
                {
                    player.ChatMessage(string.Format(lang.GetMessage("NotInBuildingZone", this)));
                    return null;
                }
                if (!HasPerm(player))
                {
                    player.ChatMessage(string.Format(lang.GetMessage("NoPermissions", this)));
                    return null;
                }
                if (IsFull(player))
                {
                    player.ChatMessage(string.Format(lang.GetMessage("PlayerFull", this)));
                    return null;
                }

                foreach (var itemAmount in bp.ingredients)
                {
                    if (HasIngredient(itemCrafter, itemAmount, amount))
                    {
                        check++;
                        hasIngredient.Add(itemAmount);
                        if (hasIngredient.Count >= bp.ingredients.Count)
                            return null;
                        continue;
                    }
                    else
                    {
                        int required = (int)itemAmount.amount;

                        if (!allPlayerSettings[player.userID].fp || !CanForcePull(player))
                        {
                            // Gets Item info from Containers in Building Area
                            var possibleItems = GetUsableItems(player, itemAmount.itemid, required);
                            if (possibleItems != null)
                            {
                                check++;
                                foreach (var Item in possibleItems)
                                    transferItems.Add(Item.Key, Item.Value);
                            }
                            else
                            {
                                player.ChatMessage(string.Format(lang.GetMessage("MissingItem", this), itemAmount.itemDef.displayName.english));
                                break;
                            }
                        }
                        else
                            ForceUsableItem(itemCrafter, itemAmount.itemid, required);
                    }
                }
                if (check >= bp.ingredients.Count)
                {
                    foreach (var Item in transferItems)
                    {
                        if (Item.Value != 0)
                            Item.Key.SplitItem(Item.Value).MoveToContainer(player.inventory.containerMain);
                        else
                            Item.Key.MoveToContainer(player.inventory.containerMain);
                    }
                    player.ChatMessage(string.Format(lang.GetMessage("ItemsPulled", this)));
                    if (!allPlayerSettings[player.userID].autocraft)
                        return false;
                }
                else if (allPlayerSettings[player.userID].fp)
                {
                    player.ChatMessage(string.Format(lang.GetMessage("ForcePulled", this)));
                    if (!allPlayerSettings[player.userID].autocraft)
                        return false;
                }
            }
            return null;
        }
        #endregion

        #region Methods
        private void ForceUsableItem(ItemCrafter itemCrafter, int itemid, int required)
        {
            var player = itemCrafter.GetComponent<BasePlayer>();
            Item item = ItemManager.CreateByItemID(itemid, required);
            player.GiveItem(item);
        }
        Dictionary<Item, int> GetUsableItems(BasePlayer player, int itemid, int required)
        {
            var itemDef = ItemManager.FindItemDefinition(itemid);
            var building = player.GetBuildingPrivilege().GetBuilding();
            var possibleItems = new Dictionary<Item, int>();
            if (building != null)
            {
                foreach (var decayEntities in building.GetDominatingBuildingPrivilege().GetBuilding().decayEntities)
                {
                    if (decayEntities.GetEntity() is StorageContainer)
                    {
                        var storageContainer = decayEntities.GetEntity() as StorageContainer;
                        if (config.checkForOwner)
                        {
                            if (storageContainer.OwnerID != player.userID)
                                continue;
                        }
                        if (storageContainer.PrefabName.Contains("cupboard") && !allPlayerSettings[player.userID].fromTC)
                            continue;
                        foreach (Item item in storageContainer.inventory.itemList)
                        {
                            if (required == 0)
                                break;
                            if (item.info.itemid == itemDef.itemid)
                            {
                                if (required >= item.amount)
                                {
                                    required -= item.amount;
                                    possibleItems.Add(item, 0);
                                }
                                else
                                {
                                    possibleItems.Add(item, required);
                                    required = 0;
                                }
                            }
                        }
                    }
                }
                if (required == 0)
                    return possibleItems;
                else
                    return null;
            }
            return null;
        }      
        private bool IsFull(BasePlayer player)
        {
            if (player.inventory.containerMain.IsFull() && player.inventory.containerBelt.IsFull())
                return true;
            else
                return false;
        }
        private bool HasIngredient(ItemCrafter itemCrafter, ItemAmount itemAmount, int amount)
        {
            int num = 0;
            foreach (var itemContainer in itemCrafter.containers)
            {
                num += itemContainer.GetAmount(itemAmount.itemid, true);
            }
            float iAmount = itemAmount.amount * amount;
            int required = (int)iAmount - num;
            if (!(num >= iAmount))
                return false;
            else
                return true;
        }

        private bool IsEnabled(BasePlayer player) { return allPlayerSettings[player.userID].enabled; }
        private bool IsAutocraft(BasePlayer player) { return allPlayerSettings[player.userID].autocraft; }
        private bool IsFromTC(BasePlayer player) { return allPlayerSettings[player.userID].fromTC; }
        private bool IsForcePulling(BasePlayer player) { return allPlayerSettings[player.userID].fp; }

        private void ChangeEnabled(BasePlayer player, string setting)
        {
            switch (setting)
            {
                case "enabled":
                    if (IsEnabled(player))
                    {
                        allPlayerSettings[player.userID].enabled = false;
                        player.ChatMessage(string.Format(lang.GetMessage("toggleoff", this)));
                    }                    
                    else
                    {
                        allPlayerSettings[player.userID].enabled = true;
                        player.ChatMessage(string.Format(lang.GetMessage("toggleon", this)));
                    }                        
                    break;
                case "autocraft":
                    if (IsAutocraft(player))
                    {
                        allPlayerSettings[player.userID].autocraft = false;
                        player.ChatMessage(string.Format(lang.GetMessage("autocraftoff", this)));
                    }
                    else
                    {
                        allPlayerSettings[player.userID].autocraft = true;
                        player.ChatMessage(string.Format(lang.GetMessage("autocrafton", this)));
                    }
                    break;
                case "fromtc":
                    if (IsFromTC(player))
                    {
                        allPlayerSettings[player.userID].fromTC = false;
                        player.ChatMessage(string.Format(lang.GetMessage("fromTCoff", this)));
                    }
                    else
                    {
                        allPlayerSettings[player.userID].fromTC = true;
                        player.ChatMessage(string.Format(lang.GetMessage("fromTCon", this)));
                    }
                    break;
                case "fp":
                    if (!CanForcePull(player))
                    {
                        player.ChatMessage(string.Format(lang.GetMessage("NoPermission", this)));
                        break;
                    }
                    else
                    {
                        if (IsForcePulling(player))
                        {
                            allPlayerSettings[player.userID].fp = false;
                            player.ChatMessage(string.Format(lang.GetMessage("fpoff", this)));
                        }
                        else
                        {
                            allPlayerSettings[player.userID].fp = true;
                            player.ChatMessage(string.Format(lang.GetMessage("fpon", this)));
                        }
                        break;
                    }
            }
            SaveData();
        }
        #endregion

        #region Commands
        [ChatCommand("ip")]
        void ItemPullerChatCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!(args.Length > 0))
            { 
                ChangeEnabled(player, "enabled");
                return;
            }
            else
            {
                switch (args[0].ToLower())
                {
                    case "help":
                        player.ChatMessage(string.Format(lang.GetMessage("Help", this)));
                        break;
                    case "ac":
                    case "autocraft":
                        ChangeEnabled(player, "autocraft");
                        break;
                    case "fromtc":
                        ChangeEnabled(player, "fromtc");
                        break;
                    case "fp":
                    case "forcepull":
                        ChangeEnabled(player, "fp");
                        break;
                    case "settings":
                        var ps = allPlayerSettings[player.userID];
                        player.ChatMessage(string.Format(lang.GetMessage("Settings", this), ps.enabled, ps.autocraft, ps.fromTC, ps.fp));
                        break;
                    default:
                        player.ChatMessage(string.Format(lang.GetMessage("InvalidArg", this)));
                        break;
                }
            }
        }
        [ChatCommand("t")]
        void tt(BasePlayer player, string cmd, string[] args)
        {
            CreatePlayerSettings(player);
            SaveData();
        }
        #endregion

        private bool HasPerm(BasePlayer player) => (permission.UserHasPermission(player.UserIDString, permUse));
        private bool IsInBuildingZone(BasePlayer player) => (player.IsBuildingAuthed());
        private bool CanForcePull(BasePlayer player) => (permission.UserHasPermission(player.UserIDString, permForcePull));
    }
}


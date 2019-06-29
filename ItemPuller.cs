using System.Collections.Generic;
using Oxide.Core;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Item Puller", "collect_vood", "1.0.5")]
    [Description("Gives you the ability to pull items from containers")]
    class ItemPuller : RustPlugin
    {
        const string permUse = "itempuller.use";
        const string permForcePull = "itempuller.forcepull";

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "NoPermission", "You don't have permission to use this." },
                { "InvalidArg", "Invalid argument" },
                { "MissingItem", "Missing <color=#ff0000>{0}</color>!" },
                { "Settings", "<color=#00AAFF>Item Puller Settings:</color>\nItem Puller - <color=#32CD32>{0}</color>\nAutocraft - <color=#32CD32>{1}</color>\nFrom Toolcupboard - <color=#32CD32>{2}</color>\nForce Pulling - <color=#32CD32>{3}</color>" },
                { "ForcePulled", "Item were force pulled <color=#7FFF00>sucessfully</color>!" },
                { "ItemsPulled", "Items were moved <color=#7FFF00>successfully</color>!" },
                { "NotInBuildingZone", "You need to be in building priviledge zone to use item puller!" },
                { "PlayerFull", "Cannot pull items, inventory full!" },
                { "Help", "<color=#00AAFF>Item Puller Help:</color>\n<color=#32CD32>/ip</color> - toggle item puller on/off\n<color=#32CD32>/ip <autocraft></color> - toggle autocraft on/off\n<color=#32CD32>/ip <fromtc></color> - toggle tool cupboard pulling on/off\n<color=#32CD32>/ip <fp></color> - toggle force pulling on/off\n<color=#32CD32>/ip <settings></color> - show current settings" },
                { "toggleon", "<color=#7FFF00>Activated</color> Item Puller" },
                { "toggleoff", "<color=#ff0000>Disabled</color> Item Puller" },
                { "fromTCon", "<color=#7FFF00>Activated</color> Item Pulling from Tool Cupboard" },
                { "fromTCoff", "<color=#ff0000>Disabled</color> Item Pulling from Tool Cupboard" },
                { "autocrafton", "<color=#7FFF00>Activated</color> Item Puller auto crafting" },
                { "autocraftoff", "<color=#ff0000>Disabled</color> Item Puller auto crafting" },
                { "fpon", "<color=#7FFF00>Activated</color> Item force pulling" },
                { "fpoff", "<color=#ff0000>Disabled</color> Item force pulling" }
            }, this);
        }
        #endregion

        #region Config
        private Configuration config;
        private class Configuration
        {
            [JsonProperty("Check for Owner (save mode)")]
            public bool checkForOwner = false;

            [JsonProperty(PropertyName = "Player Default Settings")]
            public Dictionary<string, bool> PlayerDefaultSettings = new Dictionary<string, bool>
            {
                { "Enabled", false },
                { "Autocraft", false },
                { "Pull from ToolCupboard", true },
                { "Force Pull (recommend to not set true)", false }
            };

        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            config = new Configuration();
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
                    enabled = config.PlayerDefaultSettings["Enabled"],
                    autocraft = config.PlayerDefaultSettings["Autocraft"],
                    fromTC = config.PlayerDefaultSettings["Pull from ToolCupboard"],
                    fp = config.PlayerDefaultSettings["Force Pull (recommend to not set true)"]
                };
            }
        }
        #endregion

        #region Hooks
        private void Init()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);

            foreach (var player in Player.Players)
            {
                CreatePlayerSettings(player);
            }

            SaveData();
            
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permForcePull, this);

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
                    player.ChatMessage(string.Format(lang.GetMessage("NotInBuildingZone", this, player.UserIDString)));
                    return null;
                }
                if (!HasPerm(player))
                {
                    player.ChatMessage(string.Format(lang.GetMessage("NoPermissions", this, player.UserIDString)));
                    return null;
                }
                if (IsFull(player))
                {
                    player.ChatMessage(string.Format(lang.GetMessage("PlayerFull", this, player.UserIDString)));
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
                                player.ChatMessage(string.Format(lang.GetMessage("MissingItem", this, player.UserIDString), itemAmount.itemDef.displayName.english));
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
                    player.ChatMessage(string.Format(lang.GetMessage("ItemsPulled", this, player.UserIDString)));
                    if (!allPlayerSettings[player.userID].autocraft)
                        return false;
                }
                else if (allPlayerSettings[player.userID].fp)
                {
                    player.ChatMessage(string.Format(lang.GetMessage("ForcePulled", this, player.UserIDString)));
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
            if (item != null)
                player.GiveItem(item);
        }
        Dictionary<Item, int> GetUsableItems(BasePlayer player, int itemid, int required)
        {
            var itemDef = ItemManager.FindItemDefinition(itemid);
            if (itemDef == null)
                return null;
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
                        player.ChatMessage(string.Format(lang.GetMessage("toggleoff", this, player.UserIDString)));
                    }                    
                    else
                    {
                        allPlayerSettings[player.userID].enabled = true;
                        player.ChatMessage(string.Format(lang.GetMessage("toggleon", this, player.UserIDString)));
                    }                        
                    break;
                case "autocraft":
                    if (IsAutocraft(player))
                    {
                        allPlayerSettings[player.userID].autocraft = false;
                        player.ChatMessage(string.Format(lang.GetMessage("autocraftoff", this, player.UserIDString)));
                    }
                    else
                    {
                        allPlayerSettings[player.userID].autocraft = true;
                        player.ChatMessage(string.Format(lang.GetMessage("autocrafton", this, player.UserIDString)));
                    }
                    break;
                case "fromtc":
                    if (IsFromTC(player))
                    {
                        allPlayerSettings[player.userID].fromTC = false;
                        player.ChatMessage(string.Format(lang.GetMessage("fromTCoff", this, player.UserIDString)));
                    }
                    else
                    {
                        allPlayerSettings[player.userID].fromTC = true;
                        player.ChatMessage(string.Format(lang.GetMessage("fromTCon", this, player.UserIDString)));
                    }
                    break;
                case "fp":
                    if (!CanForcePull(player))
                    {
                        player.ChatMessage(string.Format(lang.GetMessage("NoPermission", this, player.UserIDString)));
                        break;
                    }
                    else
                    {
                        if (IsForcePulling(player))
                        {
                            allPlayerSettings[player.userID].fp = false;
                            player.ChatMessage(string.Format(lang.GetMessage("fpoff", this, player.UserIDString)));
                        }
                        else
                        {
                            allPlayerSettings[player.userID].fp = true;
                            player.ChatMessage(string.Format(lang.GetMessage("fpon", this, player.UserIDString)));
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
                        player.ChatMessage(string.Format(lang.GetMessage("Help", this, player.UserIDString)));
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
                        player.ChatMessage(string.Format(lang.GetMessage("Settings", this, player.UserIDString), ps.enabled, ps.autocraft, ps.fromTC, ps.fp));
                        break;
                    default:
                        player.ChatMessage(string.Format(lang.GetMessage("InvalidArg", this, player.UserIDString)));
                        break;
                }
            }
        }
        #endregion
        
        private bool HasPerm(BasePlayer player) => (permission.UserHasPermission(player.UserIDString, permUse));
        private bool IsInBuildingZone(BasePlayer player) => (player.IsBuildingAuthed());
        private bool CanForcePull(BasePlayer player) => (permission.UserHasPermission(player.UserIDString, permForcePull));
    }
}


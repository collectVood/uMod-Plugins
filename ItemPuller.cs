using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Item Puller", "collect_vood", "1.1.0")]
    [Description("Gives you the ability to pull items from containers")]
    class ItemPuller : CovalencePlugin
    {
        [PluginReference]
        private Plugin Friends, Clans;

        #region Constants
        const string permUse = "itempuller.use";
        const string permForcePull = "itempuller.forcepull";
        const string permBuildPull = "itempuller.buildpull";
        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "NoPermission", "You don't have permission to use this." },
                { "NoPermissionBuild", "You don't have permission to use build pull." },
                { "InvalidArg", "Invalid argument" },
                { "MissingItem", "Missing <color=#ff0000>{0}</color>!" },
                { "Settings", "<color=#00AAFF>Item Puller Settings:</color>\nItem Puller: {0}\nAutocraft: {1}\nFrom Toolcupboard: {2}\nForce Pulling: {3}" },
                { "ForcePulled", "Items were force pulled <color=#7FFF00>sucessfully</color>!" },
                { "ItemsPulled", "Items were moved <color=#7FFF00>successfully</color>!" },
                { "NotInBuildingZone", "You need to be in building priviledge zone to use item puller!" },
                { "PlayerFull", "Cannot pull items, inventory full!" },
                { "Help", "<color=#00AAFF>Item Puller Help:</color>\n<color=#7FFF00>/ip</color> - toggle item puller on/off\n<color=#7FFF00>/ip <autocraft></color> - toggle autocraft on/off\n<color=#7FFF00>/ip <fromtc></color> - toggle tool cupboard pulling on/off\n<color=#7FFF00>/ip <fp></color> - toggle force pulling on/off\n<color=#7FFF00>/ip <settings></color> - show current settings" },
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
            [JsonProperty("Command")]
            public string Command = "ip";
            [JsonProperty("Pull on build")]
            public bool pullOnBuild = true;
            [JsonProperty("Check for Owner")]
            public bool checkForOwner = false;
            [JsonProperty("Check for Friend")]
            public bool checkForFriends = false;
            [JsonProperty("Check for Clan")]
            public bool checkForClans = false;
            [JsonProperty(PropertyName = "Player Default Settings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
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
            SaveConfig();
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

        private void OnServerSave() => SaveData();
        private void Unload() => SaveData();

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

            foreach (var player in BasePlayer.activePlayerList)
                CreatePlayerSettings(player);

            AddCovalenceCommand(config.Command, nameof(ItemPullerChatCommand));

            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permForcePull, this);
            permission.RegisterPermission(permBuildPull, this);
        }
        private void Loaded()
        {
            if (config.checkForFriends && (!Friends || !Friends.IsLoaded))
                PrintWarning("You are missing the Friends API plugin(check for Friends won't work)");
            if (config.checkForClans && (!Clans || !Clans.IsLoaded))
                PrintWarning("You are missing the Clans plugin(check for Clans won't work)");
        }
        object OnMessagePlayer(string message, BasePlayer player)
        {
            if (player == null)
                return null;
            if (message == "Can't afford to place!" && allPlayerSettings[player.userID].enabled)
                return true;
            return null;
        }
        object CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
        {
            var player = itemCrafter.GetComponent<BasePlayer>();
            if (player == null)
                return null;
            if (!allPlayerSettings[player.userID].enabled)
                return null;
            if (CheckPermissions(player) == null)
                return null;

            Results results = ScanItems(player, bp.ingredients, amount);

            object status = GiveItems(player, results, bp.ingredients);

            return status;
        }
        object CanAffordToPlace(BasePlayer player, Planner planner, Construction construction)
        {
            if (!config.pullOnBuild || !allPlayerSettings[player.userID].enabled)
                return null;
            var itemCrafter = player.GetComponent<ItemCrafter>();
            if (itemCrafter == null)
                return null;
            if (CheckPermissions(player, true) == null)
                return null;

            Results results = ScanItems(player, construction.defaultGrade.costToBuild, 1);

            object status = GiveItems(player, results, construction.defaultGrade.costToBuild);

            return status;
        }
        private void OnPlayerInit(BasePlayer player) { CreatePlayerSettings(player); }
        #endregion

        #region Commands
        void ItemPullerChatCommand(IPlayer player, string cmd, string[] args)
        {
            BasePlayer bPlayer;
            if ((bPlayer = player.Object as BasePlayer) == null)
                return;
            if (!(args.Length > 0))
            {
                ChangeEnabled(bPlayer, "enabled");
                return;
            }
            else
            {
                switch (args[0].ToLower())
                {
                    case "help":
                        player.Reply(string.Format(lang.GetMessage("Help", this, player.Id)));
                        break;
                    case "ac":
                    case "autocraft":
                        ChangeEnabled(bPlayer, "autocraft");
                        break;
                    case "fromtc":
                        ChangeEnabled(bPlayer, "fromtc");
                        break;
                    case "fp":
                    case "forcepull":
                        ChangeEnabled(bPlayer, "fp");
                        break;
                    case "settings":
                        var ps = allPlayerSettings[bPlayer.userID];
                        player.Reply(string.Format(lang.GetMessage("Settings", this, player.Id), GetOptionFormatted(ps.enabled), GetOptionFormatted(ps.autocraft), GetOptionFormatted(ps.fromTC), GetOptionFormatted(ps.fp)));
                        break;
                    default:
                        player.Reply(string.Format(lang.GetMessage("InvalidArg", this, player.Id)));
                        break;
                }
            }
        }
        string GetOptionFormatted(bool option)
        {
            if (option)
                return "<color=#7FFF00>Activated</color>";
            else
                return "<color=#ff0000>Disabled</color>";
        }
        #endregion

        #region Helpers

        class Results
        {
            public bool hasResources = false;
            public Dictionary<Item, int> transferItems = new Dictionary<Item, int>();
            public int check = 0;
        }

        Results ScanItems(BasePlayer player, List<ItemAmount> ingredients, int amount)
        {
            var results = new Results();

            var hasIngredient = new List<ItemAmount>();

            var itemCrafter = player.GetComponent<ItemCrafter>();
            if (itemCrafter == null)
                return results;

            foreach (var itemAmount in ingredients)
            {
                if (HasIngredient(itemCrafter, itemAmount, amount))
                {
                    results.check++;
                    hasIngredient.Add(itemAmount);
                    if (hasIngredient.Count >= ingredients.Count)
                    {
                        results.hasResources = true;
                        return results;
                    }
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
                            results.check++;
                            foreach (var Item in possibleItems)
                                results.transferItems.Add(Item.Key, Item.Value);
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
            return results;
        }
        object GiveItems(BasePlayer player, Results results, List<ItemAmount> ingredients)
        {
            if (results.hasResources)
                return null;

            if (results.check >= ingredients.Count)
            {
                foreach (var Item in results.transferItems)
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
            return null;
        }
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
                        if (storageContainer.OwnerID != player.userID)
                        {
                            if (config.checkForOwner)
                                continue;
                            if (config.checkForFriends && Friends != null && Friends.IsLoaded)
                            {
                                if (!(bool)Friends?.Call("AreFriends", player.userID, storageContainer.OwnerID))
                                    continue;
                            }
                            if (config.checkForClans && Clans != null && Clans.IsLoaded)
                            {
                                string clantag = Clans?.Call("GetClanOf", storageContainer.OwnerID) as string;
                                if (clantag == null)
                                    continue;
                                JObject clan = Clans?.Call("GetClan", clantag) as JObject;
                                if (clan == null)
                                    continue;
                                JArray members = clan["members"] as JArray;
                                if (members == null)
                                    continue;
                                if (!members.Contains(player.UserIDString))
                                    continue;
                            }
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
        object CheckPermissions(BasePlayer player, bool OnBuild = false)
        {
            if (!IsInBuildingZone(player) && !allPlayerSettings[player.userID].fp)
            {
                player.ChatMessage(string.Format(lang.GetMessage("NotInBuildingZone", this, player.UserIDString)));
                return null;
            }
            if (!HasPerm(player, permUse))
            {
                player.ChatMessage(string.Format(lang.GetMessage("NoPermission", this, player.UserIDString)));
                return null;
            }
            if (OnBuild && !HasPerm(player, permBuildPull))
            {
                player.ChatMessage(string.Format(lang.GetMessage("NoPermissionBuild", this, player.UserIDString)));
                return null;
            }
            if (IsFull(player))
            {
                player.ChatMessage(string.Format(lang.GetMessage("PlayerFull", this, player.UserIDString)));
                return null;
            }
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
        }

        private bool HasPerm(BasePlayer player, string perm) => (permission.UserHasPermission(player.UserIDString, perm));
        private bool IsInBuildingZone(BasePlayer player) => (player.IsBuildingAuthed());
        private bool CanForcePull(BasePlayer player) => (permission.UserHasPermission(player.UserIDString, permForcePull));
        #endregion
    }
}

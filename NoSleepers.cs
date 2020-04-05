using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("NoSleepers", "collect_vood", "0.5.6")]
    [Description("Prevents players from sleeping and optionally removes player corpses and bags")]

    class NoSleepers : CovalencePlugin
    {

        #region Config     

        private ConfigurationFile Configuration;
        private class ConfigurationFile
        {
            [JsonProperty(PropertyName = "Kill existing")]
            public bool KillExisting = false;
            [JsonProperty(PropertyName = "Remove corpses")]
            public bool RemoveCorpses = true;
            [JsonProperty(PropertyName = "Remove bags")]
            public bool RemoveBags = false;
            [JsonProperty(PropertyName = "Save last position")]
            public bool SaveLastPosition = false;
            [JsonProperty(PropertyName = "Save last inventory")]
            public bool SaveLastInventory = false;

            [JsonProperty(PropertyName = "Exclude Permission")]
            public string PermExclude = "nosleepers.exclude";

            [JsonProperty(PropertyName = "Player Inactivity Data Removal (hours)")]
            public int InactivityRemovalTime = 168;
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Configuration = new ConfigurationFile();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Configuration = Config.ReadObject<ConfigurationFile>();
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(Configuration);

        #endregion

        #region Data

        private StoredData AllStoredData;
        private Dictionary<string, PlayerData> AllPlayerData => AllStoredData.AllPlayerData;

        private class StoredData
        {
            [JsonProperty("All Player Data", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, PlayerData> AllPlayerData { get; private set; } = new Dictionary<string, PlayerData>();
        }

        public class PlayerData
        {
            [JsonProperty("Player Items")]
            public List<PlayerItem> PlayerItems;

            [JsonProperty("Last position")]
            public Vector3 LastPosition;

            [JsonProperty("Last active")]
            public long LastActive = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
          
            public PlayerData()
            {

            }
        }

        public class PlayerItem
        {
            public enum ContainerType { Wear, Main, Belt }

            [JsonProperty("Name")]
            public string Shortname;
            [JsonProperty("Amount")]
            public int Amount;
            [JsonProperty("SkinId")]
            public ulong SkinId;
            [JsonProperty("Position")]
            public int Position;
            [JsonProperty("Condition")]
            public float Condition;
            [JsonProperty("Magazine")]
            public int Magazine;
            [JsonProperty("Container")]
            public ContainerType Container;
            [JsonProperty("Mods")]
            public List<int> Mods;

            public PlayerItem(string shortName, int amount, ulong skinId, int position, float condition, int magazine, ContainerType container, List<int> mods)
            {
                Shortname = shortName;
                Amount = amount;
                SkinId = skinId;
                Position = position;
                Condition = condition;
                Magazine = magazine;
                Container = container;
                Mods = mods;
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, AllStoredData);

        private void LoadData()
        {
            AllStoredData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            if (AllStoredData == null) AllStoredData = new StoredData();

            SaveData();
        }

        private void OnServerSave() => ClearUpData();

        private void Unload() => SaveData();

        private void ClearUpData()
        {
            if (Configuration.InactivityRemovalTime == 0) 
                return;

            var copy = new Dictionary<string, PlayerData>(AllPlayerData);
            foreach (var colData in copy)
            {
                if (colData.Value.LastActive == 0) 
                    continue;
                if (colData.Value.LastActive + (Configuration.InactivityRemovalTime * 3600) < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) 
                    AllPlayerData.Remove(colData.Key);
            }

            SaveData();
        }

        #endregion

        #region Initialization
       
        void OnServerInitialized()
        {
            #if !RUST
            throw new NotSupportedException("This plugin does not support this game");
            #endif

            permission.RegisterPermission(Configuration.PermExclude, this);

            LoadData();

            if (!Configuration.KillExisting) 
                return;

            var killCount = 0;
            foreach (var ply in BasePlayer.sleepingPlayerList.ToList())
            {
                if (BasePlayer.activePlayerList.Contains(ply)) continue;
                if (!ply.IsDestroyed)
                {
                    ply.Die();
                    killCount++;
                }
            }

            if (killCount > 0) 
                Puts($"Killed {killCount} {(killCount == 1 ? "sleeper" : "sleepers")}");
        }

        #endregion

        #region Hooks

        void OnPlayerRespawned(BasePlayer player)
        {
            if (player.IsSleeping() && !permission.UserHasPermission(player.UserIDString, Configuration.PermExclude))
            {
                player.EndSleeping();

                if (!AllPlayerData.ContainsKey(player.UserIDString)) 
                    return;

                var playerData = AllPlayerData[player.UserIDString];
                if (playerData.PlayerItems != null) 
                    GivePlayerItems(player, playerData.PlayerItems);

                AllPlayerData.Remove(player.UserIDString);
            }
        }

        object OnPlayerRespawn(BasePlayer player)
        {
            if (!AllPlayerData.ContainsKey(player.UserIDString)) 
                return null;

            var playerData = AllPlayerData[player.UserIDString];
            if (playerData.LastPosition == Vector3.zero) 
                return null;

            Vector3 latestPos = playerData.LastPosition;
            playerData.LastPosition = Vector3.zero;

            return new BasePlayer.SpawnPoint { pos = latestPos };
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (!player.IsDestroyed && !permission.UserHasPermission(player.UserIDString, Configuration.PermExclude))
            {
                if (Configuration.SaveLastPosition || Configuration.SaveLastInventory)
                {
                    var playerData = new PlayerData();

                    if (Configuration.SaveLastPosition)
                        playerData.LastPosition = player.transform.position;
                    if (Configuration.SaveLastInventory)
                        playerData.PlayerItems = GetAllItems(player.inventory);

                    if (AllPlayerData.ContainsKey(player.UserIDString))
                        AllPlayerData[player.UserIDString] = playerData;
                    else 
                        AllPlayerData.Add(player.UserIDString, playerData);
                }

                player.Die();
            }
        }

        object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg.cmd.Name == "sleep")
            {
                var bPlayer = arg.Player();
                if (bPlayer == null || bPlayer.IsAdmin || permission.UserHasPermission(bPlayer.UserIDString, Configuration.PermExclude)) 
                    return null;

                return false;
            }
            return null;
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (Configuration.RemoveCorpses && entity.ShortPrefabName.Equals("player_corpse")) 
                entity.Kill();
            if (Configuration.RemoveBags && entity.ShortPrefabName.Equals("item_drop_backpack")) 
                entity.Kill();
        }

        //object OnPlayerSleep(BasePlayer player) => true; // TODO: Hook might be causing local player duplication

        #endregion

        #region Helpers

        List<PlayerItem> GetAllItems(PlayerInventory inventory)
        {           
            var playerItems = new List<PlayerItem>();

            foreach (var item in inventory.containerBelt.itemList)
            {
                List<int> mods = new List<int>();

                int magazine = 0;
                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null) magazine = weapon.primaryMagazine.contents;
                if (item.contents != null)
                {
                    foreach (var contentItem in item.contents.itemList)
                    {
                        mods.Add(contentItem.info.itemid);
                    };
                }
                playerItems.Add(new PlayerItem(item.info.shortname, item.amount, item.skin, item.position, item.condition, magazine, PlayerItem.ContainerType.Belt, mods));

            }
            foreach (var item in inventory.containerMain.itemList)
            {
                List<int> mods = new List<int>();

                int magazine = 0;
                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null) magazine = weapon.primaryMagazine.contents;
                if (item.contents != null)
                {
                    foreach (var contentItem in item.contents.itemList)
                    {
                        mods.Add(contentItem.info.itemid);
                    };
                }
                playerItems.Add(new PlayerItem(item.info.shortname, item.amount, item.skin, item.position, item.condition, magazine, PlayerItem.ContainerType.Main, mods));
            }
            foreach (var item in inventory.containerWear.itemList)
            {
                playerItems.Add(new PlayerItem(item.info.shortname, item.amount, item.skin, item.position, item.condition, 0, PlayerItem.ContainerType.Wear, new List<int>()));
            }
            return playerItems;
        }

        void GivePlayerItems(BasePlayer player, List<PlayerItem> playerItems)
        {
            player.inventory.containerBelt.Clear();
            player.inventory.containerMain.Clear();
            player.inventory.containerWear.Clear();
            ItemManager.DoRemoves();

            foreach (var playerItem in playerItems)
            {
                var item = ItemManager.CreateByName(playerItem.Shortname, playerItem.Amount, playerItem.SkinId);
                var weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null) weapon.primaryMagazine.contents = playerItem.Magazine;
                item.condition = playerItem.Condition;

                switch (playerItem.Container)
                {
                    case PlayerItem.ContainerType.Belt:
                        item.MoveToContainer(player.inventory.containerBelt, playerItem.Position);
                        break;
                    case PlayerItem.ContainerType.Main:
                        item.MoveToContainer(player.inventory.containerMain, playerItem.Position);
                        break;
                    case PlayerItem.ContainerType.Wear:
                        item.MoveToContainer(player.inventory.containerWear, playerItem.Position);
                        break;
                }

                foreach (var mod in playerItem.Mods)
                {
                    Item modItem = ItemManager.CreateByItemID(mod, 1, 0);

                    item.contents.AddItem(modItem.info, 1);
                }
            }
        }

        #endregion

    }
}
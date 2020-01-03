using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("NoSleepers", "collect_vood", "0.5.2", ResourceId = 1452)]
    [Description("Prevents players from sleeping and optionally removes player corpses and bags")]

    class NoSleepers : CovalencePlugin
    {

        #region Config     

        private Configuration config;
        private class Configuration
        {
            [JsonProperty(PropertyName = "Kill existing")]
            public bool killExisting = false;
            [JsonProperty(PropertyName = "Remove corpses")]
            public bool removeCorpses = true;
            [JsonProperty(PropertyName = "Remove bags")]
            public bool removeBags = false;
            [JsonProperty(PropertyName = "Save last position")]
            public bool saveLastPosition = false;
            [JsonProperty(PropertyName = "Save last inventory")]
            public bool saveLastInventory = false;

            [JsonProperty(PropertyName = "Exclude Permission")]
            public string permExclude = "nosleepers.exclude";

            [JsonProperty(PropertyName = "Player Inactivity Data Removal (days)")]
            public int inactivityRemovalTime = 7;
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
        private Dictionary<string, PlayerData> allPlayerData => storedData.AllPlayerData;

        private class StoredData
        {
            public Dictionary<string, PlayerData> AllPlayerData { get; private set; } = new Dictionary<string, PlayerData>();
        }

        public class PlayerData
        {
            [JsonProperty("Player Items")]
            public List<PlayerItem> _playerItems;

            [JsonProperty("Last position")]
            public Vector3 _lastPosition;

            [JsonProperty("Last active")]
            public long _lastActive = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
          
            public PlayerData()
            {

            }
        }

        public class PlayerItem
        {
            public enum Container { Wear, Main, Belt }

            [JsonProperty("Name")]
            public string _shortName;
            [JsonProperty("Amount")]
            public int _amount;
            [JsonProperty("SkinId")]
            public ulong _skinId;
            [JsonProperty("Position")]
            public int _position;
            [JsonProperty("Condition")]
            public float _condition;
            [JsonProperty("Magazine")]
            public int _magazine;
            [JsonProperty("Container")]
            public Container _container;
            [JsonProperty("Mods")]
            public List<int> _mods;

            public PlayerItem(string shortName, int amount, ulong skinId, int position, float condition, int magazine, Container container, List<int> mods)
            {
                _shortName = shortName;
                _amount = amount;
                _skinId = skinId;
                _position = position;
                _condition = condition;
                _magazine = magazine;
                _container = container;
                _mods = mods;
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

        private void OnServerSave()
        {
            ClearUpData();
            SaveData();
        }
        private void Unload() => SaveData();

        private void ClearUpData()
        {
            if (config.inactivityRemovalTime == 0) return;

            var copy = new Dictionary<string, PlayerData>(allPlayerData);
            foreach (var colData in copy)
            {
                if (colData.Value._lastActive == 0) continue;
                if (colData.Value._lastActive + (config.inactivityRemovalTime * 86400) < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) allPlayerData.Remove(colData.Key);
            }
        }

        #endregion

        #region Initialization
       
        void OnServerInitialized()
        {
            #if !RUST
            throw new NotSupportedException("This plugin does not support this game");
            #endif

            permission.RegisterPermission(config.permExclude, this);

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);

            if (!config.killExisting) return;

            var killCount = 0;
            var sleepers = BasePlayer.sleepingPlayerList;
            foreach (var sleeper in sleepers.ToArray())
            {
                if (!sleeper.IsDead() && !sleeper.IsDestroyed)
                {
                    sleeper.Kill();
                    killCount++;
                }
                sleepers.Remove(sleeper);
            }
            if (killCount > 0) Puts($"Killed {killCount} {(killCount == 1 ? "sleeper" : "sleepers")}");
        }

        #endregion

        #region Methods

        void OnPlayerInit(BasePlayer player)
        {
            if (player.IsDead() && !permission.UserHasPermission(player.UserIDString, config.permExclude))
            {
                if (!allPlayerData.ContainsKey(player.UserIDString))
                {
                    player.Respawn();
                    return;
                }

                var playerData = allPlayerData[player.UserIDString];
                if (playerData._lastPosition != Vector3.zero) player.RespawnAt(playerData._lastPosition, new Quaternion());
                else player.Respawn();

                if (playerData._playerItems != null) GivePlayerItems(player, playerData._playerItems);
            }
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (player.IsSleeping() && !permission.UserHasPermission(player.UserIDString, config.permExclude)) player.EndSleeping();
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (!player.IsDestroyed && !permission.UserHasPermission(player.UserIDString, config.permExclude))
            {
                var playerData = new PlayerData();
                if (config.saveLastPosition) playerData._lastPosition = player.transform.position;
                if (config.saveLastInventory) playerData._playerItems = GetAllItems(player.inventory);
                if (allPlayerData.ContainsKey(player.UserIDString)) allPlayerData[player.UserIDString] = playerData;
                else allPlayerData.Add(player.UserIDString, playerData);

                player.Kill();
            }
        }

        object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg.cmd.Name == "sleep")
            {
                var bPlayer = arg.Player();
                if (bPlayer == null || bPlayer.IsAdmin || permission.UserHasPermission(bPlayer.UserIDString, config.permExclude)) return null;
                return false;
            }
            return null;
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (config.removeCorpses && entity.ShortPrefabName.Equals("player_corpse")) entity.Kill();
            if (config.removeBags && entity.ShortPrefabName.Equals("item_drop_backpack")) entity.Kill();
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
                playerItems.Add(new PlayerItem(item.info.shortname, item.amount, item.skin, item.position, item.condition, magazine, PlayerItem.Container.Belt, mods));

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
                playerItems.Add(new PlayerItem(item.info.shortname, item.amount, item.skin, item.position, item.condition, magazine, PlayerItem.Container.Main, mods));
            }
            foreach (var item in inventory.containerWear.itemList)
            {
                playerItems.Add(new PlayerItem(item.info.shortname, item.amount, item.skin, item.position, item.condition, 0, PlayerItem.Container.Wear, new List<int>()));
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
                var item = ItemManager.CreateByName(playerItem._shortName, playerItem._amount, playerItem._skinId);
                var weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null) weapon.primaryMagazine.contents = playerItem._magazine;
                item.condition = playerItem._condition;

                switch (playerItem._container)
                {
                    case PlayerItem.Container.Belt:
                        item.MoveToContainer(player.inventory.containerBelt, playerItem._position);
                        break;
                    case PlayerItem.Container.Main:
                        item.MoveToContainer(player.inventory.containerMain, playerItem._position);
                        break;
                    case PlayerItem.Container.Wear:
                        item.MoveToContainer(player.inventory.containerWear, playerItem._position);
                        break;
                }

                foreach (var mod in playerItem._mods)
                {
                    Item modItem = ItemManager.CreateByItemID(mod, 1, 0);

                    item.contents.AddItem(modItem.info, 1);
                }
            }
        }

        #endregion

    }
}
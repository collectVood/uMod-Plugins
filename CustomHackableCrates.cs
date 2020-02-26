using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Custom Hackable Crates", "collect_vood", "1.0.0")]
    [Description("Allows to set different unlock times")]
    class CustomHackableCrates : CovalencePlugin
    {
        #region Config

        private ConfigurationFile Configuration;

        private class ConfigurationFile
        {
            [JsonProperty(PropertyName = "Permissions Time Data", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<PermissionTimeData> PermissionTimeData = new List<PermissionTimeData> { new PermissionTimeData() };

            [JsonProperty(PropertyName = "Only team/clan/friend loot access")]
            public bool OnlyLimitedAccess = false;
        }

        private class PermissionTimeData
        {
            [JsonProperty(PropertyName = "Permission")]
            public string Permission = string.Empty;

            [JsonProperty(PropertyName = "Unlock time")]
            public float UnlockTime = 900f; 
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

        protected override void SaveConfig() => Config.WriteObject(Config);

        #endregion

        #region Data

        private StoredData Data;
        private Dictionary<uint, HackableCrateData> AllHackableCrates => Data.AllHackableCrates;

        private class StoredData
        {
            [JsonProperty("All Hackable Crate Data", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<uint, HackableCrateData> AllHackableCrates = new Dictionary<uint, HackableCrateData>();
        }

        public class HackableCrateData
        {
            public HackableCrateData(BasePlayer player, Vector3 position, DateTime dateTime)
            {
                UnlockerName = player.displayName;
                UnlockerID = player.UserIDString;
                Position = position;
                DateTime = dateTime;
            }

            [JsonProperty(PropertyName = "Unlocker name")]
            public string UnlockerName;

            [JsonProperty(PropertyName = "Unlocker id")]
            public string UnlockerID;

            [JsonProperty(PropertyName = "Position")]
            public Vector3 Position;

            [JsonProperty(PropertyName = "Date time")]
            public DateTime DateTime;

        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, Data);

        private void LoadData()
        {
            Data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            if (Data == null) Data = new StoredData();

            SaveData();
        }

        private void OnServerSave() => ClearUpData();

        private void Unload() => SaveData();

        private void ClearUpData()
        {           


            SaveData();
        }

        #endregion

        #region Hooks

        private void CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            crate.hackSeconds = 0;
        }

        #endregion
    }
}

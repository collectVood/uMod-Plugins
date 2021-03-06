﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Customizable Friendly Fire", "collect_vood", "1.0.2")]
    [Description("Gives you the ability to enable or disable friendly fire player based")]
    class CustomizableFriendlyFire : CovalencePlugin
    {
        [PluginReference]
        private Plugin Friends;

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "NoFriendlyFire", "You cannot damage your friends! (<color=#7FFF00>/ff on</color>)" },
                { "OtherNoFriendlyFire", "{0} has friendly fire disabled!" },
                { "FriendAttack", "Your friend {0} tried to attack you!"},
                { "FFOn", "Friendly Fire turned <color=#7FFF00>on</color>!" },
                { "FFOff", "Friendly Fire turned <color=#FF0000>off</color>!" },
                { "AlreadyState", "Friendly Fire is already turned {0}!" },
                { "FFHelp", "Friendly Fire:\n/ff on - to turn on friendly fire\n/ff off - to turn off friendly fire" },
                { "CommandArguments", "You have to use <color=#7FFF00>on</color> or <color=#FF0000>off</color> as arguments!" }
            }, this);
        }
        #endregion

        #region Config       
        private Configuration config;
        private class Configuration
        {
            [JsonProperty(PropertyName = "Command")]
            public string Command = "ff";
            [JsonProperty(PropertyName = "Player Default Settings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, bool> PlayerDefaultSettings = new Dictionary<string, bool>
            {
                { "Friendly Fire", false }
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
        private Dictionary<string, PlayerSettings> allPlayerSettings => storedData.AllPlayerSettings;

        private class PlayerSettings
        {
            public bool ff;
        }
        private class StoredData
        {
            public Dictionary<string, PlayerSettings> AllPlayerSettings { get; private set; } = new Dictionary<string, PlayerSettings>();
        }
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        private void OnServerSave() => SaveData();
        private void Unload() => SaveData();

        private void CreatePlayerSettings(IPlayer player)
        {
            if (!allPlayerSettings.ContainsKey(player.Id))
            {
                allPlayerSettings[player.Id] = new PlayerSettings
                {
                    ff = config.PlayerDefaultSettings["Friendly Fire"],
                };
            }
        }
        #endregion

        #region Hooks
        private void Init()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);

            foreach (var player in BasePlayer.activePlayerList)
                CreatePlayerSettings(player.IPlayer);
            SaveData();

            AddCovalenceCommand(config.Command, nameof(CommandFriendlyFire));
        }
        private void Loaded()
        {
            if (Friends == null || !Friends.IsLoaded)
                PrintWarning("You are missing the Friends API plugin (required plugin)");
        }
        object OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (info == null || info.InitiatorPlayer == null || player == null)
                return null;
            IPlayer attacker = info.InitiatorPlayer.IPlayer;
            if (attacker == player.IPlayer)
                return null;
            if (!allPlayerSettings.ContainsKey(attacker.Id))
                CreatePlayerSettings(attacker);
            if (!allPlayerSettings.ContainsKey(player.UserIDString))
                CreatePlayerSettings(player.IPlayer);
            if (!allPlayerSettings[attacker.Id].ff || !allPlayerSettings[player.UserIDString].ff)
            {
                if (Friends == null || !Friends.IsLoaded)
                {
                    PrintWarning("You are missing the Friends API plugin (required plugin)");
                    return null;
                }
                if ((bool)Friends?.Call("AreFriendsS", attacker.Id, player.UserIDString))
                {
                    if (!allPlayerSettings[attacker.Id].ff)
                    {
                        attacker.Reply(GetMessage("NoFriendlyFire", attacker));
                    }
                    else
                    {
                        attacker.Reply(GetMessage("OtherNoFriendlyFire", attacker, player.IPlayer.Name));
                    }
                    player.ChatMessage(GetMessage("FriendAttack", player.IPlayer, attacker.Name));

                    return true;
                }
            }
            return null;
        }
        private void OnPlayerInit(BasePlayer player) { CreatePlayerSettings(player.IPlayer); }
        #endregion

        #region Command
        private void CommandFriendlyFire(IPlayer player, string command, string[] args)
        {
            if (args.Length <= 0)
            {
                player.Reply(GetMessage("CommandArguments", player));
                return;
            }
            if (!allPlayerSettings.ContainsKey(player.Id))
                CreatePlayerSettings(player);
            switch (args[0])
            {
                case "on":
                    if (allPlayerSettings[player.Id].ff == true)
                    {
                        player.Reply(GetMessage("AlreadyState", player, "<color=#7FFF00>on</color>"));
                        break;
                    }
                    allPlayerSettings[player.Id].ff = true;
                    player.Reply(GetMessage("FFOn", player));
                    break;
                case "off":
                    if (allPlayerSettings[player.Id].ff == false)
                    {
                        player.Reply(GetMessage("AlreadyState", player, "<color=#FF0000>off</color>"));
                        break;
                    }
                    allPlayerSettings[player.Id].ff = false;
                    player.Reply(GetMessage("FFOff", player));
                    break;
                case "help":
                    player.Reply(GetMessage("FFHelp", player));
                    break;
                default:
                    player.Reply(GetMessage("CommandArguments", player));
                    break;
            }
        }
        #endregion

        #region Helpers
        string GetMessage(string key, IPlayer player, params string[] args) => String.Format(lang.GetMessage(key, this, player.Id), args);
        #endregion
    }
}

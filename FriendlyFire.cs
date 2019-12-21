using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Friendly Fire", "collect_vood", "1.1.2")]
    [Description("Gives you the ability to enable or disable friendly fire player based")]
    class FriendlyFire : CovalencePlugin
    {
        [PluginReference]
        private Plugin Friends, Clans;

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "NoFriendlyFire", "You cannot damage your friends! (<color=#7FFF00>/ff on</color>)" },
                { "OtherNoFriendlyFire", "{0} has friendly fire disabled!" },
                { "FriendAttack", "Your friend {0} tried to attack you!"},
                { "FFOn", "Friendly Fire turned <color=#7FFF00>on</color>!" },
                { "FFOff", "Friendly Fire turned <color=#FF0000>off</color>!" },
                { "AlreadyStateOn", "Friendly Fire is already turned <color=#7FFF00>on</color>!" },
                { "AlreadyStateOff", "Friendly Fire is already turned <color=#FF0000>off</color>!" },
                { "FFHelp", "Friendly Fire:\n/ff on - to turn on friendly fire\n/ff off - to turn off friendly fire" },
                { "CommandArguments", "You have to use <color=#7FFF00>on</color> or <color=#FF0000>off</color> as arguments!" },
                { "NoPermission", "You don't have access to use this command"}
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
            [JsonProperty(PropertyName = "Change friendly fire state permission")]
            public string ChangeStatePermission = "friendlyfire.changestate";
            [JsonProperty(PropertyName = "Send friendly fire messages")]
            public bool SendMessages = true;
            [JsonProperty(PropertyName = "Include check if friend")]
            public bool isFriendCheck = true;
            [JsonProperty(PropertyName = "Include check if team member")]
            public bool isTeamMemberCheck = true;
            [JsonProperty(PropertyName = "Include check if clan member")]
            public bool isClanMemberCheck = true;
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

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
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
            permission.RegisterPermission(config.ChangeStatePermission, this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            AddCovalenceCommand(config.Command, nameof(CommandFriendlyFire));        
        }
        private void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
                CreatePlayerSettings(player.IPlayer);
            SaveData();
        }
        object OnPlayerAttack(BasePlayer player, HitInfo info)
        {
            if (info?.HitEntity == null || player == null)
                return null;
            IPlayer attacker = player.IPlayer;
            if (!(info.HitEntity is BasePlayer))
                return null;
            BasePlayer victimBP = info.HitEntity as BasePlayer;
            IPlayer victim = victimBP.IPlayer;
            if (attacker == null || victim == null || attacker.Id == victim.Id)
                return null;
            CreatePlayerSettings(attacker);
            CreatePlayerSettings(victim);
            if (!allPlayerSettings[attacker.Id].ff || !allPlayerSettings[victim.Id].ff)
            {
                if ((config.isTeamMemberCheck && IsTeamMember(player, victimBP)) || (config.isFriendCheck && (Friends?.Call<bool>("AreFriends", attacker.Id, victim.Id) ?? false)) || (config.isClanMemberCheck && IsClanMember(player, victimBP)))
                {
                    if (config.SendMessages)
                    {
                        if (!allPlayerSettings[attacker.Id].ff)
                        {
                            attacker.Reply(GetMessage("NoFriendlyFire", attacker));
                        }
                        else
                        {
                            attacker.Reply(GetMessage("OtherNoFriendlyFire", attacker, victim.Name));
                        }
                        victim.Reply(GetMessage("FriendAttack", victim, attacker.Name));
                    }
                    Interface.Oxide.CallHook("OnFriendAttacked", attacker, victim, info);
                    return true;
                }
            }
            return null;
        }
        private void OnUserConnected(IPlayer player) { CreatePlayerSettings(player); }
        #endregion

        #region Command
        private void CommandFriendlyFire(IPlayer player, string command, string[] args)
        {
            if (!HasPermission(player))
            {
                player.Reply(GetMessage("NoPermission", player));
                return;
            }
            CreatePlayerSettings(player);
            if (args.Length <= 0)
            {
                if (allPlayerSettings[player.Id].ff == true)
                {
                    allPlayerSettings[player.Id].ff = false;
                    player.Reply(GetMessage("FFOff", player));
                }
                else
                {
                    allPlayerSettings[player.Id].ff = true;
                    player.Reply(GetMessage("FFOn", player));
                }
                return;
            }
            switch (args[0].ToLower())
            {
                case "on":
                    if (allPlayerSettings[player.Id].ff == true)
                    {
                        player.Reply(GetMessage("AlreadyStateOn", player));
                        break;
                    }
                    allPlayerSettings[player.Id].ff = true;
                    player.Reply(GetMessage("FFOn", player));
                    break;
                case "off":
                    if (allPlayerSettings[player.Id].ff == false)
                    {
                        player.Reply(GetMessage("AlreadyStateOff", player));
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
        bool HasPermission(IPlayer player) => permission.UserHasPermission(player.Id, config.ChangeStatePermission);
        bool IsTeamMember(BasePlayer player, BasePlayer possibleMember)
        {
            if (player.currentTeam == 0 || possibleMember.currentTeam == 0)
                return false;
            return player.currentTeam == possibleMember.currentTeam;
        }
        bool IsClanMember(BasePlayer player, BasePlayer possibleMember)
        {
            if (Clans == null || !Clans.IsLoaded)
                return false;

            var playerClan = Clans.Call<string>("GetClanOf", player);
            var otherPlayerClan = Clans.Call<string>("GetClanOf", possibleMember);
            if (String.IsNullOrEmpty(otherPlayerClan) || String.IsNullOrEmpty(playerClan))
            {
                return false;
            }
            return playerClan == otherPlayerClan;
        }
        #endregion
    }
}

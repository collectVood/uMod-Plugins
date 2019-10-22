using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Coloured Names", "collect_vood", "2.0.0")]
    [Description("Allows players to change their name colour in chat")]
    class ColouredNamesRe : CovalencePlugin
    {
        [PluginReference]
        private Plugin BetterChat;

        #region Constants
        const string colourRegex = "^#(?:[0-9a-fA-f]{3}){1,2}$";
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "NoPermission", "You don't have permission to use this." },
                { "NoPermissionSetOthers", "You don't have permission to set other players' colours." },
                { "IncorrectUsage", "<color=#00AAFF>Incorrect usage!</color><color=#A8A8A8> /colour {{colour}} [player]\nFor a list of colours do /colours</color>" },
                { "PlayerNotFound", "Player {0} was not found." },
                { "SizeBlocked", "You may not try and change your size! You sneaky player..." },
                { "InvalidCharacters", "The character '{0}' is not allowed in colours. Please remove it." },
                { "ColourBlocked", "<color=#00AAFF>ColouredNames: </color><color=#A8A8A8>That colour is blocked.</color>" },
                { "ColourNotWhitelisted", "<color=#00AAFF>ColouredNames: </color><color=#A8A8A8>That colour is not whitelisted.</color>"},
                { "ColourRemoved", "<color=#00AAFF>ColouredNames: </color><color=#A8A8A8>Name colour removed!</color>" },
                { "ColourChanged", "<color=#00AAFF>ColouredNames: </color><color=#A8A8A8>Name colour changed to </color><color={0}>{0}</color><color=#A8A8A8>!</color>" },
                { "ColourChangedFor", "<color=#00AAFF>ColouredNames: </color><color=#A8A8A8>{0}'s name colour changed to </color><color={1}>{1}</color><color=#A8A8A8>!</color>" },
                { "LogInfo", "[CHAT] {0}[{1}/{2}] : {3}" },
                { "ColoursInfo", "<color=#00AAFF>ColouredNames</color><color=#A8A8A8>\nYou can only use hexcode, eg '</color><color=#FFFF00>#FFFF00</color><color=#A8A8A8>'\nTo remove your colour, use 'clear' or 'remove'\nAn invalid colour will default to </color>white<color=#A8A8A8></color>" },
                { "CantUseClientside", "You may not use this command from ingame - server cosole only." },
                { "ConsoleColourIncorrectUsage", "Incorrect usage! colour {{partialNameOrUserid}} {{colour}}" },
                { "ConsoleColourChanged", "Colour of {0} changed to {1}." },
                { "InvalidColour", "That colour is not valid. Do /colours for more information on valid colours." }
            }, this);
        }
        #endregion

        #region Config       
        private Configuration config;
        private class Configuration
        {
            [JsonProperty(PropertyName = "Colour Command")]
            public string ColourCommand = "colour";
            [JsonProperty(PropertyName = "Colours Command (Help)")]
            public string ColoursCommand = "colours";
            [JsonProperty(PropertyName = "Use permission")]
            public string permUse = "colourednames.use";
            [JsonProperty(PropertyName = "Bypass restrictions permission")]
            public string permBypass = "colourednames.bypass";
            [JsonProperty(PropertyName = "Set others colour permission")]
            public string permSetOthers = "colourednames.bypass";
            [JsonProperty(PropertyName = "Use Blacklist")]
            public bool UseBlacklist = true;
            [JsonProperty(PropertyName = "Blocked Characters")]
            public string[] blockedValues = { "{", "}", "size" };
            [JsonProperty(PropertyName = "Blocked Colours Hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> blockColoursHex = new List<string>
            {
                { "#000000" }
            };
            [JsonProperty(PropertyName = "Use Whitelist")]
            public bool UseWhitelist = false;
            [JsonProperty(PropertyName = "Whitelisted Colours Hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> whitelistedColoursHex = new List<string>
            {
                { "#000000" }
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
        private Dictionary<string, string> allColourData => storedData.AllColourData;

        private class StoredData
        {
            public Dictionary<string, string> AllColourData { get; private set; } = new Dictionary<string, string>();
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        private void OnServerSave() => SaveData();
        private void Unload() => SaveData();

        private void ChangeColour(IPlayer target, string newColour)
        {
            if (!allColourData.ContainsKey(target.Id)) allColourData.Add(target.Id, "");
            allColourData[target.Id] = newColour;
        }
        #endregion

        #region Hooks
        private void Init()
        {
            permission.RegisterPermission(config.permUse, this);
            permission.RegisterPermission(config.permBypass, this);
            permission.RegisterPermission(config.permSetOthers, this);

            AddCovalenceCommand(config.ColourCommand, nameof(ColourCommand));
            AddCovalenceCommand(config.ColoursCommand, nameof(ColoursCommand));

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        }

        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (BetterChatIns()) return null;
            IPlayer player = covalence.Players.FindPlayerById(arg.Connection.userid.ToString());
            if (player == null) return null;

            if (!allColourData.ContainsKey(player.Id)) return null;

            string argMsg = arg.GetString(0, "text");
            server.Broadcast(argMsg, $"<color={allColourData[player.Id]}>{player.Name}</color>", player.Id);
         
            Interface.Oxide.LogInfo(GetMessage("LogInfo", player, player.Name, (arg.Connection.player as BasePlayer).net.ID.ToString(), player.Id, argMsg));
            return true;
        }

        Dictionary<string, object> OnBetterChat(Dictionary<string, object> dict)
        {
            string Id = (dict["Player"] as IPlayer).Id;
            if (!allColourData.ContainsKey(Id)) return dict;
            ((Dictionary<string, object>)dict["UsernameSettings"])["Color"] = allColourData[Id];
            return dict;
        }
        #endregion

        #region Commands
        void ColourCommand(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer)
            {
                if (args.Length < 2)
                {
                    player.Reply(GetMessage("ConsoleColourIncorrectUsage", player));
                    return;
                }
                IPlayer target = covalence.Players.FindPlayer(args[0]);
                if (target == null)
                {
                    player.Reply(GetMessage("PlayerNotFound", player, args[0]));
                    return;
                }
                ChangeColour(target, args[1]);
                player.Reply(GetMessage("ConsoleColourChanged", player, target.Name, args[1]));
                return;
            }
            if (!HasPerm(player))
            {
                player.Reply(GetMessage("NoPermission", player));
                return;
            }
            if (args.Length == 0)
            {
                player.Reply(GetMessage("IncorrectUsage", player));
                return;
            }
            string colLower = args[0].ToLower();

            if (colLower == "clear" || colLower == "remove")
            {
                allColourData.Remove(player.Id);
                SaveData();
                player.Reply(GetMessage("ColourRemoved", player));
                return;
            }
            string invalidChar;
            if ((invalidChar = IsInvalidCharacter(colLower)) != null)
            {
                player.Reply(GetMessage("InvalidCharacters", player, invalidChar));
                return;
            }
            if (!IsValid(colLower) && !CanBypass(player))
            {
                if (config.UseBlacklist)
                    player.Reply(GetMessage("ColourBlocked", player));
                else if (config.UseWhitelist)
                    player.Reply(GetMessage("ColourNotWhitelisted", player));
                return;
            }
            if (!IsValidColour(args[0]))
            {
                player.Reply(GetMessage("InvalidColour", player));
                return;
            }
            if (args.Length > 1)
            {
                //Setting for other person area
                if (!CanSetOthers(player))
                {
                    player.Reply(GetMessage("NoPermissionSetOthers", player));
                    return;
                }

                IPlayer target = covalence.Players.FindPlayerById(args[1]);
                if (target == null)
                {
                    player.Reply(GetMessage("PlayerNotFound", player, args[1]));
                    return;
                }

                ChangeColour(target, args[0]);
                player.Reply(GetMessage("ColourChangedFor", player, target.Name, args[0]));
                return;
            }

            ChangeColour(player, args[0]);
            player.Reply(GetMessage("ColourChanged", player, args[0]));
        }
        void ColoursCommand(IPlayer player, string cmd, string[] args)
        {
            if (!HasPerm(player))
            {
                player.Reply(GetMessage("NoPermission", player));
                return;
            }
            player.Reply(GetMessage("ColoursInfo", player));
        }
        #endregion

        #region Helpers
        bool BetterChatIns() => (BetterChat != null);
        bool IsValidColour(string input) => Regex.Match(input, colourRegex).Success;

        string GetMessage(string key, IPlayer player, params string[] args) => String.Format(lang.GetMessage(key, this, player.Id), args);
       
        bool HasPerm(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.permUse));
        bool CanBypass(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.permBypass));
        bool CanSetOthers(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.permSetOthers));

        bool IsValid(string input)
        {
            if (config.UseBlacklist)
            {
                return !config.blockColoursHex.Any(x => (input == x));
            }
            else if (config.UseWhitelist)
            {
                return config.whitelistedColoursHex.Any(x => (input == x));
            }
            return true;
        }
        string IsInvalidCharacter(string input) => (config.blockedValues.Where(x => input.Contains(x)).FirstOrDefault()) ?? null;
        #endregion
    }
}

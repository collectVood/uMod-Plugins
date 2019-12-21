using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using ConVar;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Coloured Names", "collect_vood", "1.3.13")]
    [Description("Allows players to change their name colour in chat")]
    class ColouredNames : CovalencePlugin
    {
        [PluginReference]
        private Plugin BetterChat, BetterChatMute;

        #region Constants
        const string colourRegex = "^#(?:[0-9a-fA-f]{3}){1,2}$";
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "NoPermission", "You don't have permission to use this command." },
                { "NoPermissionSetOthers", "You don't have permission to set other players colours." },
                { "IncorrectUsage", "<color=#00AAFF>Incorrect usage!</color><color=#A8A8A8> /colour {{colour}} [player]\nFor a list of colours do /colours</color>" },
                { "PlayerNotFound", "Player <color=#00AAFF>{0}</color> was not found." },
                { "SizeBlocked", "You may not try and change your size! You sneaky player..." },
                { "InvalidCharacters", "The character '{0}' is not allowed in colours. Please remove it." },
                { "ColourBlocked", "<color=#00AAFF>ColouredNames: </color><color=#A8A8A8>That colour is blocked.</color>" },
                { "ColourNotWhitelisted", "<color=#00AAFF>ColouredNames: </color><color=#A8A8A8>That colour is not whitelisted.</color>"},
                { "ColourRemoved", "<color=#00AAFF>ColouredNames: </color><color=#A8A8A8>Name colour removed!</color>" },
                { "ColourChanged", "<color=#00AAFF>ColouredNames: </color><color=#A8A8A8>Name colour changed to </color><color={0}>{0}</color><color=#A8A8A8>!</color>" },
                { "ColourChangedFor", "<color=#00AAFF>ColouredNames: </color><color=#A8A8A8>{0}'s name colour changed to </color><color={1}>{1}</color><color=#A8A8A8>!</color>" },
                { "ColoursInfo", "<color=#00AAFF>ColouredNames</color><color=#A8A8A8>\nYou can only use hexcode, eg '</color><color=#FFFF00>#FFFF00</color><color=#A8A8A8>'\nTo remove your colour, use 'clear', 'reset' or 'remove'\nAn invalid colour will default to </color>white<color=#A8A8A8></color>" },
                { "ConsoleColourIncorrectUsage", "Incorrect usage! colour {{colour}} {{partialNameOrUserid}}" },
                { "ConsoleColourChanged", "Colour of {0} changed to {1}." },
                { "InvalidColour", "That colour is not valid. Do /colours for more information on valid colours." },
                { "RndColour", "Colour was randomized to <color={0}>{0}</color>" },
                { "ConsoleRndColour", "Colour of {0} randomized to {1}."},
                { "RconLogFormat", "{0}[{1}]"}
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
            [JsonProperty(PropertyName = "Block messages of muted players (requires BetterChatMute)")]
            public bool blockChatMute = true;
            [JsonProperty(PropertyName = "Use permission")]
            public string permUse = "colourednames.use";
            [JsonProperty(PropertyName = "Bypass restrictions permission")]
            public string permBypass = "colourednames.bypass";
            [JsonProperty(PropertyName = "Set others colour permission")]
            public string permSetOthers = "colourednames.setothers";
            [JsonProperty(PropertyName = "Get random colour permission")]
            public string permRandomColour = "colourednames.rndcolour";
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
            permission.RegisterPermission(config.permRandomColour, this);

            AddCovalenceCommand(config.ColourCommand, nameof(cmdColourCommand));
            AddCovalenceCommand(config.ColoursCommand, nameof(cmdColoursCommand));

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        }

        private object OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (BetterChatIns()) return null;
            if (config.blockChatMute && BetterChatMuteIns()) if (BetterChatMute.Call<bool>("API_IsMuted", player.IPlayer)) return null;
            if (player == null) return null;            
            if (!allColourData.ContainsKey(player.UserIDString) || !HasPerm(player.IPlayer)) return null;

            if (Chat.serverlog)
            {
                object[] objArray = new object[] { ConsoleColor.DarkYellow, null, null, null };
                objArray[1] = string.Concat(new object[] { "[", channel, "] ", player.displayName.EscapeRichText(), ": " });
                objArray[2] = ConsoleColor.DarkGreen;
                objArray[3] = message;
                ServerConsole.PrintColoured(objArray);
            }
            string formattedMsg = GetMessage("RconLogFormat", player.IPlayer, player.displayName.EscapeRichText(), player.UserIDString);

            if (channel == Chat.ChatChannel.Global)
            {
                foreach (BasePlayer Player in BasePlayer.activePlayerList)
                {
                    Player.SendConsoleCommand("chat.add2", 0, player.userID, message, player.displayName, allColourData[player.UserIDString]);
                }
                DebugEx.Log(string.Concat("[CHAT] ", formattedMsg, " : ", message), StackTraceLogType.None);
            }
            else if (channel == Chat.ChatChannel.Team)
            {
                foreach (ulong memberId in player.Team.members)
                {
                    BasePlayer member;
                    if ((member = BasePlayer.FindByID(memberId)) != null)
                    {
                        member.SendConsoleCommand("chat.add2", 1, player.userID, message, player.displayName, allColourData[player.UserIDString]);
                    }
                }
                DebugEx.Log(string.Concat("[TEAM CHAT] ", formattedMsg, " : ", message), StackTraceLogType.None);
            }
            else return null;

            Chat.ChatEntry chatentry = new Chat.ChatEntry
            {
                Channel = channel,
                Message = message,
                UserId = player.UserIDString,
                Username = player.displayName,
                Color = allColourData[player.UserIDString],
                Time = Facepunch.Math.Epoch.Current
            };
            Facepunch.RCon.Broadcast(Facepunch.RCon.LogType.Chat, chatentry);          
            return true;
        }

        Dictionary<string, object> OnBetterChat(Dictionary<string, object> dict)
        {
            if (dict != null)
            {
                string Id = (dict["Player"] as IPlayer).Id;
                if (!allColourData.ContainsKey(Id)) return dict;
                ((Dictionary<string, object>)dict["UsernameSettings"])["Color"] = allColourData[Id];
            }
            return dict;
        }
        #endregion

        #region Commands
        void cmdColourCommand(IPlayer player, string cmd, string[] args)
        {
            string colLower = string.Empty;
            if (player.IsServer)
            {
                if (args.Length < 2)
                {
                    player.Reply(GetMessage("ConsoleColourIncorrectUsage", player));
                    return;
                }
                IPlayer target = covalence.Players.FindPlayer(args[1]);
                if (target == null)
                {
                    player.Reply(GetMessage("PlayerNotFound", player, args[1]));
                    return;
                }
                colLower = args[0].ToLower();
                if (!ProcessColour(target, colLower, player)) return;

                ChangeColour(target, args[0]);
                player.Reply(GetMessage("ConsoleColourChanged", player, target.Name, args[0]));
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
            colLower = args[0].ToLower();
            if (!ProcessColour(player, colLower)) return;

            if (!IsValid(colLower) && !CanBypass(player))
            {
                if (config.UseBlacklist) player.Reply(GetMessage("ColourBlocked", player));
                else if (config.UseWhitelist) player.Reply(GetMessage("ColourNotWhitelisted", player));
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
        void cmdColoursCommand(IPlayer player, string cmd, string[] args)
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
        bool BetterChatIns() => (BetterChat != null && BetterChat.IsLoaded);
        bool BetterChatMuteIns() => (BetterChatMute != null && BetterChatMute.IsLoaded);
        bool IsValidColour(string input) => Regex.Match(input, colourRegex).Success;

        string GetMessage(string key, IPlayer player, params string[] args) => String.Format(lang.GetMessage(key, this, player.Id), args);
        //player.IsAdmin || 
        bool HasPerm(IPlayer player) => (permission.UserHasPermission(player.Id, config.permUse));
        bool CanBypass(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.permBypass));
        bool CanSetOthers(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.permSetOthers));
        bool CanRandomColour(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.permRandomColour));

        bool IsValid(string input)
        {
            if (config.UseBlacklist) return !config.blockColoursHex.Any(x => (input == x));
            else if (config.UseWhitelist) return config.whitelistedColoursHex.Any(x => (input == x));
            return true;
        }
        bool ProcessColour(IPlayer player, string colLower, IPlayer serverPlayer = null)
        {
            if (colLower == "reset" || colLower == "clear" || colLower == "remove")
            {
                allColourData.Remove(player.Id);
                SaveData();
                if (serverPlayer != null) serverPlayer.Reply(GetMessage("ColourRemoved", serverPlayer));
                player.Reply(GetMessage("ColourRemoved", player));
                return false;
            }
            if (colLower == "random" && (CanRandomColour(player) || serverPlayer != null))
            {
                colLower = GetRndColour();
                ChangeColour(player, colLower);
                player.Reply(GetMessage("RndColour", player, colLower));
                if (serverPlayer != null) serverPlayer.Reply(GetMessage("ConsoleRndColour", serverPlayer, player.Name, colLower));
                return false;
            }
            string invalidChar;
            if ((invalidChar = IsInvalidCharacter(colLower)) != null)
            {
                if (serverPlayer != null)
                {
                    serverPlayer.Reply(GetMessage("InvalidCharacters", serverPlayer, invalidChar));
                    return false;
                }
                player.Reply(GetMessage("InvalidCharacters", player, invalidChar));
                return false;
            }
            if (!IsValidColour(colLower))
            {
                if (serverPlayer != null)
                {
                    serverPlayer.Reply(GetMessage("InvalidColour", serverPlayer));
                    return false;
                }
                player.Reply(GetMessage("InvalidColour", player));
                return false;
            }
            return true;
        }
        string GetRndColour() => String.Format("#{0:X6}", new System.Random().Next(0x1000000));
        string IsInvalidCharacter(string input) => (config.blockedValues.Where(x => input.Contains(x)).FirstOrDefault()) ?? null;
        #endregion
    }
}

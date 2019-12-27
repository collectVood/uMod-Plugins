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
    [Info("Coloured Chat", "collect_vood", "2.0.0")]
    [Description("Allows players to change their name & message colour in chat")]
    class ColouredChat : CovalencePlugin
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
                { "NoPermissionSetOthers", "You don't have permission to set other players {0} colours." },
                { "NoPermissionGradient", "You don't have permission to use {0} gradients." },
                { "IncorrectGradientUsage", "Incorrect usage! To use gradients please use /{0} gradient hexCode1 hexCode2 ...</color>" },
                { "IncorrectGradientUsageArgs", "Incorrect usage! A gradient requires at least two different colours!"},
                { "GradientChanged", "{0} gradient changed to {1}!"},
                { "GradientChangedFor", "{0}'s gradient {1} colour changed to {2}!"},
                { "IncorrectUsage", "Incorrect usage! /{0} <colour>\nFor a list of colours do /colours" },
                { "IncorrectSetUsage", "Incorrect set usage! /{0} set <playerIdOrName> <colourOrColourArgument>\nFor a list of colours do /colours" },
                { "PlayerNotFound", "Player {0} was not found." },
                { "InvalidCharacters", "The character '{0}' is not allowed in colours. Please remove it." },
                { "ColourRemoved", "{0} colour removed!" },
                { "ColourRemovedFor", "{0}'s {1} colour was removed!" },
                { "ColourChanged", "{0} colour changed to <color={1}>{1}</color>!" },
                { "ColourChangedFor", "{0}'s {1} colour changed to <color={2}>{2}</color>!" },
                { "ColoursInfo", "You can only use hexcode, eg '<color=#FFFF00>#FFFF00</color>'\nTo remove your colour, use 'clear', 'reset' or 'remove'\n\n{0}" },
                { "InvalidColour", "That colour is not valid. Do /colours for more information on valid colours." },
                { "RndColour", "{0} colour was randomized to <color={1}>{1}</color>" },
                { "RndColourFor", "{0} colour of {1} randomized to {2}."},
            }, this);
        }

        #endregion

        #region Config     

        private Configuration config;
        private class Configuration
        {
            //General
            [JsonProperty(PropertyName = "Player Inactivity Data Removal (days)")]
            public int inactivityRemovalTime = 7;
            [JsonProperty(PropertyName = "Block messages of muted players (requires BetterChatMute)")]
            public bool blockChatMute = true;
            [JsonProperty(PropertyName = "Rainbow Colours")]
            public string[] rainbowColours = { "#ff0000", "#ffa500", "#ffff00", "#008000", "#0000ff", "#4b0082", "#ee82ee" };
            [JsonProperty(PropertyName = "Blocked Characters")]
            public string[] blockedValues = { "{", "}", "size" };

            //Name
            [JsonProperty(PropertyName = "Name colour command")]
            public string nameColourCommand = "colour";
            [JsonProperty(PropertyName = "Name colours command (Help)")]
            public string nameColoursCommand = "colours";
            [JsonProperty(PropertyName = "Name show colour permission")]
            public string namePermShow = "colouredchat.nameshow";
            [JsonProperty(PropertyName = "Name show use permission")]
            public string namePermUse = "colouredchat.nameuse";
            [JsonProperty(PropertyName = "Name use gradient permission")]
            public string namePermGradient = "colouredchat.namegradient";
            [JsonProperty(PropertyName = "Name default Rainbow Name Permission")]
            public string namePermRainbow = "colouredchat.namerainbow";
            [JsonProperty(PropertyName = "Name bypass restrictions permission")]
            public string namePermBypass = "colouredchat.namebypass";
            [JsonProperty(PropertyName = "Name set others colour permission")]
            public string namePermSetOthers = "colouredchat.namesetothers";
            [JsonProperty(PropertyName = "Name get random colour permission")]
            public string namePermRandomColour = "colouredchat.namerndcolour";
            [JsonProperty(PropertyName = "Name use blacklist")]
            public bool nameUseBlacklist = true;
            [JsonProperty(PropertyName = "Name blocked colours hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> nameBlockColoursHex = new List<string>
            {
                { "#000000" }
            };
            [JsonProperty(PropertyName = "Name use whitelist")]
            public bool nameUseWhitelist = false;
            [JsonProperty(PropertyName = "Name whitelisted colours hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> nameWhitelistedColoursHex = new List<string>
            {
                { "#000000" }
            };

            //Message
            [JsonProperty(PropertyName = "Message colour command")]
            public string messageColourCommand = "mcolour";
            [JsonProperty(PropertyName = "Message colours command (Help)")]
            public string messageColoursCommand = "mcolours";
            [JsonProperty(PropertyName = "Message show colour permission")]
            public string messagePermShow = "colouredchat.messageshow";
            [JsonProperty(PropertyName = "Message use permission")]
            public string messagePermUse = "colouredchat.messageuse";
            [JsonProperty(PropertyName = "Message use gradient permission")]
            public string messagePermGradient = "colouredchat.messagegradient";
            [JsonProperty(PropertyName = "Message default Rainbow Name Permission")]
            public string messagePermRainbow = "colouredchat.messagerainbow";
            [JsonProperty(PropertyName = "Message bypass restrictions permission")]
            public string messagePermBypass = "colouredchat.messagebypass";
            [JsonProperty(PropertyName = "Message set others colour permission")]
            public string messagePermSetOthers = "colouredchat.messagesetothers";
            [JsonProperty(PropertyName = "Message get random colour permission")]
            public string messagePermRandomColour = "colouredchat.messagerndcolour";
            [JsonProperty(PropertyName = "Message use blacklist")]
            public bool messageUseBlacklist = true;
            [JsonProperty(PropertyName = "Message blocked Colours Hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> messageBlockColoursHex = new List<string>
            {
                { "#000000" }
            };
            [JsonProperty(PropertyName = "Message use whitelist")]
            public bool messageUseWhitelist = false;
            [JsonProperty(PropertyName = "Message whitelisted colours hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> messageWhitelistedColoursHex = new List<string>
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

        private Dictionary<string, string> cachedNames = new Dictionary<string, string>();
        private StoredData storedData;
        private Dictionary<string, PlayerData> allColourData => storedData.AllColourData;

        private class StoredData
        {
            public Dictionary<string, PlayerData> AllColourData { get; private set; } = new Dictionary<string, PlayerData>();
        }

        public class PlayerData
        {
            [JsonProperty("Name Colour")]
            public string NameColour;
            [JsonProperty("Name Gradient Args")]
            public string[] NameGradientArgs;

            [JsonProperty("Message Colour")]
            public string MessageColour;
            [JsonProperty("Message Gradient Args")]
            public string[] MessageGradientArgs;

            [JsonProperty("Last active")]
            public long LastActive;

            public PlayerData(string nameColour = "", string[] nameGradientArgs = null, string messageColour = "", string[] messageGradientArgs = null)
            {
                NameColour = nameColour;
                NameGradientArgs = nameGradientArgs;

                MessageColour = messageColour;
                MessageGradientArgs = messageGradientArgs;

                LastActive = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

        private void OnServerSave()
        {
            ClearUpData();
            SaveData();
        } 
        private void Unload() => SaveData();

        private void ChangeNameColour(IPlayer target, string colour, string[] colourArgs)
        {
            var playerData = new PlayerData(colour, colourArgs);
            if (!allColourData.ContainsKey(target.Id)) allColourData.Add(target.Id, playerData);

            allColourData[target.Id].NameColour = colour;
            allColourData[target.Id].NameGradientArgs = colourArgs;
        }

        private void ChangeMessageColour(IPlayer target, string colour, string[] colourArgs)
        {
            var playerData = new PlayerData(string.Empty, null, colour, colourArgs);           
            if (!allColourData.ContainsKey(target.Id)) allColourData.Add(target.Id, playerData);

            allColourData[target.Id].MessageColour = colour;
            allColourData[target.Id].MessageGradientArgs = colourArgs;
        }

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(config.namePermShow, this);
            permission.RegisterPermission(config.messagePermShow, this);
            permission.RegisterPermission(config.namePermRainbow, this);
            permission.RegisterPermission(config.messagePermRainbow, this);
            permission.RegisterPermission(config.namePermGradient, this);
            permission.RegisterPermission(config.messagePermGradient, this);
            permission.RegisterPermission(config.namePermUse, this);
            permission.RegisterPermission(config.messagePermUse, this);
            permission.RegisterPermission(config.namePermBypass, this);
            permission.RegisterPermission(config.messagePermBypass, this);
            permission.RegisterPermission(config.namePermSetOthers, this);
            permission.RegisterPermission(config.messagePermSetOthers, this);
            permission.RegisterPermission(config.namePermRandomColour, this);
            permission.RegisterPermission(config.messagePermRandomColour, this);

            AddCovalenceCommand(config.nameColourCommand, nameof(cmdNameColour));
            AddCovalenceCommand(config.nameColoursCommand, nameof(cmdNameColours));
            AddCovalenceCommand(config.messageColourCommand, nameof(cmdMessageColour));
            AddCovalenceCommand(config.messageColoursCommand, nameof(cmdMessageColours));

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);

            ClearUpData();
        }

        void OnUserConnected(IPlayer player)
        {
            if (!allColourData.ContainsKey(player.Id)) return;
            allColourData[player.Id].LastActive = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        void OnUserDisconnected(IPlayer player)
        {
            if (!allColourData.ContainsKey(player.Id)) return;
            allColourData[player.Id].LastActive = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        object OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (BetterChatIns()) return null;
            if (config.blockChatMute && BetterChatMuteIns()) if (BetterChatMute.Call<bool>("API_IsMuted", player.IPlayer)) return null;
            if (player == null || !allColourData.ContainsKey(player.UserIDString)) return null;

            var playerData = new PlayerData();
            playerData = allColourData[player.UserIDString];

            //Rainbow Handling
            if (HasNameRainbow(player.IPlayer) && (string.IsNullOrEmpty(playerData.NameColour) && playerData.NameGradientArgs == null)) playerData.NameGradientArgs = config.rainbowColours;
            if (HasMessageRainbow(player.IPlayer) && (string.IsNullOrEmpty(playerData.MessageColour) && playerData.MessageGradientArgs == null)) playerData.MessageGradientArgs = config.rainbowColours;

            if (Chat.serverlog)
            {
                object[] objArray = new object[] { ConsoleColor.DarkYellow, null, null, null };
                objArray[1] = string.Concat(new object[] { "[", channel, "] ", player.displayName.EscapeRichText(), ": " });
                objArray[2] = ConsoleColor.DarkGreen;
                objArray[3] = message;
                ServerConsole.PrintColoured(objArray);
            }
            string formattedMsg = string.Format("{0}[{1}]", new string[] { player.displayName.EscapeRichText(), player.UserIDString });

            string playerUserName = player.displayName;
            string playerColour = player.IsAdmin ? "#af5" : "#5af";
            string playerMessage = message;

            //Gradient Handling & Caching
            if (playerData.NameGradientArgs != null && playerData.NameGradientArgs.Length != 0)
            {
                if (!cachedNames.ContainsKey(player.UserIDString)) cachedNames.Add(player.UserIDString, ProcessGradient(player.displayName, playerData.NameGradientArgs));
                playerUserName = cachedNames[player.UserIDString];
            }
            else if (!string.IsNullOrEmpty(playerData.NameColour)) playerColour = playerData.NameColour;

            if (playerData.MessageGradientArgs != null && playerData.MessageGradientArgs.Length != 0) playerMessage = ProcessGradient(message, playerData.MessageGradientArgs);
            else if (!string.IsNullOrEmpty(playerData.MessageColour)) playerMessage = ProcessColourMessage(message, playerData.MessageColour);

            var colouredChatMessage = new ColouredChatMessage(player.IPlayer, playerUserName, playerColour, playerMessage);
            var colouredChatMessageDict = colouredChatMessage.ToDictionary();

            foreach (Plugin plugin in plugins.GetAll())
            {
                object obj = Interface.Oxide.CallHook("OnColouredChat", colouredChatMessageDict);

                if (obj is Dictionary<string, object>)
                {
                    try
                    {
                        colouredChatMessageDict = obj as Dictionary<string, object>;
                    }
                    catch (Exception e)
                    {
                        PrintError($"Failed to load modified OnBetterChat hook data from plugin '{plugin.Title} ({plugin.Version})':{Environment.NewLine}{e}");
                        continue;
                    }
                }
                else if (obj != null) return null;
            }

            colouredChatMessage = ColouredChatMessage.FromDictionary(colouredChatMessageDict);

            if (channel == Chat.ChatChannel.Global)
            {
                foreach (BasePlayer Player in BasePlayer.activePlayerList)
                {
                    Player.SendConsoleCommand("chat.add2", 0, player.userID, colouredChatMessage.Message, colouredChatMessage.Name, colouredChatMessage.Colour);
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
                        member.SendConsoleCommand("chat.add2", 1, player.userID, colouredChatMessage.Message, colouredChatMessage.Name, colouredChatMessage.Colour);
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
                Color = allColourData[player.UserIDString].NameColour,
                Time = Facepunch.Math.Epoch.Current
            };
            Facepunch.RCon.Broadcast(Facepunch.RCon.LogType.Chat, chatentry);          
            return true;
        }

        Dictionary<string, object> OnBetterChat(Dictionary<string, object> dict)
        {
            if (dict != null)
            {
                IPlayer player = dict["Player"] as IPlayer;
                if (!allColourData.ContainsKey(player.Id)) return dict;

                var playerData = allColourData[player.Id];

                //Rainbow Handling
                if (HasNameRainbow(player) && (string.IsNullOrEmpty(playerData.NameColour) && playerData.NameGradientArgs == null)) playerData.NameGradientArgs = config.rainbowColours;
                if (HasMessageRainbow(player) && (string.IsNullOrEmpty(playerData.MessageColour) && playerData.MessageGradientArgs == null)) playerData.MessageGradientArgs = config.rainbowColours;

                //Name
                if (playerData.NameGradientArgs != null && playerData.NameGradientArgs.Length != 0)
                {
                    if (!cachedNames.ContainsKey(player.Id)) cachedNames.Add(player.Id, ProcessGradient(player.Name, playerData.NameGradientArgs));
                    dict["Username"] = cachedNames[player.Id];
                }
                else if (!string.IsNullOrEmpty(playerData.NameColour)) ((Dictionary<string, object>)dict["UsernameSettings"])["Color"] = allColourData[player.Id].NameColour;

                //Message
                string message = dict["Message"].ToString();
                if (playerData.MessageGradientArgs != null && playerData.MessageGradientArgs.Length != 0) dict["Message"] = ProcessGradient(message, playerData.MessageGradientArgs);
                else if (!string.IsNullOrEmpty(playerData.MessageColour)) dict["Message"] = ProcessColourMessage(message, playerData.MessageColour);
            }
            return dict;
        }

        #endregion

        #region Commands

        void cmdNameColour(IPlayer player, string cmd, string[] args) => ProcessColourCommand(player, cmd, args);

        void cmdNameColours(IPlayer player, string cmd, string[] args) => ProcessColoursCommand(player, cmd, args);

        void cmdMessageColour(IPlayer player, string cmd, string[] args) => ProcessColourCommand(player, cmd, args, true);

        void cmdMessageColours(IPlayer player, string cmd, string[] args) => ProcessColoursCommand(player, cmd, args, true);

        #endregion

        #region Helpers

        bool BetterChatIns() => (BetterChat != null && BetterChat.IsLoaded);
        bool BetterChatMuteIns() => (BetterChatMute != null && BetterChatMute.IsLoaded);
        bool IsValidColour(string input) => Regex.Match(input, colourRegex).Success;
        string GetMessage(string key, IPlayer player, params string[] args) => String.Format(lang.GetMessage(key, this, player.Id), args);

        //Name
        bool HasNameShowPerm(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.namePermShow));
        bool HasNamePerm(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.namePermUse));
        bool HasNameRainbow(IPlayer player) => (permission.UserHasPermission(player.Id, config.namePermRainbow));
        bool CanNameGradient(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.namePermGradient));
        bool CanNameBypass(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.namePermBypass));
        bool CanNameSetOthers(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.namePermSetOthers));
        bool CanNameRandomColour(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.namePermRandomColour));

        //Message
        bool HasMessageShowPerm(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.messagePermShow));
        bool HasMessagePerm(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.messagePermUse));
        bool HasMessageRainbow(IPlayer player) => (permission.UserHasPermission(player.Id, config.messagePermRainbow));
        bool CanMessageGradient(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.messagePermGradient));
        bool CanMessageBypass(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.messagePermBypass));
        bool CanMessageSetOthers(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.messagePermSetOthers));
        bool CanMessageRandomColour(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.messagePermRandomColour));

        bool IsValidName(string input)
        {
            if (config.nameUseBlacklist) return !config.nameBlockColoursHex.Any(x => (input == x));
            else if (config.nameUseBlacklist) return config.nameWhitelistedColoursHex.Any(x => (input == x));
            return true;
        }

        bool IsValidMessage(string input)
        {
            if (config.messageUseBlacklist) return !config.messageBlockColoursHex.Any(x => (input == x));
            else if (config.messageUseWhitelist) return config.messageWhitelistedColoursHex.Any(x => (input == x));
            return true;
        }

        void ProcessColourCommand(IPlayer player, string cmd, string[] args, bool isMessage = false)
        {
            if (args.Length < 1)
            {
                player.Reply(GetMessage("IncorrectUsage", player, isMessage ? config.messageColourCommand : config.nameColourCommand));
                return;
            }
            string colLower = string.Empty;
            if (args[0] == "set" || player.IsServer)
            {
                if ((!isMessage && !CanNameSetOthers(player)) || (isMessage && !CanMessageSetOthers(player)))
                {
                    player.Reply(GetMessage("NoPermissionSetOthers", player));
                    return;
                }
                if (args.Length < 3)
                {
                    player.Reply(GetMessage("IncorrectSetUsage", player, isMessage ? config.messageColourCommand : config.nameColourCommand));
                    return;
                }
                IPlayer target = covalence.Players.FindPlayer(args[1]);
                if (target == null)
                {
                    player.Reply(GetMessage("PlayerNotFound", player, args[1]));
                    return;
                }
                colLower = args[2].ToLower();
                ProcessColour(player, target, colLower, args.Skip(2).ToArray(), isMessage);
            }
            else
            {
                if ((!isMessage && !HasNamePerm(player)) || (isMessage && !HasMessagePerm(player)))
                {
                    player.Reply(GetMessage("NoPermission", player));
                    return;
                }
                if (args.Length < 1)
                {
                    player.Reply(GetMessage("IncorrectUsage", player, isMessage ? config.messageColourCommand : config.nameColourCommand));
                    return;
                }
                colLower = args[0].ToLower();
                ProcessColour(player, player, colLower, args.Skip(1).ToArray(), isMessage);
            }
        }

        void ProcessColoursCommand(IPlayer player, string cmd, string[] args, bool isMessage = false)
        {
            if ((!isMessage && !HasNamePerm(player)) || (isMessage && !HasMessagePerm(player)))
            {
                player.Reply(GetMessage("NoPermission", player));
                return;
            }
            string additionalInfo = string.Empty;

            if (isMessage && config.messageUseWhitelist)
            {
                additionalInfo += "Whitelisted Colours:\n";
                foreach (string colour in config.messageWhitelistedColoursHex)
                {
                    additionalInfo += "- " + colour + "\n";
                }
            }
            else if (isMessage && config.messageUseBlacklist)
            {
                additionalInfo += "Blacklisted Colours:\n";
                foreach (string colour in config.messageBlockColoursHex)
                {
                    additionalInfo += "- " + colour + "\n";
                }
            }
            else if (!isMessage && config.nameUseWhitelist)
            {
                additionalInfo += "Whitelisted Colours:\n";
                foreach (string colour in config.nameWhitelistedColoursHex)
                {
                    additionalInfo += "- " + colour + "\n";
                }
            }
            else if (!isMessage && config.nameUseBlacklist)
            {
                additionalInfo += "Blacklisted Colours:\n";
                foreach (string colour in config.nameBlockColoursHex)
                {
                    additionalInfo += "- " + colour + "\n";
                }
            }

            player.Reply(GetMessage("ColoursInfo", player, additionalInfo));
        }

        string ProcessColourMessage(string message, string colour) => $"<color={colour}>" + message + "</color>";

        void ProcessColour(IPlayer player, IPlayer target, string colLower, string[] colours, bool isMessage = false)
        {
            bool isCalledOnto = false;
            if (player != target) isCalledOnto = true;

            if (colLower == "gradient")
            {
                if ((!isMessage && !CanNameGradient(player)) || (isMessage && !CanMessageGradient(player)))
                {
                    player.Reply(GetMessage("NoPermissionGradient", player, isMessage ? "message" : "name"));
                    return;
                }
                if (colours.Length < 2)
                {
                    player.Reply(GetMessage("IncorrectGradientUsageArgs", player));
                    return;
                }
                string gradientName = ProcessGradient(isMessage ? "Example Message" : target.Name, colours);
                if (gradientName.Equals(string.Empty))
                {
                    player.Reply(GetMessage("IncorrectGradientUsage", player, isMessage ? config.messageColourCommand : config.nameColourCommand));
                    return;
                }
                if (isMessage)
                {
                    allColourData[target.Id].MessageColour = string.Empty;
                    allColourData[target.Id].MessageGradientArgs = colours;
                }
                else
                {
                    allColourData[target.Id].NameColour = string.Empty;
                    allColourData[target.Id].NameGradientArgs = colours;
                    if (!cachedNames.ContainsKey(target.Id)) cachedNames.Add(target.Id, gradientName);
                    else cachedNames[target.Id] = gradientName;
                }
                target.Reply(GetMessage("GradientChanged", target, isMessage ? "Message" : "Name", gradientName));
                if (isCalledOnto) player.Reply(GetMessage("GradientChangedFor", player, target.Name, isMessage ? "message" : "name", gradientName));
                return;
            }
            if (colLower == "reset" || colLower == "clear" || colLower == "remove")
            {
                if (isMessage)
                {
                    allColourData[target.Id].MessageColour = string.Empty;
                    allColourData[target.Id].MessageGradientArgs = null;
                }
                else
                {
                    allColourData[target.Id].NameColour = string.Empty;
                    allColourData[target.Id].NameGradientArgs = null;
                    if (cachedNames.ContainsKey(target.Id)) cachedNames.Remove(target.Id);
                }
                target.Reply(GetMessage("ColourRemoved", target, isMessage ? "Message" : "Name"));
                if (isCalledOnto) player.Reply(GetMessage("ColourRemovedFor", player, target.Name, isMessage ? "message" : "name"));
                return;
            }
            if (colLower == "random" && (!isMessage && CanNameRandomColour(player) || isMessage && CanMessageRandomColour(player)))
            {
                colLower = GetRndColour();
                if (isMessage) ChangeMessageColour(target, colLower, null);
                else ChangeNameColour(target, colLower, null);
                target.Reply(GetMessage("RndColour", target, isMessage ? "Message" : "Name", colLower));
                if (isCalledOnto) player.Reply(GetMessage("RndColourFor", player, isMessage ? "Message" : "Name", target.Name, colLower));
                return;
            }
            string invalidChar;
            if ((invalidChar = IsInvalidCharacter(colLower)) != null && (!isMessage && !CanNameBypass(player) || isMessage && !CanMessageBypass(player)))
            {
                player.Reply(GetMessage("InvalidCharacters", player, invalidChar));
                return;
            }
            if (!IsValidColour(colLower))
            {
                player.Reply(GetMessage("InvalidColour", player));
                return;
            }
            if (!IsValidName(colLower) && (!isMessage && !CanNameBypass(player) || isMessage && !CanMessageBypass(player)))
            {
                player.Reply(GetMessage("InvalidColour", player));
                return;
            }

            if (isMessage) ChangeMessageColour(target, colLower, null);
            else ChangeNameColour(target, colLower, null);

            target.Reply(GetMessage("ColourChanged", target, isMessage ? "Message" : "Name", colLower));
            if (isCalledOnto) player.Reply(GetMessage("ColourChangedFor", player, target.Name, isMessage ? "message" : "name", colLower));          
        }

        string ProcessGradient(string name, string[] colourArgs)
        {
            colourArgs = colourArgs.Where(col => IsValidName(col) && IsValidMessage(col) && IsValidColour(col) && (IsInvalidCharacter(col) == null ? true : false)).ToArray();

            var chars = name.ToCharArray();
            string gradientName = string.Empty;

            var colours = new List<Color>();
            Color startColour;
            Color endColour;
            int gradientsSteps = chars.Length / (colourArgs.Length - 1);
            if (gradientsSteps <= 1)
            {
                for (int i = 0; i < chars.Length; i++)
                {
                    if (i > colourArgs.Length - 1) ColorUtility.TryParseHtmlString(colourArgs[colourArgs.Length - 1], out startColour);
                    else ColorUtility.TryParseHtmlString(colourArgs[i], out startColour);
                    colours.Add(startColour);
                }
            }
            else
            {
                int gradientIterations = chars.Length / gradientsSteps;
                for (int i = 0; i < gradientIterations; i++)
                {
                    if (colours.Count >= chars.Length) continue;
                    if (i > colourArgs.Length - 1) ColorUtility.TryParseHtmlString(colourArgs[colourArgs.Length - 1], out startColour);
                    else ColorUtility.TryParseHtmlString(colourArgs[i], out startColour);
                    if (i >= colourArgs.Length - 1) endColour = startColour;
                    else ColorUtility.TryParseHtmlString(colourArgs[i + 1], out endColour);
                    foreach (var c in GetGradients(startColour, endColour, gradientsSteps)) colours.Add(c);
                }
                if (colours.Count < chars.Length)
                {
                    ColorUtility.TryParseHtmlString(colourArgs[colourArgs.Length - 1], out endColour);
                    while (colours.Count < chars.Length) colours.Add(endColour);
                }
            }
            for (int i = 0; i < colours.Count; i++)
            {
                gradientName += $"<color=#{ColorUtility.ToHtmlStringRGB(colours[i])}>{chars[i]}</color>";
            }
            return gradientName;
        } 

        List<Color> GetGradients(Color start, Color end, int steps)
        {
            var colours = new List<Color>();

            float stepR = ((end.r - start.r) / (steps - 1));
            float stepG = ((end.g - start.g) / (steps - 1));
            float stepB = ((end.b - start.b) / (steps - 1));

            for (int i = 0; i < steps; i++)
            {
                colours.Add(new Color(start.r + (stepR * i), start.g + (stepG * i), start.b + (stepB * i)));
            }
            return colours;
        }

        string GetRndColour() => String.Format("#{0:X6}", new System.Random().Next(0x1000000));

        string IsInvalidCharacter(string input) => (config.blockedValues.Where(x => input.Contains(x)).FirstOrDefault()) ?? null;

        void ClearUpData()
        {
            if (config.inactivityRemovalTime == 0) return;

            var copy = new Dictionary<string, PlayerData>(allColourData);
            foreach (var colData in copy)
            {
                if (colData.Value.LastActive + (config.inactivityRemovalTime * 3600) < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) allColourData.Remove(colData.Key);
            }
        }

        #endregion

        #region API

        public class ColouredChatMessage
        {
            public IPlayer Player;
            public string Name;
            public string Colour;
            public string Message;

            public ColouredChatMessage()
            {

            }

            public ColouredChatMessage(IPlayer player, string name, string colour, string message)
            {
                Player = player;
                Name = name;
                Colour = colour;
                Message = message;
            }

            public Dictionary<string, object> ToDictionary() => new Dictionary<string, object>
            {
                [nameof(Player)] = Player,
                [nameof(Name)] = Name,
                [nameof(Colour)] = Colour,
                [nameof(Message)] = Message
            };

            public static ColouredChatMessage FromDictionary(Dictionary<string, object> dict)
            {
                return new ColouredChatMessage()
                {
                    Player = dict[nameof(Player)] as IPlayer,
                    Name = dict[nameof(Name)] as string,
                    Colour = dict[nameof(Colour)] as string,
                    Message = dict[nameof(Message)] as string,
                };
            }
        }
        
        #endregion

    }
}

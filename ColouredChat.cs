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
using CompanionServer;

namespace Oxide.Plugins
{
    [Info("Coloured Chat", "collect_vood", "2.2.7")]
    [Description("Allows players to change their name & message colour in chat")]
    class ColouredChat : CovalencePlugin
    {
        [PluginReference]
        private Plugin BetterChat, BetterChatMute, ZoneManager;

        #region Constants

        private const string ColourRegex = "^#(?:[0-9a-fA-f]{3}){1,2}$";

        private const string ChatFormat = "{0}: {1}";

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "NoPermission", "You don't have permission to use this command." },
                { "NoPermissionSetOthers", "You don't have permission to set other players {0} colours." },
                { "NoPermissionGradient", "You don't have permission to use {0} gradients." },
                { "NoPermissionRandom", "You don't have permission to use random {0} colours." },
                { "NoPermissionRainbow", "You don't have permission to use the rainbow colours." },
                { "IncorrectGradientUsage", "Incorrect usage! To use gradients please use /{0} gradient hexCode1 hexCode2 ...</color>" },
                { "IncorrectGradientUsageArgs", "Incorrect usage! A gradient requires at least two different valid colours!"},
                { "GradientChanged", "{0} gradient changed to {1}!"},
                { "GradientChangedFor", "{0}'s gradient {1} colour changed to {2}!"},
                { "IncorrectUsage", "Incorrect usage! /{0} <colour>\nFor detailed help do /{1}" },
                { "IncorrectSetUsage", "Incorrect set usage! /{0} set <playerIdOrName> <colourOrColourArgument>\nFor a list of colours do /colours" },
                { "PlayerNotFound", "Player {0} was not found." },
                { "InvalidCharacters", "The character '{0}' is not allowed in colours. Please remove it." },
                { "ColourRemoved", "{0} colour removed!" },
                { "ColourRemovedFor", "{0}'s {1} colour was removed!" },
                { "ColourChanged", "{0} colour changed to <color={1}>{1}</color>!" },
                { "ColourChangedFor", "{0}'s {1} colour changed to <color={2}>{2}</color>!" },
                { "ColoursInfo", "You can only use hexcodes, eg '<color=#ffff94>#ffff94</color>'\nTo remove your colour, use 'clear', 'reset' or 'remove'\n\nAvailable Commands: {0}\n\n{1}"},
                { "InvalidColour", "That colour is not valid. Do /colours for more information on valid colours." },
                { "RndColour", "{0} colour was randomized to <color={1}>{1}</color>" },
                { "RndColourFor", "{0} colour of {1} randomized to <color={2}>{2}</color>."},
                { "RainbowColour", "{0} colour was set to rainbow." },
                { "RainbowColourFor", "{0} colour of {1} set to rainbow."},
                { "IncorrectGroupUsage", "Incorrect group usage! /{0} group <groupName> <colourOrColourArgument>\nFor a list of colours do /colours" },
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
            public string[] rainbowColours = { "#ff0000", "#ffa500", "#ffff94", "#008000", "#0000ff", "#4b0082", "#ee82ee" };
            [JsonProperty(PropertyName = "Blocked Characters")]
            public string[] blockedValues = { "{", "}", "size" };

            //Name
            [JsonProperty(PropertyName = "Name colour command")]
            public string nameColourCommand = "colour";
            [JsonProperty(PropertyName = "Name colours command (Help)")]
            public string nameColoursCommand = "colours";
            [JsonProperty(PropertyName = "Name show colour permission")]
            public string namePermShow = "colouredchat.name.show";
            [JsonProperty(PropertyName = "Name use permission")]
            public string namePermUse = "colouredchat.name.use";
            [JsonProperty(PropertyName = "Name use gradient permission")]
            public string namePermGradient = "colouredchat.name.gradient";
            [JsonProperty(PropertyName = "Name default rainbow name permission")]
            public string namePermRainbow = "colouredchat.name.rainbow";
            [JsonProperty(PropertyName = "Name bypass restrictions permission")]
            public string namePermBypass = "colouredchat.name.bypass";
            [JsonProperty(PropertyName = "Name set others colour permission")]
            public string namePermSetOthers = "colouredchat.name.setothers";
            [JsonProperty(PropertyName = "Name get random colour permission")]
            public string namePermRandomColour = "colouredchat.name.random";
            [JsonProperty(PropertyName = "Name use blacklist")]
            public bool nameUseBlacklist = true;
            [JsonProperty(PropertyName = "Name blocked colour hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> nameBlockColoursHex = new List<string>
            {
                { "#000000" }
            };
            [JsonProperty(PropertyName = "Name blocked colours range hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ColourRange> nameBlacklistedRangeColoursHex = new List<ColourRange>
            {
                { new ColourRange("#000000", "#000000") }
            };
            [JsonProperty(PropertyName = "Name use whitelist")]
            public bool nameUseWhitelist = false;
            [JsonProperty(PropertyName = "Name whitelisted colours hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> nameWhitelistedColoursHex = new List<string>
            {
                { "#000000" }
            };
            [JsonProperty(PropertyName = "Name whitelisted colour range hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ColourRange> nameWhitelistedRangeColoursHex = new List<ColourRange>
            {
                { new ColourRange("#000000", "#FFFFFF") }
            };

            //Message
            [JsonProperty(PropertyName = "Message colour command")]
            public string messageColourCommand = "mcolour";
            [JsonProperty(PropertyName = "Message colours command (Help)")]
            public string messageColoursCommand = "mcolours";
            [JsonProperty(PropertyName = "Message show colour permission")]
            public string messagePermShow = "colouredchat.message.show";
            [JsonProperty(PropertyName = "Message use permission")]
            public string messagePermUse = "colouredchat.message.use";
            [JsonProperty(PropertyName = "Message use gradient permission")]
            public string messagePermGradient = "colouredchat.message.gradient";
            [JsonProperty(PropertyName = "Message default rainbow name permission")]
            public string messagePermRainbow = "colouredchat.message.rainbow";
            [JsonProperty(PropertyName = "Message bypass restrictions permission")]
            public string messagePermBypass = "colouredchat.message.bypass";
            [JsonProperty(PropertyName = "Message set others colour permission")]
            public string messagePermSetOthers = "colouredchat.message.setothers";
            [JsonProperty(PropertyName = "Message get random colour permission")]
            public string messagePermRandomColour = "colouredchat.message.random";
            [JsonProperty(PropertyName = "Message use blacklist")]
            public bool messageUseBlacklist = true;
            [JsonProperty(PropertyName = "Message blocked colours hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> messageBlockColoursHex = new List<string>
            {
                { "#000000" }
            };
            [JsonProperty(PropertyName = "Message blocked colour range hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ColourRange> messageBlacklistedRangeColoursHex = new List<ColourRange>
            {
                { new ColourRange("#000000", "#000000") }
            };
            [JsonProperty(PropertyName = "Message use whitelist")]
            public bool messageUseWhitelist = false;
            [JsonProperty(PropertyName = "Message whitelisted colours hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> messageWhitelistedColoursHex = new List<string>
            {
                { "#000000" }
            };
            [JsonProperty(PropertyName = "Message whitelisted colour range hex", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ColourRange> messageWhitelistedRangeColoursHex = new List<ColourRange>
            {
                { new ColourRange("#000000", "#FFFFFF") }
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

        #region Colour Range Class

        public class ColourRange
        {
            [JsonProperty(PropertyName = "From")]
            public string _from;
            [JsonProperty(PropertyName = "To")]
            public string _to;

            public ColourRange(string from, string to)
            {
                _from = from;
                _to = to;
            }
        }

        #endregion

        #endregion

        #region Data

        private Dictionary<string, CachePlayerData> cachedData = new Dictionary<string, CachePlayerData>();
        private StoredData storedData;
        private Dictionary<string, PlayerData> allColourData => storedData.AllColourData;

        private class StoredData
        {
            public Dictionary<string, PlayerData> AllColourData { get; private set; } = new Dictionary<string, PlayerData>();
        }

        private class PlayerData
        {
            [JsonProperty("Name Colour")]
            public string NameColour = string.Empty;
            [JsonProperty("Name Gradient Args")]
            public string[] NameGradientArgs = null;

            [JsonProperty("Message Colour")]
            public string MessageColour = string.Empty;
            [JsonProperty("Message Gradient Args")]
            public string[] MessageGradientArgs = null;

            [JsonProperty("Last active")]
            public long LastActive = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            public PlayerData()
            {

            }

            public PlayerData(string nameColour = "", string[] nameGradientArgs = null, string messageColour = "", string[] messageGradientArgs = null)
            {
                NameColour = nameColour;
                NameGradientArgs = nameGradientArgs;

                MessageColour = messageColour;
                MessageGradientArgs = messageGradientArgs;
            }

            public PlayerData(bool isGroup)
            {
                LastActive = 0;
            }
        }

        private class CachePlayerData
        {
            public string NameColourGradient;
            public string PrimaryGroup;

            public CachePlayerData(string nameColourGradient = "", string primaryGroup = "")
            {
                NameColourGradient = nameColourGradient;
                PrimaryGroup = primaryGroup;
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

        private void OnServerSave()
        {
            ClearUpData();
            SaveData();
        } 
        private void Unload() => SaveData();

        private void ChangeNameColour(string key, string colour, string[] colourArgs)
        {
            var playerData = new PlayerData(colour, colourArgs);
            if (!allColourData.ContainsKey(key)) allColourData.Add(key, playerData);

            allColourData[key].NameColour = colour;
            allColourData[key].NameGradientArgs = colourArgs;
        }

        private void ChangeMessageColour(string key, string colour, string[] colourArgs)
        {
            var playerData = new PlayerData(string.Empty, null, colour, colourArgs);           
            if (!allColourData.ContainsKey(key)) allColourData.Add(key, playerData);

            allColourData[key].MessageColour = colour;
            allColourData[key].MessageGradientArgs = colourArgs;
        }

        #endregion

        #region Hooks

        private void Init()
        {
            if (config.messageUseBlacklist && config.messageUseWhitelist || config.nameUseBlacklist && config.nameUseWhitelist) PrintWarning("You are using both black/- and whitelist! This might cause issues.");

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

        private void OnUserConnected(IPlayer player)
        {
            if (!allColourData.ContainsKey(player.Id)) return;
            allColourData[player.Id].LastActive = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private void OnUserDisconnected(IPlayer player)
        {
            if (!allColourData.ContainsKey(player.Id)) return;
            allColourData[player.Id].LastActive = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private void OnUserNameUpdated(string id, string oldName, string newName) => ClearCache(id);

        private void OnUserGroupAdded(string id, string groupName) => ClearCache(id);

        private void OnUserGroupRemoved(string id, string groupName) => ClearCache(id);

        private void OnGroupDeleted(string name) => ClearCache();

        private void OnGroupPermissionGranted(string name, string perm) => ClearCache();

        private void OnGroupPermissionRevoked(string name, string perm) => ClearCache();

        private object OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (BetterChatIns()) return null;
            if (config.blockChatMute && BetterChatMuteIns()) if (BetterChatMute.Call<bool>("API_IsMuted", player.IPlayer)) return null;
            if (player == null) return null;
            if (ZoneManagerIns() && ZoneManager.Call<bool>("PlayerHasFlag", player, "nochat")) return false;

            if (Chat.serverlog)
            {
                object[] objArray = new object[] { ConsoleColor.DarkYellow, null, null, null };
                objArray[1] = string.Concat(new object[] { "[", channel, "] ", player.displayName.EscapeRichText(), ": " });
                objArray[2] = ConsoleColor.DarkGreen;
                objArray[3] = message;
                ServerConsole.PrintColoured(objArray);
            }

            var colouredChatMessage = FromMessage(player.IPlayer, channel, message);
            var colouredChatMessageDict = colouredChatMessage.ToDictionary();

            #region API

            foreach (Plugin plugin in plugins.GetAll())
            {
                object obj = plugin.CallHook("OnColouredChat", colouredChatMessageDict);

                if (obj is Dictionary<string, object>)
                {
                    try
                    {
                        colouredChatMessageDict = obj as Dictionary<string, object>;
                    }
                    catch (Exception e)
                    {
                        PrintError($"Failed to load modified OnColouredChat hook data from plugin '{plugin.Title} ({plugin.Version})':{Environment.NewLine}{e}");
                        continue;
                    }
                }
                else if (obj != null) return obj;
            }

            colouredChatMessage = ColouredChatMessage.FromDictionary(colouredChatMessageDict);

            #endregion

            SendMessage(colouredChatMessage);
            return true;
        }

        private Dictionary<string, object> OnBetterChat(Dictionary<string, object> dict)
        {
            if (dict != null)
            {
                IPlayer player = dict["Player"] as IPlayer;

                var colouredChatMessage = FromMessage(player, (Chat.ChatChannel)dict["ChatChannel"], 
                    dict["Message"].ToString());

                #region API

                var colouredChatMessageDict = colouredChatMessage.ToDictionary();

                foreach (Plugin plugin in plugins.GetAll())
                {
                    object obj = plugin.CallHook("OnColouredChat", colouredChatMessageDict);

                    if (obj is Dictionary<string, object>)
                    {
                        try
                        {
                            colouredChatMessageDict = obj as Dictionary<string, object>;
                        }
                        catch (Exception e)
                        {
                            PrintError($"Failed to load modified OnColouredChat hook data from plugin '{plugin.Title} ({plugin.Version})':{Environment.NewLine}{e}");
                            continue;
                        }
                    }
                    else if (obj != null)
                    {
                        if (obj is bool)
                        {
                            dict["CancelOption"] = 2;
                        }
                    }
                }

                colouredChatMessage = ColouredChatMessage.FromDictionary(colouredChatMessageDict);

                #endregion

                if (!string.IsNullOrEmpty(colouredChatMessage.Name)) dict["Username"] = colouredChatMessage.Name;
                if (!string.IsNullOrEmpty(colouredChatMessage.Colour)) { ((Dictionary<string, object>)dict["UsernameSettings"])["Color"] = colouredChatMessage.Colour; }
                dict["Message"] = colouredChatMessage.Message;
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

        private bool BetterChatIns() => (BetterChat != null && BetterChat.IsLoaded);
        private bool BetterChatMuteIns() => (BetterChatMute != null && BetterChatMute.IsLoaded);
        private bool ZoneManagerIns() => (ZoneManager != null && ZoneManager.IsLoaded);
        private bool IsValidColour(string input) => Regex.Match(input, ColourRegex).Success;
        private string GetMessage(string key, IPlayer player, params string[] args) => String.Format(lang.GetMessage(key, this, player.Id), args);

        //Name
        private bool HasNameShowPerm(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.namePermShow));
        private bool HasNamePerm(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.namePermUse));
        private bool HasNameRainbow(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.namePermRainbow));
        private bool CanNameGradient(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.namePermGradient));
        private bool CanNameBypass(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.namePermBypass));
        private bool CanNameSetOthers(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.namePermSetOthers));
        private bool CanNameRandomColour(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.namePermRandomColour));

        //Message
        private bool HasMessageShowPerm(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.messagePermShow));
        private bool HasMessagePerm(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.messagePermUse));
        private bool HasMessageRainbow(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.messagePermRainbow));
        private bool CanMessageGradient(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.messagePermGradient));
        private bool CanMessageBypass(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.messagePermBypass));
        private bool CanMessageSetOthers(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.messagePermSetOthers));
        private bool CanMessageRandomColour(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.messagePermRandomColour));

        private bool IsValidName(string input, IPlayer iPlayer = null)
        {
            if (iPlayer != null && CanNameBypass(iPlayer)) return true;
            if (config.nameUseBlacklist)
            {
                bool inRange = false;
                foreach (var colourRange in config.nameBlacklistedRangeColoursHex)
                {
                    inRange = IsInHexRange(input, colourRange._from, colourRange._to);
                    if (inRange) break;
                }
                return !config.nameBlockColoursHex.Any(x => (input == x)) && !inRange;
            }
            else if (config.nameUseWhitelist)
            {
                bool inRange = false;
                foreach (var colourRange in config.nameWhitelistedRangeColoursHex)
                {
                    inRange = IsInHexRange(input, colourRange._from, colourRange._to);
                    if (!inRange) break;
                }
                return config.nameWhitelistedColoursHex.Any(x => (input == x)) || inRange;
            }
            return true;
        }

        private bool IsValidMessage(string input, IPlayer iPlayer = null)
        {
            if (iPlayer != null && CanMessageBypass(iPlayer)) return true;
            if (config.messageUseBlacklist)
            {
                bool inRange = false;
                foreach (var colourRange in config.messageBlacklistedRangeColoursHex)
                {
                    inRange = IsInHexRange(input, colourRange._from, colourRange._to);
                    if (inRange) break;
                }
                return !config.messageBlockColoursHex.Any(x => (input == x)) && !inRange;
            }
            else if (config.messageUseWhitelist)
            {
                bool inRange = false;
                foreach (var colourRange in config.messageWhitelistedRangeColoursHex)
                {
                    inRange = IsInHexRange(input, colourRange._from, colourRange._to);
                    if (!inRange) break;
                }
                return config.messageWhitelistedColoursHex.Any(x => (input == x)) || inRange;
            }
            return true;
        }

        private void SendMessage(ColouredChatMessage colouredChatMessage, List<BasePlayer> players = null)
        {
            var player = colouredChatMessage.Player.Object as BasePlayer;
            string formattedMsg = string.Format("{0}[{1}]", new string[] { player?.displayName?.EscapeRichText() ?? string.Empty, player?.UserIDString ?? string.Empty });

            string chatMessage = colouredChatMessage.GetChatOutput();
            var receivers = new ListHashSet<BasePlayer>();

            if (players != null)
            {
                foreach (var ply in players)
                {
                    ply.SendConsoleCommand("chat.add2", (int)colouredChatMessage.ChatChannel, player.userID, colouredChatMessage.Message, colouredChatMessage.Name, colouredChatMessage.Colour);
                }
                DebugEx.Log(string.Concat("[PRIVATE CHAT] ", formattedMsg, " : ", colouredChatMessage.Message), StackTraceLogType.None);
            }
            else if (colouredChatMessage.ChatChannel == Chat.ChatChannel.Global)
            {
                receivers = BasePlayer.activePlayerList;
                DebugEx.Log(string.Concat("[CHAT] ", formattedMsg, " : ", colouredChatMessage.Message), StackTraceLogType.None);
            }
            else if (colouredChatMessage.ChatChannel == Chat.ChatChannel.Team)
            {
                if (player?.Team == null) return;

                player.Team.BroadcastTeamChat(player.userID, player.displayName, colouredChatMessage.Message, colouredChatMessage.Colour);
                foreach (ulong memberId in player.Team.members)
                {
                    BasePlayer member;
                    if ((member = RelationshipManager.FindByID(memberId)) != null) 
                        receivers.Add(member);
                }
                DebugEx.Log(string.Concat("[TEAM CHAT] ", formattedMsg, " : ", colouredChatMessage.Message), StackTraceLogType.None);
            }

            foreach (var receiver in receivers)
                receiver.SendConsoleCommand("chat.add", new object[] { (int)colouredChatMessage.ChatChannel, player.userID, chatMessage });

            Chat.ChatEntry chatentry = new Chat.ChatEntry
            {
                Channel = colouredChatMessage.ChatChannel,
                Message = colouredChatMessage.Message,
                UserId = player.UserIDString,
                Username = player.displayName,
                Color = colouredChatMessage.Colour,
                Time = Facepunch.Math.Epoch.Current
            };
            Facepunch.RCon.Broadcast(Facepunch.RCon.LogType.Chat, chatentry);
        }

        private void ProcessColourCommand(IPlayer player, string cmd, string[] args, bool isMessage = false)
        {
            if (args.Length < 1)
            {
                player.Reply(GetMessage("IncorrectUsage", player, isMessage ? config.messageColourCommand : config.nameColourCommand, isMessage ? config.messageColoursCommand : config.nameColoursCommand));
                return;
            }
            string colLower = string.Empty;
            if (args[0] == "set")
            {
                if ((!isMessage && !CanNameSetOthers(player)) || (isMessage && !CanMessageSetOthers(player)))
                {
                    player.Reply(GetMessage("NoPermissionSetOthers", player, isMessage ? "message" : "name"));
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
            else if (args[0] == "group")
            {
                if (!player.IsAdmin)
                {
                    player.Reply(GetMessage("NoPermission", player));
                    return;
                }
                if (args.Length < 3)
                {
                    player.Reply(GetMessage("IncorrectGroupUsage", player, isMessage ? config.messageColourCommand : config.nameColourCommand));
                    return;
                }
                if (!permission.GroupExists(args[1])) permission.CreateGroup(args[1], string.Empty, 0);
                colLower = args[2].ToLower();
                ProcessColour(player, player, colLower, args.Skip(3).ToArray(), isMessage, args[1]);
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

        private void ProcessColoursCommand(IPlayer player, string cmd, string[] args, bool isMessage = false)
        {
            if ((!isMessage && !HasNamePerm(player)) || (isMessage && !HasMessagePerm(player)))
            {
                player.Reply(GetMessage("NoPermission", player));
                return;
            }
            string availableCommands = string.Empty;

            if (isMessage)
            {
                availableCommands += $"\n/{config.messageColourCommand} <color=#ff6666>#ff6666</color>";
                if (CanMessageRandomColour(player)) availableCommands += $"\n/{config.messageColourCommand} random";
                if (CanMessageGradient(player)) availableCommands += $"\n/{config.messageColourCommand} gradient <color=#ff6666>#ff6666</color> <color=#ff6666>#ff6666</color>" +
                                                                     $"\n/{config.messageColourCommand} gradient <color=#ff6666>#ff6666</color> <color=#ffff94>#ffff94</color> <color=#90ee90>#90ee90</color>";
                if (CanMessageSetOthers(player)) availableCommands += $"\n/{config.messageColourCommand} set <color=#a8a8a8>playerIdOrName</color> <color=#ff6666>#ff6666</color>" +
                                                                      $"\n/{config.messageColourCommand} set <color=#a8a8a8>playerIdOrName</color> gradient <color=#ff6666>#ff6666</color> <color=#ffff94>#ffff94</color>";
                if (player.IsAdmin) availableCommands += $"\n/{config.messageColourCommand} group <color=#a8a8a8>groupName</color> <color=#ff6666>#ff6666</color>" +
                                                         $"\n/{config.messageColourCommand} group <color=#a8a8a8>groupName</color> gradient <color=#ff6666>#ff6666</color> <color=#ffff94>#ffff94</color>";
            }
            else
            {
                availableCommands += $"\n/{config.nameColourCommand} <color=#ff6666>#ff6666</color>";
                if (CanNameRandomColour(player)) availableCommands += $"\n/{config.nameColourCommand} random";
                if (CanNameGradient(player)) availableCommands += $"\n/{config.nameColourCommand} gradient <color=#ff6666>#ff6666</color> <color=#ffff94>#ffff94</color>" +
                                                                     $"\n/{config.nameColourCommand} gradient <color=#ff6666>#ff6666</color> <color=#ffff94>#ffff94</color> <color=#90ee90>#90ee90</color>";
                if (CanNameSetOthers(player)) availableCommands += $"\n/{config.nameColourCommand} set <color=#a8a8a8>playerIdOrName</color> <color=#ff6666>#ff6666</color>" +
                                                                      $"\n/{config.nameColourCommand} set <color=#a8a8a8>playerIdOrName</color> gradient <color=#ff6666>#ff6666</color> <color=#ffff94>#ffff94</color>";
                if (player.IsAdmin) availableCommands += $"\n/{config.nameColourCommand} group <color=#a8a8a8>groupName</color> <color=#ff6666>#ff6666</color>" +
                                                         $"\n/{config.nameColourCommand} group <color=#a8a8a8>groupName</color> gradient <color=#ff6666>#ff6666</color> <color=#ffff94>#ffff94</color>";
            }

            string additionalInfo = string.Empty;
                
            if (isMessage && config.messageUseWhitelist)
            {
                additionalInfo += "Whitelisted Colours:\n";
                foreach (string colour in config.messageWhitelistedColoursHex)
                {
                    additionalInfo += "- <color=" + colour + ">" + colour + "</color>\n";
                }
                foreach (var colourRange in config.messageWhitelistedRangeColoursHex)
                {
                    additionalInfo += "- From <color=" + colourRange._from + ">" + colourRange._from + "</color> to <color=" + colourRange._to + ">" + colourRange._to + "</color>\n";
                }
            }
            else if (isMessage && config.messageUseBlacklist)
            {
                additionalInfo += "Blacklisted Colours:\n";
                foreach (string colour in config.messageBlockColoursHex)
                {
                    additionalInfo += "- <color=" + colour + ">" + colour + "</color>\n";
                }
                foreach (var colourRange in config.messageBlacklistedRangeColoursHex)
                {
                    additionalInfo += "- From <color=" + colourRange._from + ">" + colourRange._from + "</color> to <color=" + colourRange._to + ">" + colourRange._to + "</color>\n";
                }
            }
            else if (!isMessage && config.nameUseWhitelist)
            {
                additionalInfo += "Whitelisted Colours:\n";
                foreach (string colour in config.nameWhitelistedColoursHex)
                {
                    additionalInfo += "- <color=" + colour + ">" + colour + "</color>\n";
                }
                foreach (var colourRange in config.nameWhitelistedRangeColoursHex)
                {
                    additionalInfo += "- From <color=" + colourRange._from + ">" + colourRange._from + "</color> to <color=" + colourRange._to + ">" + colourRange._to + "</color>\n";
                }
            }
            else if (!isMessage && config.nameUseBlacklist)
            {
                additionalInfo += "Blacklisted Colours:\n";
                foreach (string colour in config.nameBlockColoursHex)
                {
                    additionalInfo += "- <color=" + colour + ">" + colour + "</color>\n";
                }
                foreach (var colourRange in config.nameBlacklistedRangeColoursHex)
                {
                    additionalInfo += "- From <color=" + colourRange._from + ">" + colourRange._from + "</color> to <color=" + colourRange._to + ">" + colourRange._to + "</color>\n";
                }
            }

            player.Reply(GetMessage("ColoursInfo", player, availableCommands, additionalInfo));
        }

        private string ProcessColourMessage(string message, string colour) => $"<color={colour}>" + message + "</color>";

        private void ProcessColour(IPlayer player, IPlayer target, string colLower, string[] colours, bool isMessage = false, string groupName = "")
        {
            bool isGroup = false;
            if (!string.IsNullOrEmpty(groupName)) isGroup = true;
            bool isCalledOnto = false;
            if (player != target && !isGroup) isCalledOnto = true;

            string key = isGroup ? groupName : target.Id;
            
            if (!isGroup && !allColourData.ContainsKey(target.Id)) allColourData.Add(target.Id, new PlayerData());
            else if (isGroup && !allColourData.ContainsKey(groupName)) allColourData.Add(groupName, new PlayerData(true));

            if (colLower == "gradient")
            {
                if ((!isMessage && !CanNameGradient(player)) || (isMessage && !CanMessageGradient(player)))
                {
                    player.Reply(GetMessage("NoPermissionGradient", player, isMessage ? "message" : "name"));
                    return;
                }
                colours = colours.Where(col => isMessage ? IsValidMessage(col, player) : IsValidName(col, player) && IsValidColour(col) && (IsInvalidCharacter(col) == null ? true : false)).ToArray();
                if (colours.Length < 2)
                {
                    player.Reply(GetMessage("IncorrectGradientUsageArgs", player, isMessage ? config.messageColourCommand : config.nameColourCommand));
                    return;
                }
                string gradientName = ProcessGradient(isMessage ? "Example Message" : target.Name, colours, isMessage, player);
                if (gradientName.Equals(string.Empty))
                {
                    player.Reply(GetMessage("IncorrectGradientUsage", player, isMessage ? config.messageColourCommand : config.nameColourCommand));
                    return;
                }
                if (isMessage)
                {
                    allColourData[key].MessageColour = string.Empty;
                    allColourData[key].MessageGradientArgs = colours;
                }
                else
                {
                    allColourData[key].NameColour = string.Empty;
                    allColourData[key].NameGradientArgs = colours;
                    if (!isGroup)
                    {
                        if (!cachedData.ContainsKey(key)) cachedData.Add(key, new CachePlayerData(gradientName, GetPrimaryUserGroup(player.Id)));
                        else cachedData[key].NameColourGradient = gradientName;
                    }
                }
                if (isGroup) ClearCache();

                if (target.IsConnected) target.Reply(GetMessage("GradientChanged", target, GetCorrectLang(isGroup, isMessage, key), gradientName));
                if (isCalledOnto) player.Reply(GetMessage("GradientChangedFor", player, target.Name, isMessage ? "message" : "name", gradientName));
                return;
            }
            if (colLower == "reset" || colLower == "clear" || colLower == "remove")
            {
                if (isMessage)
                {
                    allColourData[key].MessageColour = string.Empty;
                    allColourData[key].MessageGradientArgs = null;
                } 
                else
                {
                    allColourData[key].NameColour = string.Empty;
                    allColourData[key].NameGradientArgs = null;
                    if (cachedData.ContainsKey(key)) cachedData.Remove(key);
                }
                if (string.IsNullOrEmpty(allColourData[key].NameColour) && allColourData[key].NameGradientArgs == null && string.IsNullOrEmpty(allColourData[key].MessageColour) && allColourData[key].MessageGradientArgs == null) allColourData.Remove(key);
                if (isGroup) ClearCache();

                if (target.IsConnected) target.Reply(GetMessage("ColourRemoved", target, GetCorrectLang(isGroup, isMessage, key)));
                if (isCalledOnto) player.Reply(GetMessage("ColourRemovedFor", player, target.Name, isMessage ? "message" : "name"));
                return;
            }
            if (colLower == "random")
            {
                if (!isMessage && !CanNameRandomColour(player) || isMessage && !CanMessageRandomColour(player))
                {
                    player.Reply(GetMessage("NoPermissionRandom", player, isMessage ? "message" : "name"));
                    return;
                }
                colLower = GetRndColour();
                if (isMessage) ChangeMessageColour(key, colLower, null);
                else ChangeNameColour(key, colLower, null);
                if (isGroup) ClearCache();

                if (target.IsConnected) target.Reply(GetMessage("RndColour", target, GetCorrectLang(isGroup, isMessage, key), colLower));
                if (isCalledOnto) player.Reply(GetMessage("RndColourFor", player, isMessage ? "Message" : "Name", target.Name, colLower));
                return;
            }
            if (colLower == "rainbow")
            {
                if (isMessage && !HasMessageRainbow(player) || !HasNameRainbow(player))
                {
                    player.Reply(GetMessage("NoPermissionRainbow", player));
                    return;
                }

                if (isMessage) ChangeMessageColour(key, string.Empty, config.rainbowColours);
                else ChangeNameColour(key, string.Empty, config.rainbowColours);
                if (isGroup) ClearCache();

                if (target.IsConnected) target.Reply(GetMessage("RainbowColour", target, GetCorrectLang(isGroup, isMessage, key)));
                if (isCalledOnto) player.Reply(GetMessage("RainbowColourFor", player, isMessage ? "Message" : "Name", target.Name));
                return;
            }
            string invalidChar;
            if ((invalidChar = IsInvalidCharacter(colLower)) != null)
            {
                player.Reply(GetMessage("InvalidCharacters", player, invalidChar));
                return;
            }
            if (!IsValidColour(colLower))
            {
                player.Reply(GetMessage("InvalidColour", player));
                return;
            }
            if (isMessage ? !IsValidMessage(colLower, player) : !IsValidName(colLower, player))
            {
                player.Reply(GetMessage("InvalidColour", player));
                return;
            }

            if (isMessage) ChangeMessageColour(key, colLower, null);
            else ChangeNameColour(key, colLower, null);

            if (isCalledOnto) player.Reply(GetMessage("ColourChangedFor", player, target.Name, isMessage ? "message" : "name", colLower));
            else if (isGroup && target.IsConnected) target.Reply(GetMessage("ColourChangedFor", player, key, isMessage ? "message" : "name", colLower));
            else if (target.IsConnected) target.Reply(GetMessage("ColourChanged", target, isMessage ? "Message" : "Name", colLower));
            if (isGroup) ClearCache();
        }

        private string ProcessGradient(string name, string[] colourArgs, bool isMessage = false, IPlayer iPlayer = null)
        {           
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

        private List<Color> GetGradients(Color start, Color end, int steps)
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

        private string GetRndColour() => String.Format("#{0:X6}", new System.Random().Next(0x1000000));

        private string IsInvalidCharacter(string input) => (config.blockedValues.Where(x => input.Contains(x)).FirstOrDefault()) ?? null;

        private void ClearUpData()
        {
            if (config.inactivityRemovalTime == 0) return;

            var copy = new Dictionary<string, PlayerData>(allColourData);
            foreach (var colData in copy)
            {
                if (colData.Value.LastActive == 0) continue;
                if (colData.Value.LastActive + (config.inactivityRemovalTime * 86400) < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) allColourData.Remove(colData.Key);
            }
        }

        private void ClearCache()
        {
            var cachedCopy = new Dictionary<string, CachePlayerData>(cachedData);
            foreach (var cache in cachedCopy) cachedData.Remove(cache.Key);
        }

        private void ClearCache(string Id)
        {
            if (cachedData.ContainsKey(Id)) cachedData.Remove(Id);
        }

        private string GetCorrectLang(bool isGroup, bool isMessage, string key) => isGroup ? (isMessage ? "Group " + key + " message" : "Group " + key + " name") : isMessage ? "Message" : "Name";

        private string GetPrimaryUserGroup(string Id)
        {
            var groups = permission.GetUserGroups(Id);

            string primaryGroup = string.Empty;
            int groupRank = -1;
            foreach (var group in groups)
            {
                if (!allColourData.ContainsKey(group)) continue;
                int currentGroupRank = permission.GetGroupRank(group);
                if (currentGroupRank > groupRank)
                {
                    groupRank = currentGroupRank;
                    primaryGroup = group;
                }
            }
            return primaryGroup;
        }

        private ColouredChatMessage FromMessage(IPlayer player, Chat.ChatChannel channel, string message)
        {
            var playerData = new PlayerData();
            if (allColourData.ContainsKey(player.Id)) playerData = allColourData[player.Id];
            var colouredNameData = GetColouredName(player, playerData);
                  
            colouredNameData.ChatChannel = channel;
            colouredNameData.Message = GetColouredMessage(player, playerData, message);
            return colouredNameData;
        }

        private ColouredChatMessage GetColouredName(IPlayer player, PlayerData playerData)
        {
            string playerUserName = player.Name;
            string playerColour = player.IsAdmin ? "#af5" : "#5af";
            string playerColourNonModified = playerColour;

            if (!cachedData.ContainsKey(player.Id))
            {
                string gradientName = string.Empty;
                if (playerData?.NameGradientArgs != null) 
                    gradientName = ProcessGradient(player.Name, playerData.NameGradientArgs, false, player);

                cachedData.Add(player.Id, new CachePlayerData(gradientName, GetPrimaryUserGroup(player.Id)));
            }

            if (HasNameShowPerm(player))
            {
                //Gradient Handling
                if (playerData?.NameGradientArgs != null) playerUserName = cachedData[player.Id].NameColourGradient;
                else if (!string.IsNullOrEmpty(playerData?.NameColour)) playerColour = playerData.NameColour;
                else if (playerUserName == player.Name && !string.IsNullOrEmpty(cachedData[player.Id].NameColourGradient)) playerUserName = cachedData[player.Id].NameColourGradient;
            }

            string userPrimaryGroup = cachedData[player.Id].PrimaryGroup;
            if (allColourData.ContainsKey(userPrimaryGroup))
            {
                var groupData = allColourData[userPrimaryGroup];
                if (playerUserName == player.Name && playerColour == playerColourNonModified)
                {
                    if (groupData?.NameGradientArgs != null) playerUserName = string.IsNullOrEmpty(cachedData[player.Id].NameColourGradient) ?
                            cachedData[player.Id].NameColourGradient = ProcessGradient(player.Name, groupData.NameGradientArgs, false, player) :
                            cachedData[player.Id].NameColourGradient;
                    else if (!string.IsNullOrEmpty(groupData?.NameColour)) playerColour = groupData.NameColour;
                }
            }

            return new ColouredChatMessage() { Player = player, Name = playerUserName, Colour = (playerColour == playerColourNonModified && BetterChatIns()) ? string.Empty : playerColour };
        }

        private string GetColouredMessage(IPlayer player, PlayerData playerData, string message)
        {
            string playerMessage = message;

            if (HasNameShowPerm(player))
            {
                if (playerData?.MessageGradientArgs != null) 
                    playerMessage = ProcessGradient(message, playerData.MessageGradientArgs, true, player);
                else if (!string.IsNullOrEmpty(playerData?.MessageColour)) 
                    playerMessage = ProcessColourMessage(message, playerData.MessageColour);
            }

            //Group Handling
            string userPrimaryGroup = cachedData[player.Id].PrimaryGroup;
            if (allColourData.ContainsKey(userPrimaryGroup))
            {
                var groupData = allColourData[userPrimaryGroup];
                if (playerMessage == message)
                {
                    if (groupData?.MessageGradientArgs != null) 
                        playerMessage = ProcessGradient(message, groupData.MessageGradientArgs, true, player);
                    else if (!string.IsNullOrEmpty(groupData?.MessageColour)) 
                        playerMessage = ProcessColourMessage(message, groupData.MessageColour);
                }
            }

            return playerMessage;
        }

        private bool IsInHexRange(string hexCode,string rangeHexCode1, string rangeHexCode2)
        {
            Color mainColour;
            ColorUtility.TryParseHtmlString(hexCode, out mainColour);
            Color start;
            ColorUtility.TryParseHtmlString(rangeHexCode1, out start);
            Color end;
            ColorUtility.TryParseHtmlString(rangeHexCode2, out end);

            if ((mainColour.r >= start.r && mainColour.r <= end.r) &&
                (mainColour.g >= start.g && mainColour.g <= end.g) &&
                (mainColour.b >= start.b && mainColour.b <= end.b)) return true;

            return false;
        }

        #endregion

        #region API

        private string API_GetColouredName(IPlayer player)
        {
            var playerData = new PlayerData();
            if (allColourData.ContainsKey(player.Id)) playerData = allColourData[player.Id];

            var colouredData = GetColouredName(player, playerData);
            if (!string.IsNullOrEmpty(colouredData.Colour))
                return $"<color={colouredData.Colour}>{player.Name}</color>";

            return colouredData.Name;
        }

        private string API_GetColouredMessage(IPlayer player, string message)
        {
            var playerData = new PlayerData();
            if (allColourData.ContainsKey(player.Id)) playerData = allColourData[player.Id];

            return GetColouredMessage(player, playerData, message);
        }

        private string API_GetColouredChatMessage(IPlayer iPlayer, Chat.ChatChannel channel,
            string message)
        {
            var colouredChatMessage = FromMessage(iPlayer, channel, message);

            string formattedMessage = colouredChatMessage.GetChatOutput();

            if (BetterChatIns())
            {
                Dictionary<string, object> betterChatMessageData = BetterChat.CallHook("API_GetMessageData", iPlayer, message) as Dictionary<string, object>;

                if (!string.IsNullOrEmpty(colouredChatMessage.Name)) betterChatMessageData["Username"] = colouredChatMessage.Name;
                if (!string.IsNullOrEmpty(colouredChatMessage.Colour)) { ((Dictionary<string, object>)betterChatMessageData["UsernameSettings"])["Color"] = colouredChatMessage.Colour; }

                formattedMessage = BetterChat.CallHook("API_GetFormattedMessageFromDict", betterChatMessageData) as string;
            }

            return formattedMessage;
        }

        public class ColouredChatMessage
        {
            public IPlayer Player;
            public Chat.ChatChannel ChatChannel;
            public string Name;
            public string Colour;
            public string Message;

            public ColouredChatMessage()
            {

            }

            public ColouredChatMessage(IPlayer player, Chat.ChatChannel chatChannel, string name, string colour, string message)
            {
                Player = player;
                ChatChannel = chatChannel;
                Name = name;
                Colour = colour;
                Message = message;
            }

            public Dictionary<string, object> ToDictionary() => new Dictionary<string, object>
            {
                [nameof(Player)] = Player,
                [nameof(ChatChannel)] = ChatChannel,
                [nameof(Name)] = Name,
                [nameof(Colour)] = Colour,
                [nameof(Message)] = Message
            };

            public static ColouredChatMessage FromDictionary(Dictionary<string, object> dict)
            {
                return new ColouredChatMessage()
                {
                    Player = dict[nameof(Player)] as IPlayer,
                    ChatChannel = (Chat.ChatChannel)dict[nameof(ChatChannel)],
                    Name = dict[nameof(Name)] as string,
                    Colour = dict[nameof(Colour)] as string,
                    Message = dict[nameof(Message)] as string,
                };
            }     
            
            public string GetChatOutput()
            {
                return string.Format(ChatFormat, $"<color={Colour}>{Name}</color>", Message);
            }
        }
        
        #endregion

    }
}

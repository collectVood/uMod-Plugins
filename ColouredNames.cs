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
    [Info("Coloured Names", "collect_vood", "1.4.3")]
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
                { "NoPermissionGradient", "You don't have permission to use gradients." },
                { "IncorrectGradientUsage", "Incorrect usage! To use gradients please use /{0} gradient hexCode1 hexCode2 ...</color>" },
                { "IncorrectGradientUsageArgs", "Incorrect usage! A gradient requires at least two different colours!"},
                { "GradientChanged", "Name gradient changed to {0}!"},
                { "GradientChangedFor", "{0}'s gradient colour changed to {1}!"},
                { "IncorrectUsage", "Incorrect usage! /{0} <colour>\nFor a list of colours do /colours" },
                { "IncorrectSetUsage", "Incorrect set usage! /{0} set <playerIdOrName> <colourOrColourArgument>\nFor a list of colours do /colours" },
                { "PlayerNotFound", "Player {0} was not found." },
                { "InvalidCharacters", "The character '{0}' is not allowed in colours. Please remove it." },
                { "ColourRemoved", "Name colour removed!" },
                { "ColourRemovedFor", "{0}'s name colour was removed!" },
                { "ColourChanged", "Name colour changed to <color={0}>{0}</color>!" },
                { "ColourChangedFor", "{0}'s name colour changed to <color={1}>{1}</color>!" },
                { "ColoursInfo", "You can only use hexcode, eg '<color=#FFFF00>#FFFF00</color>'\nTo remove your colour, use 'clear', 'reset' or 'remove'\n\n{0}" },
                { "InvalidColour", "That colour is not valid. Do /colours for more information on valid colours." },
                { "RndColour", "Colour was randomized to <color={0}>{0}</color>" },
                { "RndColourFor", "Colour of {0} randomized to {1}."},
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
            [JsonProperty(PropertyName = "Show colour permission")]
            public string permShow = "colourednames.show";
            [JsonProperty(PropertyName = "Use permission")]
            public string permUse = "colourednames.use";
            [JsonProperty(PropertyName = "Use gradient permission")]
            public string permGradient = "colourednames.gradient";
            [JsonProperty(PropertyName = "Default Rainbow Name Permission")]
            public string permRainbow = "colourednames.rainbow";
            [JsonProperty(PropertyName = "Bypass restrictions permission")]
            public string permBypass = "colourednames.bypass";
            [JsonProperty(PropertyName = "Set others colour permission")]
            public string permSetOthers = "colourednames.setothers";
            [JsonProperty(PropertyName = "Get random colour permission")]
            public string permRandomColour = "colourednames.rndcolour";
            [JsonProperty(PropertyName = "Use Blacklist")]
            public bool UseBlacklist = true;
            [JsonProperty(PropertyName = "Rainbow Colours")]
            public string[] rainbowColours = { "#ff0000", "#ffa500", "#ffff00", "#008000", "#0000ff", "#4b0082", "#ee82ee" };
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
            permission.RegisterPermission(config.permShow, this);
            permission.RegisterPermission(config.permRainbow, this);
            permission.RegisterPermission(config.permGradient, this);
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
            
            if ((!allColourData.ContainsKey(player.UserIDString) && !HasRainbow(player.IPlayer)) || !HasShowPerm(player.IPlayer)) return null;
            if (!allColourData.ContainsKey(player.UserIDString) && HasRainbow(player.IPlayer)) allColourData.Add(player.UserIDString, ProcessGradientName(player.displayName, config.rainbowColours));

            if (Chat.serverlog)
            {
                object[] objArray = new object[] { ConsoleColor.DarkYellow, null, null, null };
                objArray[1] = string.Concat(new object[] { "[", channel, "] ", player.displayName.EscapeRichText(), ": " });
                objArray[2] = ConsoleColor.DarkGreen;
                objArray[3] = message;
                ServerConsole.PrintColoured(objArray);
            }
            string formattedMsg = GetMessage("RconLogFormat", player.IPlayer, player.displayName.EscapeRichText(), player.UserIDString);

            bool isGradient = false;
            string color = allColourData[player.UserIDString];
            if (color.Contains("<color=#")) isGradient = true;

            string playerUserName = isGradient ? allColourData[player.UserIDString] : player.displayName;
            string playerColour = isGradient ? "#5af" : allColourData[player.UserIDString];

            var colouredMessage = new ColouredNamesMessage(player.IPlayer, playerUserName, playerColour, message);
            var colouredMessageDict = colouredMessage.ToDictionary();

            foreach (Plugin plugin in plugins.GetAll())
            {
                object obj = Interface.Oxide.CallHook("OnColouredNames", colouredMessageDict);

                if (obj is Dictionary<string, object>)
                {
                    try
                    {
                        colouredMessageDict = obj as Dictionary<string, object>;
                    }
                    catch (Exception e)
                    {
                        PrintError($"Failed to load modified OnBetterChat hook data from plugin '{plugin.Title} ({plugin.Version})':{Environment.NewLine}{e}");
                        continue;
                    }
                }
                else if (obj != null) return null;
            }

            colouredMessage = ColouredNamesMessage.FromDictionary(colouredMessageDict);

            if (channel == Chat.ChatChannel.Global)
            {
                foreach (BasePlayer Player in BasePlayer.activePlayerList)
                {
                    Player.SendConsoleCommand("chat.add2", 0, player.userID, colouredMessage.Message, colouredMessage.Name, colouredMessage.Colour);
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
                        member.SendConsoleCommand("chat.add2", 1, player.userID, colouredMessage.Message, colouredMessage.Name, colouredMessage.Colour);
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
                IPlayer player = dict["Player"] as IPlayer;
                if (!allColourData.ContainsKey(player.Id) && !HasRainbow(player) || !HasShowPerm(player)) return dict;
                if (!allColourData.ContainsKey(player.Id) && HasRainbow(player)) allColourData.Add(player.Id, ProcessGradientName(player.Name, config.rainbowColours));

                bool isGradient = false;
                string color = allColourData[player.Id];
                if (color.Contains("<color=#")) isGradient = true;

                if (isGradient) dict["Username"] = allColourData[player.Id];
                if (!isGradient) ((Dictionary<string, object>)dict["UsernameSettings"])["Color"] = allColourData[player.Id];
            }
            return dict;
        }
        #endregion

        #region Commands
        void cmdColourCommand(IPlayer player, string cmd, string[] args)
        {
            if (args.Length < 1)
            {
                player.Reply(GetMessage("IncorrectUsage", player, config.ColourCommand));
                return;
            }
            string colLower = string.Empty;
            if (args[0] == "set" || player.IsServer)
            {
                if (!CanSetOthers(player))
                {
                    player.Reply(GetMessage("NoPermissionSetOthers", player));
                    return;
                }
                if (args.Length < 3)
                {
                    player.Reply(GetMessage("IncorrectSetUsage", player, config.ColourCommand));
                    return;
                }
                IPlayer target = covalence.Players.FindPlayer(args[1]);
                if (target == null)
                {
                    player.Reply(GetMessage("PlayerNotFound", player, args[1]));
                    return;
                }             
                colLower = args[2].ToLower();
                ProcessColour(player, target, colLower, args.Skip(2).ToArray());
            }
            else
            {
                if (!HasPerm(player))
                {
                    player.Reply(GetMessage("NoPermission", player));
                    return;
                }
                if (args.Length < 1)
                {
                    player.Reply(GetMessage("IncorrectUsage", player, config.ColourCommand));
                    return;
                }
                colLower = args[0].ToLower();
                ProcessColour(player, player, colLower, args.Skip(1).ToArray());
            }
        }
        void cmdColoursCommand(IPlayer player, string cmd, string[] args)
        {
            if (!HasPerm(player))
            {
                player.Reply(GetMessage("NoPermission", player));
                return;
            }
            string additionalInfo = string.Empty;

            if (config.UseWhitelist)
            {
                additionalInfo += "Whitelisted Colours:\n";
                foreach (string colour in config.whitelistedColoursHex)
                {
                    additionalInfo += "- " + colour + "\n";
                }
            }
            else if (config.UseBlacklist)
            {
                additionalInfo += "Blacklisted Colours:\n";
                foreach (string colour in config.blockColoursHex)
                {
                    additionalInfo += "- " + colour + "\n";
                }
            }
            player.Reply(GetMessage("ColoursInfo", player, additionalInfo));
        }
        #endregion

        #region Helpers
        bool BetterChatIns() => (BetterChat != null && BetterChat.IsLoaded);
        bool BetterChatMuteIns() => (BetterChatMute != null && BetterChatMute.IsLoaded);
        bool IsValidColour(string input) => Regex.Match(input, colourRegex).Success;

        string GetMessage(string key, IPlayer player, params string[] args) => String.Format(lang.GetMessage(key, this, player.Id), args);
        bool HasShowPerm(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.permShow));
        bool HasPerm(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.permUse));
        bool HasRainbow(IPlayer player) => (permission.UserHasPermission(player.Id, config.permRainbow));
        bool CanGradient(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.permGradient));
        bool CanBypass(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.permBypass));
        bool CanSetOthers(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.permSetOthers));
        bool CanRandomColour(IPlayer player) => (player.IsAdmin || permission.UserHasPermission(player.Id, config.permRandomColour));

        bool IsValid(string input)
        {
            if (config.UseBlacklist) return !config.blockColoursHex.Any(x => (input == x));
            else if (config.UseWhitelist) return config.whitelistedColoursHex.Any(x => (input == x));
            return true;
        }
        void ProcessColour(IPlayer player, IPlayer target, string colLower, string[] colours)
        {
            bool isCalledOnto = false;
            if (player != target) isCalledOnto = true;

            if (colLower == "gradient")
            {
                if (!CanGradient(player))
                {
                    player.Reply(GetMessage("NoPermissionGradient", player));
                    return;
                }
                if (colours.Length < 2)
                {
                    player.Reply(GetMessage("IncorrectGradientUsageArgs", player));
                    return;
                }
                string gradientName = ProcessGradientName(target.Name, colours);
                if (gradientName.Equals(string.Empty))
                {
                    player.Reply(GetMessage("IncorrectGradientUsage", player, config.ColourCommand));
                    return;
                }
                allColourData[target.Id] = gradientName;
                target.Reply(GetMessage("GradientChanged", target, gradientName));
                if (isCalledOnto) player.Reply(GetMessage("GradientChangedFor", player, target.Name, gradientName));
                return;
            }
            if (colLower == "reset" || colLower == "clear" || colLower == "remove")
            {
                allColourData.Remove(target.Id);
                SaveData();
                target.Reply(GetMessage("ColourRemoved", target));
                if (isCalledOnto) player.Reply(GetMessage("ColourRemovedFor", player, target.Name));
                return;
            }
            if (colLower == "random" && CanRandomColour(player))
            {
                colLower = GetRndColour();
                ChangeColour(target, colLower);
                target.Reply(GetMessage("RndColour", target, colLower));
                if (isCalledOnto) player.Reply(GetMessage("RndColourFor", player, target.Name, colLower));
                return;
            }
            string invalidChar;
            if ((invalidChar = IsInvalidCharacter(colLower)) != null && !CanBypass(player))
            {
                player.Reply(GetMessage("InvalidCharacters", player, invalidChar));
                return;
            }
            if (!IsValidColour(colLower))
            {
                player.Reply(GetMessage("InvalidColour", player));
                return;
            }
            if (!IsValid(colLower) && !CanBypass(player))
            {
                player.Reply(GetMessage("InvalidColour", player));
                return;
            }
            ChangeColour(target, colLower);

            target.Reply(GetMessage("ColourChanged", target, colLower));
            if (isCalledOnto) player.Reply(GetMessage("ColourChangedFor", player, target.Name, colLower));
        }
        public string ProcessGradientName(string name, string[] colourArgs)
        {
            colourArgs = colourArgs.Where(col => IsValid(col) && IsValidColour(col) && (IsInvalidCharacter(col) == null ? true : false)).ToArray();

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
        public List<Color> GetGradients(Color start, Color end, int steps)
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
        #endregion

        #region API
        //Code used based on BetterChat by LaserHydra
        public class ColouredNamesMessage
        {
            public IPlayer Player;
            public string Name;
            public string Colour;
            public string Message;

            public ColouredNamesMessage(IPlayer player, string name, string colour, string message)
            {
                Player = player;
                Name = name;
                Colour = colour;
                Message = message;
            }

            public ColouredNamesMessage()
            {

            }

            public Dictionary<string, object> ToDictionary() => new Dictionary<string, object>
            {
                [nameof(Player)] = Player,
                [nameof(Name)] = Name,
                [nameof(Colour)] = Colour,
                [nameof(Message)] = Message
            };

            public static ColouredNamesMessage FromDictionary(Dictionary<string, object> dict)
            {
                return new ColouredNamesMessage()
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

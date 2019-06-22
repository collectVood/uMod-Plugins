using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using System.Text.RegularExpressions;

//Reference: System.Drawing

namespace Oxide.Plugins
{
    [Info("ColouredNames", "collect_vood & PsychoTea", "1.3.2", ResourceId = 1362)]
    [Description("Allows players to change their name colour in chat.")]

    class ColouredNames : RustPlugin
    {
        [PluginReference] Plugin BetterChat;

        const string permUse = "colourednames.use";
        const string permBypass = "colourednames.bypass";
        const string permSetOthers = "colourednames.setothers";
        const string colourRegex = "^#(?:[0-9a-fA-f]{3}){1,2}$";
        readonly string[] blockedValues = { "{", "}", "size" };

        Dictionary<ulong, string> colour = new Dictionary<ulong, string>();
        List<string> blockedColours = new List<string>();
        List<string> supportedColours = new List<string> { "black", "blue", "green", "orange", "purple", "red", "white", "yellow" };
        bool allowHexcode;

        #region Hooks

        void Init()
        {
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permBypass, this);
            permission.RegisterPermission(permSetOthers, this);

            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "NoPermission", "You don't have permission to use this." },
                { "NoPermissionSetOthers", "You don't have permission to set other players' colours." },
                { "IncorrectUsage", "<color=#00AAFF>Incorrect usage!</color><color=#A8A8A8> /colour {{colour}} [player]\nFor a list of colours do /colours</color>" },
                { "PlayerNotFound", "Player {0} was not found." },
                { "SizeBlocked", "You may not try and change your size! You sneaky player..." },
                { "HexcodeBlocked", "<color=#00AAFF>ColouredNames: </color><color=#A8A8A8>Hexcode colour codes have been disabled.</color>" },
                { "InvalidCharacters", "The character '{0}' is not allowed in colours. Please remove it." },
                { "ColourBlocked", "<color=#00AAFF>ColouredNames: </color><color=#A8A8A8>That colour is blocked.</color>" },
                { "ColourRemoved", "<color=#00AAFF>ColouredNames: </color><color=#A8A8A8>Name colour removed!</color>" },
                { "ColourChanged", "<color=#00AAFF>ColouredNames: </color><color=#A8A8A8>Name colour changed to </color><color={0}>{0}</color><color=#A8A8A8>!</color>" },
                { "ColourChangedFor", "<color=#00AAFF>ColouredNames: </color><color=#A8A8A8>{0}'s name colour changed to </color><color={1}>{1}</color><color=#A8A8A8>!</color>" },
                { "ChatMessage", "<color={0}>{1}</color>: {2}" },
                { "LogInfo", "[CHAT] {0}[{1}/{2}] : {3}" },
                { "ColoursInfo", "<color=#00AAFF>ColouredNames</color><color=#A8A8A8>\nSupported colours:\n<color=black>black<color=#A8A8A8>, <color=blue>blue<color=#A8A8A8>, <color=green>green<color=#A8A8A8>, <color=orange>orange<color=#A8A8A8>, <color=purple>purple<color=#A8A8A8>, <color=red>red<color=#A8A8A8>, <color=white>white<color=#A8A8A8>, and <color=yellow>yellow<color=#A8A8A8>.\nOr you may use any hexcode (if enabled), eg '</color><color=#FFFF00>#FFFF00</color><color=#A8A8A8>'\nTo remove your colour, use 'clear' or 'remove'\nAn invalid colour will default to </color>white<color=#A8A8A8></color>" },
                { "CantUseClientside", "You may not use this command from ingame - server cosole only." },
                { "ConsoleColourIncorrectUsage", "Incorrect usage! colour {{userid}} {{colour}}" },
                { "InvalidIDConsole", "Error! {0} is not a SteamID!" },
                { "ConsoleColourChanged", "Colour of {0} changed to {1}." },
                { "InvalidColour", "That colour is not valid. Do /colours for more information on valid colours." }
            }, this);

            ReadData();

            allowHexcode = GetConfig<bool>("AllowHexcode");
            foreach (var obj in GetConfig<List<object>>("BlockedColours"))
                blockedColours.Add(obj.ToString().ToLower());
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file.");

            Config["AllowHexcode"] = true;
            Config["BlockedColours"] = new List<string>() { "#000000", "black" };

            Puts("New config file generated.");
        }

        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (BetterChatIns()) return null;

            BasePlayer player = (BasePlayer)arg.Connection.player;

            if (!colour.ContainsKey(player.userID)) return null;

            string argMsg = arg.GetString(0, "text");
            string message = GetMessage("ChatMessage", player, colour[player.userID], player.displayName, argMsg);

            Server.Broadcast(message, player.userID);

            Interface.Oxide.LogInfo(GetMessage("LogInfo", player, player.displayName, player.net.ID.ToString(), player.UserIDString, argMsg));
            return true;
        }

        Dictionary<string, object> OnBetterChat(Dictionary<string, object> dict)
        {
            ulong userId = ulong.Parse((dict["Player"] as IPlayer).Id);
            if (!colour.ContainsKey(userId)) return dict;
            ((Dictionary<string, object>)dict["UsernameSettings"])["Color"] = colour[userId];
            return dict;
        }

        #endregion

        #region Chat Commands

        [ChatCommand("colour")]
        void colourCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player))
            {
                player.ChatMessage(GetMessage("NoPermission", player));
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage(GetMessage("IncorrectUsage", player));
                return;
            }

            string colLower = args[0].ToLower();

            if (colLower == "clear" || colLower == "remove")
            {
                colour.Remove(player.userID);
                SaveData();
                player.ChatMessage(GetMessage("ColourRemoved", player));
                return;
            }

            var invalid = CheckInvalids(colLower);
            if (invalid != "")
            {
                player.ChatMessage(GetMessage("InvalidCharacters", player, invalid));
                return;
            }

            if (!CanBypass(player))
            {
                if (!allowHexcode && args[0].Contains("#"))
                {
                    player.ChatMessage(GetMessage("HexcodeBlocked", player));
                    return;
                }

                if (blockedColours.Where(x => x == colLower).Any())
                {
                    player.ChatMessage(GetMessage("ColourBlocked", player));
                    return;
                }
            }

            if (!IsValidColour(args[0]))
            {
                player.ChatMessage(GetMessage("InvalidColour", player));
                return;
            }

            if (args.Length > 1)
            {
                if (!CanSetOthers(player))
                {
                    player.ChatMessage(GetMessage("NoPermissionSetOthers", player));
                    return;
                }

                BasePlayer target = rust.FindPlayerByName(args[1]);
                if (target == null)
                {
                    player.ChatMessage(GetMessage("PlayerNotFound", player, args[1]));
                    return;
                }

                ChangeColour(target, args[0]);
                player.ChatMessage(GetMessage("ColourChangedFor", player, target.displayName, args[0]));
                return;
            }

            ChangeColour(player, args[0]);
            player.ChatMessage(GetMessage("ColourChanged", player, args[0]));
        }

        [ChatCommand("colours")]
        void coloursCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPerm(player))
            {
                PrintToChat(player, GetMessage("NoPermission", player));
                return;
            }

            PrintToChat(player, GetMessage("ColoursInfo", player));
        }

        #endregion

        #region Console Commands

        [ConsoleCommand("colour")]
        void colourConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                arg.ReplyWith(GetConsoleMessage("NoPermission"));
                return;
            }

            string[] args = (arg.Args == null) ? new string[] { } : arg.Args;

            if (args.Length < 2)
            {
                arg.ReplyWith(GetConsoleMessage("ConsoleColourIncorrectUsage"));
                return;
            }

            ulong userId;
            if (!ulong.TryParse(args[0], out userId))
            {
                arg.ReplyWith(GetConsoleMessage("InvalidIDConsole", args[0]));
                return;
            }

            ChangeColour(userId, args[1]);
            string name = (BasePlayer.FindByID(userId)?.displayName ?? args[0]);
            arg.ReplyWith(GetConsoleMessage("ConsoleColourChanged", name, args[1]));
        }

        [ConsoleCommand("viewcolours")]
        void viewColoursCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                arg.ReplyWith(GetConsoleMessage("NoPermission"));
                return;
            }

            List<string> hexcode = new List<string>();
            List<string> others = new List<string>();

            foreach (var kvp in colour)
            {
                if (kvp.Value.ToArray()[0] == '#') hexcode.Add($"{kvp.Key}: {kvp.Value}");
                else others.Add($"{kvp.Key}: {kvp.Value}");
            }

            string message = "";

            float i = 1;
            foreach (var str in hexcode)
            {
                message += str;
                if (i % 3 == 0) message += "\n";
                else message += "       ";
                i++;
            }

            message += "\n";

            i = 1;
            foreach (var str in others)
            {
                message += str;
                if (i % 3 == 0) message += "\n";
                else message += "       ";
                i++;
            }

            arg.ReplyWith(message);
        }

        #endregion

        #region Helpers

        bool IsValidColour(string input) => Regex.Match(input, colourRegex).Success || supportedColours.Contains(input);

        bool BetterChatIns() => (BetterChat != null);

        void ChangeColour(BasePlayer target, string newColour)
        {
            if (!colour.ContainsKey(target.userID)) colour.Add(target.userID, "");
            colour[target.userID] = newColour;
            SaveData();
        }
        void ChangeColour(ulong userId, string newColour) => ChangeColour(new BasePlayer() { userID = userId }, newColour);

        bool HasPerm(BasePlayer player) => (player.IsAdmin || permission.UserHasPermission(player.UserIDString, permUse));
        bool CanBypass(BasePlayer player) => (player.IsAdmin || permission.UserHasPermission(player.UserIDString, permBypass));
        bool CanSetOthers(BasePlayer player) => (player.IsAdmin || permission.UserHasPermission(player.UserIDString, permSetOthers));

        string CheckInvalids(string input) => (blockedValues.Where(x => input.Contains(x)).FirstOrDefault()) ?? string.Empty;

        string GetMessage(string key, BasePlayer player, params string[] args) => String.Format(lang.GetMessage(key, this, player.UserIDString), args);
        string GetConsoleMessage(string key, params string[] args) => GetMessage(key, new BasePlayer() { userID = 0 }, args);

        T GetConfig<T>(string key) => (T)Config[key];

        void ReadData() => colour = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, string>>(this.Title);
        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(this.Title, colour);
        
        #endregion
    }
}
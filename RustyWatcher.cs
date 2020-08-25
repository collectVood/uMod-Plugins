using CompanionServer;
using ConVar;
using Facepunch;
using Facepunch.Math;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Rusty Watcher", "collect_vood", "1.0.0")]
    [Description("Connects your discord with rust")]
    public class RustyWatcher : CovalencePlugin
    {
        [PluginReference]
        private Plugin ColouredChat, BetterChat, BetterChatMute;

        #region Configuration  

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Ingame Discord Tag")]
            public string DiscordTag = "<color=#20B2AA>[Discord]</color>";
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            _config = new Configuration();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Custom Packets

        public struct MessageContent
        {
            [JsonProperty("Channel")]
            public int Channel;
            [JsonProperty("Message")]
            public string Message;
            [JsonProperty("UserId")]
            public ulong UserID;
            [JsonProperty("Username")]
            public string Username;
            [JsonProperty("Color")]
            public string Color;
        }

        #endregion

        #region Methods

        private void DiscordBroadcast(object obj)
            => RCon.Broadcast(RCon.LogType.Chat, obj);

        #endregion

        #region Commands      

        [Command("discordsay")]
        private void CommandDiscordSay(IPlayer iPlayer, string cmd, string[] args)
        {
            if (!iPlayer.IsServer) 
                return;

            string argString = string.Join(" ", args);
            MessageContent msg = JsonConvert.DeserializeObject<MessageContent>(argString);
            
            IPlayer sender = players.FindPlayerById(msg.UserID.ToString());            
            if (sender != null)
            {
                string message = msg.Message;
                if (ColouredChat != null && ColouredChat.IsLoaded)
                {
                    message = ColouredChat.Call("API_GetColouredChatMessage", sender, Chat.ChatChannel.Global, msg.Message) as string;
                }
                else if (BetterChat != null && BetterChat.IsLoaded)
                {
                    Dictionary<string, object> betterChatMessageData = BetterChat.CallHook("API_GetMessageData", sender, message) as Dictionary<string, object>;
                    message = BetterChat.CallHook("API_GetFormattedMessageFromDict", betterChatMessageData) as string;                   
                }
                else
                {
                    var username = sender.Name;
                    if (sender.IsAdmin)
                    {
                        username = "<color=#af5>" + username + "</color>";
                    }
                    else
                    {
                        username = "<color=#5af>" + username + "</color>";
                    }

                    message = username + ": " + message;
                }

                message = _config.DiscordTag + " " + message;

                var obj = new object[]
                {
                    (int)Chat.ChatChannel.Global,
                    msg.UserID,
                    message
                };

                ConsoleNetwork.BroadcastToAllClients("chat.add", obj);
            }
            else
            {
                var obj = new object[]
                {
                    (int)Chat.ChatChannel.Global,
                    msg.UserID, msg.Message,
                    _config.DiscordTag + " " + msg.Username,
                    msg.Color
                };

                ConsoleNetwork.BroadcastToAllClients("chat.add2", obj);
            }

            var chatentry = new Chat.ChatEntry
            {
                Channel = 0,
                Message = msg.Message,
                UserId = msg.UserID.ToString(),
                Username = msg.Username,
                Color = msg.Color,
                Time = Facepunch.Math.Epoch.Current
            };

            DiscordBroadcast(chatentry);
        }

        #endregion
    }
}

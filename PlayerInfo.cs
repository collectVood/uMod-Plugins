using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Player Info", "collect_vood", "1.0.0")]
    [Description("Shows a count of all online players")]

    class PlayerInfo : CovalencePlugin
    {
        #region Constants
        private const string permAllow = "playerinfo.allow";
        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "PlayerInfo", " <color=#ff686b>{0}</color>/<color=#ff686b>{1}</color> player(s) online{2} {3}" },
                { "Joining", ", <color=#ff686b>{0}</color> joining"},
                { "Queued", "(<color=#ff686b>{0}</color> queued)"},
                { "MissingPerm", "You are not allowed to use this command"}
            }, this);
        }
        #endregion

        #region Config
        private Configuration config;
        private class Configuration
        {
            [JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Commands = new List<string>
            {
                "online", "players"
            };
            [JsonProperty(PropertyName = "Count in admins")]
            public bool countAdmins = true;
            [JsonProperty(PropertyName = "Show joining")]
            public bool showJoining = true;
            [JsonProperty(PropertyName = "Show queue")]
            public bool showQueue = true;
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

        #region Hooks
        private void Init()
        {
            permission.RegisterPermission(permAllow, this);
            foreach (var command in config.Commands)
                AddCovalenceCommand(command, nameof(PlayerInfoCommand));
        }
        #endregion

        #region Command
        void PlayerInfoCommand(IPlayer player, string cmd, string[] args)
        {
            if (!HasPermission(player))
            {
                player.Reply(GetMessage("MissingPerm", player));
                return;
            }
            string joining = "";
            if (config.showJoining)
            {
                if (ServerMgr.Instance.connectionQueue.Joining > 0)
                    joining = GetMessage("Joining", player, ServerMgr.Instance.connectionQueue.Joining.ToString());
            }
            string queue = "";
            if (config.showQueue)
            {
                if (ServerMgr.Instance.connectionQueue.Queued > 0)
                    queue = GetMessage("Queued", player, ServerMgr.Instance.connectionQueue.Queued.ToString());
            }
            player.Reply(GetMessage("PlayerInfo", player, server.Players.ToString(), server.MaxPlayers.ToString(), joining, queue));
        }
        #endregion

        #region Helpers
        string GetMessage(string key, IPlayer player, params string[] args) => String.Format(lang.GetMessage(key, this, player.Id), args);
        private bool HasPermission(IPlayer player) => (permission.UserHasPermission(player.Id, permAllow));
        #endregion
    }
}
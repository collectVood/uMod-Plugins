using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Limited Moderator", "collect_vood", "1.0.0")]
    [Description("Allows blocking of certain commands for moderators")]
    class LimitedModerator : CovalencePlugin
    {

        #region Config     

        private Configuration config;
        private class Configuration
        {
            [JsonProperty(PropertyName = "Blocked Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> blockedCommands = new List<string> {
                "ownerid", "moderatorid", "removeowner", "removemoderator"
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

        #region Hooks

        object OnServerCommand(ConsoleSystem.Arg args)
        {
            if (config.blockedCommands.Contains(args.cmd.Name) && args.Connection?.authLevel < 2)
            {
                var player = args.Player();
                if (player != null) player.ConsoleMessage("You are not allowed to use this command.");
                return true;
            }
            return null;
        }

        #endregion

    }
}

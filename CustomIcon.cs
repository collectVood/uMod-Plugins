using Network;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Custom Icon", "collect_vood", "1.0.1")]
    [Description("Set a customizable icon for all non user messages")]

    class CustomIcon : CovalencePlugin
    {
        #region Config
        private Configuration config;
        private class Configuration
        {
            [JsonProperty(PropertyName = "Steam Avatar User ID")]
            public ulong SteamAvatarUserID = 0;
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
        object[] OnBroadcastCommand(string command, object[] args)
        {
            if (args != null && config != null)
            {
                if (args.Length > 0 && command == "chat.add")
                {
                    if (args[0].ToString() == "0")
                    {
                        args[0] = config.SteamAvatarUserID;
                    }
                }
            }
            return args;
        }
        object[] OnSendCommand(Connection connection, string command, params object[] args)
        {
            if (args != null && config != null)
            {
                if (args.Length > 0 && command == "chat.add")
                {
                    if (args[0].ToString() == "0")
                    {
                        args[0] = config.SteamAvatarUserID;
                    }
                }
            }
            return args;
        }
        #endregion
    }
}
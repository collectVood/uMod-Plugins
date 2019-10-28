using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core.Libraries;
namespace Oxide.Plugins
{
    [Info("Update Notify", "collect_vood", "0.0.1")]
    [Description("Get informed once a plugin is able to be updated")]

    class UpdateNotify : RustPlugin
    {
        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "Update", "A new version for {0} is available. Current: {1} // New: {2}" },
                { "No response", "Error: {0} - Couldn't get any response. Plugin: {1}"}
            }, this);
        }
        #endregion

        #region Config       
        private Configuration config;
        private class Configuration
        {
            [JsonProperty(PropertyName = "Refresh time (seconds)")]
            public float RefreshTimeSeconds = 3600f;
            [JsonProperty(PropertyName = "Discord Webhook Channel Link")]
            public string DiscordWebhook = "";
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

        #region Class - UpdateInfo
        private class UpdateInfo
        {
            public string latest_release_version { get; set; }
        }
        #endregion

        #region Hooks
        private void Init()
        {
            SendToDiscord("Ok");
            //ScheduledCheck();
        }
        private void ScheduledCheck()
        {
            CheckPlugins();
            timer.Every(config.RefreshTimeSeconds, () =>
            {
                CheckPlugins();
            });
        }
        #endregion

        #region Methods
        private void CheckPlugins()
        {
            foreach (var plugin in plugins.GetAll())
            {
                if (plugin.Name == "RustCore" || plugin.Name == "UnityCore")
                    continue;
                string apiLink = "https://umod.org/plugins/" + InsertMinBeforeUpperCase(plugin.Name) + ".json";
                Puts(apiLink);
                GetRequest(apiLink, plugin);
            }
        }
        private void GetRequest(string apiLink, Core.Plugins.Plugin plugin)
        {
            webrequest.Enqueue(apiLink, null, (code, response) =>
                GetCallback(code, response, plugin), this, RequestMethod.GET);
        }
        private void GetCallback(int code, string response, Core.Plugins.Plugin plugin)
        {
            if (response == null || code != 200)
            {
                PrintError(GetMessage("No response", code.ToString(), plugin.Name));
                return;
            }
            PrintWarning(response);
            var updateInfo = JsonConvert.DeserializeObject<UpdateInfo>(response);
            if (updateInfo.latest_release_version != plugin.Version.ToString())
            {
                string message = GetMessage("Update", plugin.Name, plugin.Version.ToString(), updateInfo.latest_release_version);
                PrintWarning(message);
                SendToDiscord(message);
                foreach (var player in Player.Players)
                {
                    if (player.IsAdmin)
                        player.ChatMessage(message);
                }
            }
            return;
        }
        private static string InsertMinBeforeUpperCase(string str)
        {
            var sb = new StringBuilder();
            char previousChar = char.MinValue;       
            foreach (char c in str)
            {
                if (char.IsUpper(c))
                {
                    if (sb.Length != 0 && previousChar != '-')
                        sb.Append('-');
                }
                sb.Append(c);
                previousChar = c;
            }
            return sb.ToString();
        }
        private void SendToDiscord(string message)
        {
            webrequest.Enqueue(config.DiscordWebhook, message, (code, response) =>
            {
                if (code != 200 || response == null)
                {
                    PrintWarning(GetMessage("No response", code.ToString(), "Discord Webhook", response));
                    return;
                }
            }, this, RequestMethod.POST);
        }
        string GetMessage(string key, params string[] args) => System.String.Format(lang.GetMessage(key, this), args);
        #endregion
    }
}
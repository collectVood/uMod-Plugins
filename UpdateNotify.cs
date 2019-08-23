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
        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "Update", "A new version for {0} is available. Current: {1} // New: {2}" },
                { "No response", "Error: {0} - Couldn't get any response. Plugin: {1}"}
            }, this);
        }
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
            ScheduledCheck();
        }
        private void ScheduledCheck()
        {
            CheckPlugins();
            timer.Every(3600f, () =>
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
                PrintError(lang.GetMessage("No response", this), code, plugin.Name);
                return;
            }
            var updateInfo = JsonConvert.DeserializeObject<UpdateInfo>(response);
            if (updateInfo.latest_release_version != plugin.Version.ToString())
            {
                PrintWarning(lang.GetMessage("Update", this), plugin.Name, plugin.Version, updateInfo.latest_release_version);
                foreach (var player in Player.Players)
                {
                    if (player.IsAdmin)
                        player.IPlayer.Reply(lang.GetMessage("Update", this, player.UserIDString), plugin.Name, plugin.Version, updateInfo.latest_release_version);
                }
            }
            return;
        }
        public static string InsertMinBeforeUpperCase(string str)
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
        #endregion
    }
}
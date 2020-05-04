using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Better Chat Mute Voice", "collect_vood", "1.0.2")]
    [Description("Adds voice mute to better chat muted players")]
    public class BetterChatMuteVoice : CovalencePlugin
    {
        [PluginReference]
        private Plugin BetterChatMute;

        #region Variables

        public List<string> MuteCache;

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            var muteList = BetterChatMute?.Call("API_GetMuteList") as List<string>;

            MuteCache = new List<string>(muteList);
        }

        private object OnPlayerVoice(BasePlayer player)
        {
            if (MuteCache.Contains(player.UserIDString)) return true;

            return null;
        }

        #region BetterChatMute Hooks

        private void OnBetterChatMuted(IPlayer target, IPlayer initiator, string reason) => HandleAddMute(target.Id);

        private void OnBetterChatTimeMuted(IPlayer target, IPlayer initiator, TimeSpan timeSpan, string reason) => HandleAddMute(target.Id);

        private void OnBetterChatUnmuted(IPlayer target, IPlayer initiator) => HandleRemoveMute(target.Id);

        private void OnBetterChatMuteExpired(IPlayer player) => HandleRemoveMute(player.Id);

        #endregion

        #endregion

        #region Methods

        private void HandleRemoveMute(string playerId)
        {
            if (!MuteCache.Contains(playerId)) return;

            MuteCache.Remove(playerId);
        }        
        
        private void HandleAddMute(string playerId)
        {
            if (MuteCache.Contains(playerId)) return;

            MuteCache.Add(playerId);
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Better Chat Mute Voice", "collect_vood", "1.0.4")]
    [Description("Adds voice mute to better chat muted players")]
    public class BetterChatMuteVoice : CovalencePlugin
    {
        [PluginReference]
        private Plugin BetterChatMute;

        #region Variables

        public HashSet<string> MuteCache = new HashSet<string>();

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            if (BetterChatMute == null || !BetterChatMute.IsLoaded)
            {
                PrintWarning("BetterChatMute is required for this plugin to work.");
            }
            else
            {
                var muteList = BetterChatMute.Call("API_GetMuteList") as List<string>;
                MuteCache = new HashSet<string>(muteList);
            }


            if (MuteCache.Count < 1) Unsubscribe(nameof(OnPlayerVoice));
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
            MuteCache.Remove(playerId);

            if (MuteCache.Count < 1) Unsubscribe(nameof(OnPlayerVoice));
        }        
        
        private void HandleAddMute(string playerId)
        {
            MuteCache.Add(playerId);

            if (MuteCache.Count == 1) Subscribe(nameof(OnPlayerVoice));
        }

        #endregion
    }
}

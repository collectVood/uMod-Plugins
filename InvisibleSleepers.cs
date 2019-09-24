using Network;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Collections.Generic;
namespace Oxide.Plugins
{
    [Info("Invisible Sleepers", "collect_vood", "1.0.4")]
    [Description("Makes all sleepers invisible")]
    class InvisibleSleepers : RustPlugin
    {
        [PluginReference]
        private Plugin Friends, Clans;

        #region Constants
        const string permAllow = "invisiblesleepers.allow";
        const string permBypass = "invisiblesleepers.bypass";
        #endregion

        #region Config
        private Configuration config;
        private class Configuration
        {
            [JsonProperty(PropertyName = "Show clan members")]
            public bool showClan = false;
            [JsonProperty(PropertyName = "Show team members")]
            public bool showTeam = false;
            [JsonProperty(PropertyName = "Show friends")]
            public bool showFriends = false;
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
        void Init()
        {
            permission.RegisterPermission(permAllow, this);
            permission.RegisterPermission(permBypass, this);

            foreach (BasePlayer player in BasePlayer.sleepingPlayerList)
            {
                if (player != null && HasPerm(player)) HideSleeper(player);
            }
        }
        //Credit: most code taken from the vanish plugin made by Wulf
        private object CanNetworkTo(BaseNetworkable entity, BasePlayer target)
        {           
            var player = entity as BasePlayer ?? (entity as HeldEntity)?.GetOwnerPlayer();
            if (player == null || target == null || player == target)
                return null;
            if (player.IsSleeping() && HasPerm(player))
            {
                if (CanBypass(target, player))
                    return null;
                return false;
            }
            return null;
        }
        private object CanBeTargeted(BaseCombatEntity entity)
        {
            BasePlayer player = entity as BasePlayer;
            if (player == null)
                return null;
            if (player.IsSleeping() && HasPerm(player))
                return false;
            return null;
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player != null && HasPerm(player)) HideSleeper(player);
        }
        void HideSleeper(BasePlayer player)
        {
            List<Connection> connections = new List<Connection>();
            foreach (BasePlayer target in BasePlayer.activePlayerList)
            {
                if (player == target || !target.IsConnected || CanBypass(target, player))
                    continue;
                connections.Add(target.net.connection);
            }
            HeldEntity heldEntity = player.GetHeldEntity();
            if (heldEntity != null)
            {
                heldEntity.SetHeld(false);
                heldEntity.UpdateVisiblity_Invis();
                heldEntity.SendNetworkUpdate();
            }

            if (Net.sv.write.Start())
            {
                Net.sv.write.PacketID(Message.Type.EntityDestroy);
                Net.sv.write.EntityID(player.net.ID);
                Net.sv.write.UInt8((byte)BaseNetworkable.DestroyMode.None);
                Net.sv.write.Send(new SendInfo(connections));
            }
            player.UpdatePlayerCollider(false);
        }
        #endregion

        #region Helpers
        private bool HasPerm(BasePlayer player) => permission.UserHasPermission(player.UserIDString, permAllow);
        private bool CanBypass(BasePlayer player, BasePlayer target)
        {

            if (permission.UserHasPermission(player.UserIDString, permBypass) || config.showTeam && IsTeamMember(player, target) || config.showFriends && IsFriend(player, target) || config.showClan && IsClanMember(player, target))
                return true;
            return false;
        }
        bool IsClanMember(BasePlayer player, BasePlayer target)
        {
            if (Clans == null || !Clans.IsLoaded)
                return false;

            var playerClan = Clans.Call<string>("GetClanOf", player);
            var otherPlayerClan = Clans.Call<string>("GetClanOf", target);
            if (playerClan == null || otherPlayerClan == null)
                return false;
            return playerClan == otherPlayerClan;
        }
        bool IsTeamMember(BasePlayer player, BasePlayer target)
        {
            var team = RelationshipManager.Instance.FindTeam(player.currentTeam);
            if (team == null)
                return false;
            return (team.members.Contains(target.userID));

        }
        bool IsFriend(BasePlayer player, BasePlayer target)
        {
            if (Friends == null || !Friends.IsLoaded)
                return false;

            return Friends.Call<bool>("AreFriendsS", player.UserIDString, target.UserIDString);
        }
        #endregion
    }
}
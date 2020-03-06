using Network;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Invisible Sleepers", "collect_vood", "1.0.9")]
    [Description("Makes all sleepers invisible")]
    public class InvisibleSleepers : RustPlugin
    {
        [PluginReference]
        private Plugin Friends, Clans;

        #region Constants

        const string permAllow = "invisiblesleepers.allow";
        const string permBypass = "invisiblesleepers.bypass";

        #endregion

        #region Config

        private ConfigurationFile Configuration;
        private class ConfigurationFile
        {
            [JsonProperty(PropertyName = "Performance Mode")]
            public bool PerformanceMode = false;
            [JsonProperty(PropertyName = "Show clan members")]
            public bool ShowClan = false;
            [JsonProperty(PropertyName = "Show team members")]
            public bool ShowTeam = false;
            [JsonProperty(PropertyName = "Show friends")]
            public bool ShowFriends = false;
        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Configuration = new ConfigurationFile();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            Configuration = Config.ReadObject<ConfigurationFile>();
            SaveConfig();
        }
        protected override void SaveConfig() => Config.WriteObject(Configuration);

        #endregion

        #region Hooks

        private void Init()
        {
            Unsubscribe("OnPlayerSleep");
            permission.RegisterPermission(permAllow, this);
            permission.RegisterPermission(permBypass, this);
            if (Configuration.PerformanceMode)
            {
                Unsubscribe("CanNetworkTo");
                foreach (BasePlayer player in BasePlayer.sleepingPlayerList)
                {
                    if (player != null && HasPerm(player))
                        player.limitNetworking = true;
                }
            }
            else
            {
                foreach (BasePlayer player in BasePlayer.sleepingPlayerList)
                {
                    if (player != null && HasPerm(player))
                        HideSleeper(player);
                }
            }
        }

        private void OnServerInitialized()
        {
            // Resubscribing to not get null exception on server startup
            Subscribe("OnPlayerSleep");
        }

        //Credit: most code taken from the vanish plugin made by Wulf
        private object CanNetworkTo(BasePlayer player, BasePlayer target)
        {
            if ((player != null && target != null)
                && player.IsSleeping() && HasPerm(player))
            {
                if (CanBypass(target, player))
                    return null;
                return false;
            }
            return null;
        }

        private object CanNetworkTo(HeldEntity entity, BasePlayer target)
            => entity == null ? null : CanNetworkTo(entity.GetOwnerPlayer(), target);
        
        private object CanBeTargeted(BasePlayer player, MonoBehaviour monoBehaviour)
        {
            if (player && player.IsSleeping() && HasPerm(player))
                return false;
            return null;
        }

        private void HideSleeper(BasePlayer player)
        {
            List<Connection> connections = new List<Connection>();
            foreach (BasePlayer target in BasePlayer.activePlayerList)
            {
                if (target == null || player == target || !target.IsConnected || CanBypass(target, player))
                    continue;
                if (target.net?.connection != null)
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

        //Credit: birthdates for .limitNetworking possibly fixing some lag issues
        private void OnPlayerSleep(BasePlayer player)
        {
            if (player != null && HasPerm(player))
            {
                if (Configuration.PerformanceMode)
                    player.limitNetworking = true;
                else
                    HideSleeper(player);
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player != null) 
            {
                player.limitNetworking = false;
                player.UpdatePlayerCollider(true);
            }
        }

        #endregion

        #region Helpers

        private bool HasPerm(BasePlayer player) => permission.UserHasPermission(player.UserIDString, permAllow);

        private bool CanBypass(BasePlayer player, BasePlayer target)
        {
            if (player == null || target == null) return false;
            if (permission.UserHasPermission(player.UserIDString, permBypass) || Configuration.ShowTeam && IsTeamMember(player, target) || 
                    Configuration.ShowFriends && IsFriend(player, target) || Configuration.ShowClan && IsClanMember(player, target))
                return true;
            return false;
        }

        private bool IsClanMember(BasePlayer player, BasePlayer target)
        {
            if (Clans == null || !Clans.IsLoaded)
                return false;

            var playerClan = Clans.Call<string>("GetClanOf", player);
            var otherPlayerClan = Clans.Call<string>("GetClanOf", target);
            if (playerClan == null || otherPlayerClan == null)
                return false;
            return playerClan == otherPlayerClan;
        }

        private bool IsTeamMember(BasePlayer player, BasePlayer target) => RelationshipManager.Instance.FindTeam(player.currentTeam)?.members.Contains(target.userID) ?? false;

        private bool IsFriend(BasePlayer player, BasePlayer target) => Friends?.Call<bool>("AreFriendsS", player.UserIDString, target.UserIDString) ?? false;
        
        #endregion
    }
}
using Network;
using System.Collections.Generic;
namespace Oxide.Plugins
{
    [Info("Invisible Sleepers", "collect_vood", "1.0.3")]
    [Description("Makes all sleepers invisible")]
    class InvisibleSleepers : RustPlugin
    {
        const string permAllow = "invisiblesleepers.allow";
        const string permBypass = "invisiblesleepers.bypass";

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
                if (CanBypass(target))
                    return null;
                return false;
            }
            return null;
        }
        private object CanBeTargeted(BaseCombatEntity entity)
        {
            BasePlayer player = entity as BasePlayer;
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
                if (player == target || !target.IsConnected)
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
        private bool HasPerm(BasePlayer player) => (permission.UserHasPermission(player.UserIDString, permAllow));
        private bool CanBypass(BasePlayer player) => (permission.UserHasPermission(player.UserIDString, permBypass));
    }
}
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;

// Requires: FriendlyFire

namespace Oxide.Plugins
{
    [Info("Punish Friendly Fire", "collect_vood", "1.2.0")]
    [Description("Punish player by X% of the damage done to friends.")]

/*======================================================================================================================= 
*
*   
*   17th november 2018
*
*	THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*
*********************************************
*   Original author :   Vliek on versions <1.1.0
*   Maintainer(s)   :   BuzZ 20181116 from v1.1.0
                        collect_vood since 20190926 from v1.2.0
*********************************************   
*=======================================================================================================================*/

        //permissions
        
    class PunishFF : CovalencePlugin
    {
        [PluginReference]
        private Plugin FriendlyFire;

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "PunishFriendlyFire", "Friend punishment damage: {0}" },
            }, this);
        }
        #endregion

        #region Config       
        private Configuration config;
        private class Configuration
        {    
            [JsonProperty(PropertyName = "Percentage of damage to punish")]
            public int percentagePunish = 50;
            [JsonProperty(PropertyName = "Only punish damage on players with permission")]
            public bool onlyPunishPermission = false;
            [JsonProperty(PropertyName = "Give damage permission")]
            public string givePermission = "punishff.give";
            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;
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
            permission.RegisterPermission(config.givePermission, this);
        }
        private void OnFriendAttacked(IPlayer attacker, IPlayer victim, HitInfo info)
        {
            float amount = info.damageTypes.Get(info.damageTypes.GetMajorityDamageType());
            float scale = config.percentagePunish / 100;
            if (!config.onlyPunishPermission)
            {
                attacker.Hurt(amount * scale);
                attacker.Reply(GetMessage("PunishFriendlyFire", attacker, (amount * scale).ToString()));
                if (config.Debug) Puts(amount.ToString());
                if (config.Debug) Puts(scale.ToString());
                return;
            }
            else
            {
                if (!HasPermission(victim))
                    return;
                attacker.Hurt(amount * scale);
                attacker.Reply(GetMessage("PunishFriendlyFire", attacker, (amount * scale).ToString()));
                if (config.Debug) Puts(amount.ToString());
                if (config.Debug) Puts(scale.ToString());
                return;
            }
        }
        #endregion

        #region Helpers
        string GetMessage(string key, IPlayer player, params string[] args) => String.Format(lang.GetMessage(key, this, player.Id), args);
        bool HasPermission(IPlayer player) => permission.UserHasPermission(player.Id, config.givePermission);
        #endregion
    }
}
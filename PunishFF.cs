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

    public class PunishFF : CovalencePlugin
    {
        [PluginReference]
        private Plugin FriendlyFire;

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "PunishFriendlyFire", "Friendly fire punishment damage: {0}" },
            }, this);
        }
        #endregion

        #region Config     
        
        private ConfigurationFile Configuration;

        private class ConfigurationFile
        {    
            [JsonProperty(PropertyName = "Percentage of damage to punish")]
            public int PercentagePunish = 50;
            [JsonProperty(PropertyName = "Only punish damage on players with permission")]
            public bool OnlyPunishPermission = false;
            [JsonProperty(PropertyName = "Give damage permission")]
            public string GivePermission = "punishff.give";
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
            permission.RegisterPermission(Configuration.GivePermission, this);
        }

        private void OnFriendAttacked(IPlayer attacker, IPlayer victim, HitInfo info)
        {
            //So damage is correct (armor values etc applied)
            NextTick(() =>
            {
                if (attacker == null || victim == null || info == null) return;

                float amount = info.damageTypes.Total();
                float scale = Configuration.PercentagePunish / 100;
                float dmgAmount = amount * scale;
                if (!Configuration.OnlyPunishPermission)
                {
                    attacker.Hurt(dmgAmount);
                    attacker.Reply(GetMessage("PunishFriendlyFire", attacker, dmgAmount.ToString()));
                }
                else
                {
                    if (!HasPermission(victim)) return;
                    attacker.Hurt(dmgAmount);
                    attacker.Reply(GetMessage("PunishFriendlyFire", attacker, dmgAmount.ToString()));
                }
            });
        }

        #endregion

        #region Helpers

        private string GetMessage(string key, IPlayer player, params string[] args) => String.Format(lang.GetMessage(key, this, player.Id), args);

        private bool HasPermission(IPlayer player) => permission.UserHasPermission(player.Id, Configuration.GivePermission);

        #endregion
    }
}
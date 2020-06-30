using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Puncher", "collect_vood", "1.0.0")]
    [Description("Punch other players")]
    class Puncher : CovalencePlugin
    {
        #region Hooks

        private void Init()
        {
            AddCovalenceCommand("punch", nameof(CommandPunch));
            AddCovalenceCommand("cpunch", nameof(CommandContinuosPunches));
        }

        #endregion

        #region Commands

        private void CommandPunch(IPlayer iPlayer, string cmd, string[] args)
        {
            if (!iPlayer.IsAdmin)
                return;

            BasePlayer target = iPlayer.Object as BasePlayer;
            if (args.Length > 0)
            {
                target = BasePlayer.Find(args[0]);
            }

            if (target == null)
            {
                iPlayer.Reply("No target found.");
                return;
            }

            var heldEntity = target.GetHeldEntity();
            if (heldEntity == null)
            {
                iPlayer.Reply("Needs to have an active held-entity.");
                return;
            }

            PunchPlayer(target, heldEntity);
        }

        private void CommandContinuosPunches(IPlayer iPlayer, string cmd, string[] args)
        {
            if (!iPlayer.IsAdmin)
                return;

            BasePlayer target = iPlayer.Object as BasePlayer;
            if (args.Length > 0)
            {
                target = BasePlayer.Find(args[0]);
            }

            if (target == null)
            {
                iPlayer.Reply("No target found.");
                return;
            }

            var heldEntity = target.GetHeldEntity();
            if (heldEntity == null)
            {
                iPlayer.Reply("Needs to have an active held-entity.");
                return;
            }

            InvokeHandler.Instance.StartCoroutine(ContinuosPunches(target, heldEntity));
        }

        #endregion

        #region Methods

        private void PunchPlayer(BasePlayer player, HeldEntity heldEntity)
        {
            heldEntity?.SendPunch(new Vector3(0.75f, -0.5f, 0f) * (UnityEngine.Random.Range(0f, 1f) >= 0.5f ? -30f : 30f), UnityEngine.Random.Range(0.05f, 0.2f));
        }

        private IEnumerator ContinuosPunches(BasePlayer player, HeldEntity heldEntity)
        {
            int i = 0;
            while (i < 10)
            {
                if (heldEntity == null)
                    heldEntity = player.GetHeldEntity();

                PunchPlayer(player, heldEntity);

                i++;
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.5f, 5f));
            }
        }

        #endregion
    }
}

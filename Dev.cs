using System;
using Oxide.Core.Libraries;

namespace Oxide.Plugins
{
    [Info("Dev", "collect_vood", "0.0.0")]
    [Description("Dev")]

    class Dev : CovalencePlugin
    {
        int componentSearchAttempts;
        TOD_Time timeComponent;

        #region Hooks
        void OnServerInitialized()
        {
            if (TOD_Sky.Instance == null)
            {
                componentSearchAttempts++;
                if (componentSearchAttempts < 10)
                    timer.Once(1, OnServerInitialized);
                else
                    PrintWarning("Could not find required component after 10 attempts. Day/night feature feature disabled");
                return;
            }
            timeComponent = TOD_Sky.Instance.Components.Time;
            if (timeComponent == null)
            {
                PrintWarning("Could not fetch time component. Day/night feature disabled");
                return;
            }
            SetTimeComponent();

        }
        #endregion

        #region Methods
        void SetTimeComponent()
        {
            timeComponent.ProgressTime = false;
            ConVar.Env.time = 12;
            server.Broadcast("Updated time to 12, progresstime false");
        }
        #endregion
    }
}

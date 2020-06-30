using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Network;

namespace Oxide.Plugins
{
    [Info("Giveaway", "collect_vood", "1.0.0")]
    [Description("Use this to give-away in game groups randomized")]
    public class Giveaway : CovalencePlugin
    {
        #region Fields

        private readonly WaitForSeconds CachedWaitForOneSecond = new WaitForSeconds(1f);

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "NoPermission", "You don't have permission to use this command." },
                { "WrongUsage", "You have to provide what group you are giving away and to whom (by default group: default)" },
                { "GroupNotFound", "The following group wasn't be able to be found: <color=orange>{0}<color>." },
                { "NoWinnerFound", "No winner was found." },
                { "GiveawayStart", "Giving away <color=orange>{0}</color> for group <color=orange>{1}</color> in <color=orange>{2}</color>!" },
                { "WonGlobal", "<color=orange>{0}</color> won <color=orange>{1}</color>, congratulations!" },
                { "WonPersonal", "You won <color=orange>{0}</color>, congratulations!" },
            }, this);
        }

        #endregion

        #region Configuration

        private ConfigurationData Configuration;

        private class ConfigurationData
        {
            [JsonProperty(PropertyName = "Giveaway command")]
            public string GiveawayCommand = "giveaway";            
            [JsonProperty(PropertyName = "Giveaway permission")]
            public string GiveawayPermission = "giveaway.use";            
            [JsonProperty(PropertyName = "List firework prefabs", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> FireworkPrefabs = new List<string>() 
            {
                "assets/prefabs/deployable/fireworks/mortarblue.prefab",
                "assets/prefabs/deployable/fireworks/mortarchampagne.prefab",
                "assets/prefabs/deployable/fireworks/mortargreen.prefab",
                "assets/prefabs/deployable/fireworks/mortarorange.prefab",
                "assets/prefabs/deployable/fireworks/mortarred.prefab",
                "assets/prefabs/deployable/fireworks/mortarviolet.prefab"
            };
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Configuration = new ConfigurationData();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Configuration = Config.ReadObject<ConfigurationData>();
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(Configuration);

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            new GameObject().AddComponent<Coroutiner>();

            AddCovalenceCommand(Configuration.GiveawayCommand, nameof(CommandGiveaway));          
        }

        public void Unload()
        {
            UnityEngine.Object.DestroyImmediate(Coroutiner.Instance);
        }

        #endregion

        #region Commands

        private void CommandGiveaway(IPlayer iPlayer, string cmd, string[] args)
        {
            if (!iPlayer.IsAdmin && !permission.UserHasPermission(iPlayer.Id, Configuration.GiveawayPermission))
            {
                iPlayer.Reply(GetMessage("NoPermission", iPlayer));
                return;
            }

            if (args.Length < 1)
            {
                iPlayer.Reply(GetMessage("WrongUsage", iPlayer));
                return;
            }

            string receiveGroup = args[0];
            if (!permission.GroupExists(receiveGroup))
            {
                iPlayer.Reply(GetMessage("GroupNotFound", iPlayer, receiveGroup));
                return;
            }

            string forGroup = "default";
            if (args.Length >= 2)
            {
                forGroup = args[1];
                if (!permission.GroupExists(forGroup))
                {
                    iPlayer.Reply(GetMessage("GroupNotFound", iPlayer, forGroup));
                    return;
                }
            }

            int delay = 3;
            if (args.Length >= 3)
            {
                int.TryParse(args[2], out delay);
            }            
            
            int duration = 7;
            if (args.Length >= 4)
            {
                int.TryParse(args[3], out duration);
            }

            Coroutiner.Instance.StartCoroutine(StartGiveaway(iPlayer, receiveGroup, forGroup, delay, duration));
        }

        #endregion

        #region Methods

        public IEnumerator StartGiveaway(IPlayer giveawayer, string receiveGroup, string forGroup, int delay, int duration)
        {       
            while (delay > 0)
            {
                server.Broadcast(GetMessage("GiveawayStart", giveawayer, receiveGroup, (forGroup == "default" ? "everyone" : forGroup), delay.ToString()));

                delay--;
                yield return CachedWaitForOneSecond;
            }

            for (int i = 0; i < 100; i++)
            {
                var winner = GetRandomBasePlayer(forGroup);
                if (winner != null && !permission.UserHasGroup(winner.UserIDString, receiveGroup)) break;
            }
            
            if (winner == null)
            {
                server.Broadcast(GetMessage("NoWinnerFound", giveawayer));
                yield break;
            }

            server.Broadcast(GetMessage("WonGlobal", giveawayer, winner.displayName.EscapeRichText(), receiveGroup));

            if (duration != 0)
            {
                server.Command($"addgroup {winner.UserIDString} {receiveGroup} {duration}d");
            }
            else permission.AddUserGroup(winner.UserIDString, receiveGroup);

            var cachedFireworks = Facepunch.Pool.GetList<RepeatingFirework>();
            foreach (var fireworkPrefab in Configuration.FireworkPrefabs)
            {
                var firework = GameManager.server.CreateEntity(fireworkPrefab, winner.transform.position + Vector3.up * 100) as RepeatingFirework;
                firework?.Spawn();
                if (firework == null) continue;

                cachedFireworks.Add(firework);
                firework.Begin();
            }

            yield return new WaitForSeconds(12f);

            foreach (var firework in cachedFireworks)
            {
                if (firework == null || firework.IsDestroyed) continue;
                firework.Kill();
            }

            Facepunch.Pool.FreeList(ref cachedFireworks);
        }

        public BasePlayer GetRandomBasePlayer(string groupName)
        {
            if (string.IsNullOrEmpty(groupName) || groupName == "default") return BasePlayer.activePlayerList[Random.Range(0, BasePlayer.activePlayerList.Count)];
            else
            {
                BasePlayer player = null;
                string[] userIds = permission.GetUsersInGroup(groupName);                
                for (int i = 0; i < 100; i++)
                {
                    string randomUserIdName = userIds[Random.Range(0, userIds.Length)];
                    string randomUserId = randomUserIdName.Split(' ')[0];
                    if ((player = BasePlayer.FindByID(ulong.Parse(randomUserId))) != null) return player;
                }
            }

            return null;
        }

        public void SendPosition(BaseNetworkable entity, Vector3 position)
        {
            Net.sv.write.PacketID(Message.Type.EntityPosition);
            Net.sv.write.EntityID(entity.net.ID);
            Net.sv.write.Vector3(position);
            Net.sv.write.Vector3(Vector3.zero);
            Net.sv.write.Float(entity.GetNetworkTime());

            var uid = entity.parentEntity.uid;
            if (uid > 0u)
            {
                Net.sv.write.EntityID(uid);
            }

            var info = new SendInfo(entity.net.group.subscribers)
            {
                method = SendMethod.ReliableSequenced,
                priority = Priority.Immediate
            };

            Net.sv.write.Send(info);
        }

        private string GetMessage(string key, IPlayer player, params string[] args) => string.Format(lang.GetMessage(key, this, player.Id), args);

        #endregion

        #region Coroutiner

        public class Coroutiner : MonoBehaviour
        {
            public static Coroutiner Instance;

            public void Awake() { Instance = this; }
        }

        #endregion

    }
}

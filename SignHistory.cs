using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
	[Info("SignHistory", "collect_vood", "1.1.2")]
	[Description("Creates a changelog for signs")]

	public class SignHistory : CovalencePlugin
	{
        #region Constants

        private const string AdminPerm = "signhistory.allow";

		#endregion

		#region Localization

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>()
			{
				{ "NotAllowed", "You don't have permission to use this command." },
				{ "NoSign", "No sign found." },
				{ "NoHistory", "No history found for this sign." },
				{ "Owner", "Owner: {0}" },
				{ "OwnerPart", "{0} ({1})" },
				{ "Changes", "Changes: {0}" },
				{ "ChangePart", "- {0} : {1} ({2})\n" }
			}, this);
		}

		#endregion

		#region Data

		private StoredData storedData;

		private Dictionary<uint, Sign> Signs => storedData.Signs;

		private class StoredData
		{
			[JsonProperty("Signs")]
			public Dictionary<uint, Sign> Signs = new Dictionary<uint, Sign>();
		}

		private class Sign
		{
			[JsonProperty("OwnerId")]
			public string OwnerId;
			[JsonProperty("Changes", ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<Change> Changes = new List<Change>();

			public Sign(string ownerId)
			{
				OwnerId = ownerId;
			}

			public class Change
			{
				[JsonProperty("Timestamp")]
				public DateTime Timestamp = DateTime.Now;
				[JsonProperty("UserId")]
				public string Id;

				public Change(string playerId)
				{
					Id = playerId;
				}
			}
		}
		
		private void SaveData() 
			=> Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
		
		private void Unload() 
			=> SaveData();

		private void OnServerSave()
			=> SaveData();

		#endregion

		#region Hooks

		private void Init()
		{
			permission.RegisterPermission(AdminPerm, this);

			storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);

			SaveData();
		}

		private void OnNewSave(string filename)
		{
			PrintWarning("Map wipe detected! Resetting all sign data.");

			storedData.Signs = new Dictionary<uint, Sign>();
			SaveData();
		}

		private void OnSignUpdated(BaseEntity sign, BasePlayer player)
			=> LogSignChange(sign, player);

		#endregion

		#region Methods

		private string GetMessage(string key, IPlayer player, params string[] args) 
			=> String.Format(lang.GetMessage(key, this, player.Id), args);

		private bool HasPerm(IPlayer player, string perm) 
			=> player.IsAdmin || permission.UserHasPermission(player.Id, perm);

		private void LogSignChange(BaseEntity sign, BasePlayer player)
		{
			if (sign == null || player == null) 
				return;

			var netId = sign.net.ID;

			if (!Signs.ContainsKey(netId)) 
			{
				if (sign.OwnerID == 0) 
					return;

				Signs.Add(netId, new Sign(sign.OwnerID.ToString()));
			}

			Signs[netId].Changes.Add(new Sign.Change(player.UserIDString));
		}

        #endregion

        #region Commands

        [Command("history")]
		private void cmdHistory(IPlayer player, string command, string[] args)
		{
			if (player.IsServer)
			{
				player.Reply("Cannot use this command from console.");
				return;
			}

			BasePlayer bPlayer;
			if (player == null || (bPlayer = player.Object as BasePlayer) == null) return;

			if (!HasPerm(player, AdminPerm)) 
			{
				player.Reply(GetMessage("NotAllowed", player));
				return;
			}

			RaycastHit hit;
			BaseEntity sign;
			if (!Physics.Raycast(bPlayer.eyes.HeadRay(), out hit, 4.0f) 
				|| (sign = hit.GetEntity()) == null) 
			{
				player.Reply(GetMessage("NoSign", player));
				return;
			}

			if (sign.Categorize() != "sign")
			{
				player.Reply(GetMessage("NoSign", player));
				return;
			}

			var netId = sign.net.ID;
			if (!Signs.ContainsKey(netId)) 
			{
				player.Reply(GetMessage("NoHistory", player));
				return;
			}

			var signData = Signs[netId];

			string changes = "\n";
			foreach (var change in signData.Changes)
			{
				var user = covalence.Players.FindPlayerById(change.Id);
				if (user == null) 
					continue;

				changes += GetMessage("ChangePart", user, change.Timestamp.ToString(), user.Name, user.Id);
			}

			string ownerString = signData.OwnerId;

			var owner = covalence.Players.FindPlayerById(signData.OwnerId);
			if (owner != null)
			{
				ownerString = GetMessage("OwnerPart", owner, owner.Name, owner.Id);
			}

			player.Reply($"{GetMessage("Owner", player, ownerString)}\n" +
				$"{GetMessage("Changes", player, changes)}");
		}

		#endregion
	}
}
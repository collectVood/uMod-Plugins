using System;
using System.Collections.Generic;
using System.IO;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Rust;

namespace Oxide.Plugins
{
    [Info("Magic Loot", "collect_vood", "1.0.0")]
    [Description("Simple components multiplier and loot system")]
    public class MagicLoot : CovalencePlugin
    {
        #region Fields

        private bool _initialized = false;

        private readonly int _maxContainerSlots = 18;

        private Dictionary<Rarity, List<ItemData>> _sortedRarities;

        private Dictionary<int, List<ulong>> _skinsCache = new Dictionary<int, List<ulong>>();

        #endregion

        #region Configuration

        private ConfigurationFile _configuration;

        public class ConfigurationFile
        {
            [JsonProperty(PropertyName = "General Settings")]
            public SettingsFile Settings = new SettingsFile();

            [JsonProperty(PropertyName = "Extra Loot")]
            public ExtraLootFile ExtraLoot = new ExtraLootFile();

            [JsonProperty(PropertyName = "Blacklisted Items (Item-Shortnames)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> BlacklistedItems = new List<string>()
            { 
                "ammo.rocket.smoke"
            };

            [JsonProperty(PropertyName = "Manual Item Multipliers (Key: Item-Shortname, Value: Multiplier)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> ManualItemMultipliers = new Dictionary<string, float>();

            [JsonProperty(PropertyName = "Containers Data (Key: Container-Shortname, Value: Container Settings)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, ContainerData> ContainersData = new Dictionary<string, ContainerData>();

            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;
        }

        public class SettingsFile
        {
            [JsonProperty(PropertyName = "General Item List Multiplier (All items in the 'Manual Item Multipliers' List)")]
            public float ItemListMultiplier = 1f;

            [JsonProperty(PropertyName = "Non Item List Multiplier (All items not listed in the 'Manual Item Multipliers' List)")]
            public float NonItemListMultiplier = 1f;

            [JsonProperty(PropertyName = "Limit Multipliers to Stacksizes")]
            public bool LimitToStacksizes = true;

            [JsonProperty(PropertyName = "Multiply Blueprints")]
            public bool BlueprintDuplication = false;            
            
            [JsonProperty(PropertyName = "Random Workshop Skins")]
            public bool RandomWorkshopSkins = false;
        }

        public class ExtraLootFile
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled = false;

            [JsonProperty(PropertyName = "Extra Items Min")]
            public int ExtraItemsMin = 0;

            [JsonProperty(PropertyName = "Extra Items Max")]
            public int ExtraItemsMax = 0;

            [JsonProperty(PropertyName = "Prevent Duplicates")]
            public bool PreventDuplicates = true;

            [JsonProperty(PropertyName = "Prevent Duplicates Retries")]
            public int PreventDuplicatesRetries = 10;
        }

        public class ContainerData
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled = true;

            [JsonProperty(PropertyName = "Extra Items Min")]
            public int ExtraItemsMin = 0;

            [JsonProperty(PropertyName = "Extra Items Max")]
            public int ExtraItemsMax = 0;

            [JsonProperty(PropertyName = "Loot Multiplier")]
            public float Multiplier = 1f;

            [JsonProperty(PropertyName = "Utilize Vanilla Loot Tables on Default Loot")]
            public bool VanillaLootTablesDefault = true;            
            
            [JsonProperty(PropertyName = "Utilize Vanilla Loot Tables on Extra Loot")]
            public bool VanillaLootTablesExtra = true;

            [JsonProperty(PropertyName = "Utilize Random Rarity (depending on Items ALREADY in the container)")]
            public bool RandomRarities = false;

            [JsonProperty(PropertyName = "Rarity To Use (ONLY if 'Utilize Vanilla Loot Tables' is FALSE & 'Utilize Random Rarity' is FALSE | 0 = None, 1 = Common, 2 = Uncommon, 3 = Rare, 4 = Very Rare)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Rarity> Rarities = new List<Rarity>() { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.VeryRare };
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");

            _configuration = new ConfigurationFile();

            foreach (var itemDef in ItemManager.itemList)
            {
                if (itemDef.category != ItemCategory.Component)
                {
                    continue;
                }

                _configuration.ManualItemMultipliers.Add(itemDef.shortname, 1f);
            }
    
            _configuration.ManualItemMultipliers.Add("scrap", 1f);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _configuration = Config.ReadObject<ConfigurationFile>();
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_configuration);

        #endregion

        #region Data

        private readonly string _raritiesPath = $"{nameof(MagicLoot)}/Rarities";

        private bool LoadData()
        {
            foreach (var container in _configuration.ContainersData)
            {
                if (!container.Value.VanillaLootTablesDefault 
                    || !container.Value.VanillaLootTablesExtra)
                {
                    _sortedRarities = GetSortedRarities();
                    break;
                }
            }

            if (_sortedRarities == null)
            {
                Puts("Skipping Custom Loot Tables & Custom Rarities (not used) ...");
                return true;
            }

            string[] rarities = new string[] {};
            
            try
            {
                int removedItemsCount = 0;
                rarities = Interface.Oxide.DataFileSystem.GetFiles(_raritiesPath, "*");

                foreach (var fileName in rarities)
                {
                    string rarityName = Utility.GetFileNameWithoutExtension(fileName);

                    Rarity rarity;
                    if (!Enum.TryParse<Rarity>(rarityName, out rarity))
                    {
                        PrintWarning($"The Rarity Naming has changed for : {rarityName}, please contact the developer!");
                        continue;
                    }

                    var rarityItemData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, ItemData>>(_raritiesPath + "/" + rarityName);
                    var allItemData = _sortedRarities[rarity];

                    for (int i = 0; i < allItemData.Count; i++)
                    {
                        var itemData = allItemData[i];

                        ItemData savedItemData;
                        if (!rarityItemData.TryGetValue(itemData.ItemDefinition.shortname, out savedItemData))
                        {
                            Puts("Adding new Item '" + itemData.ItemDefinition.shortname + "' to rarity list : " + rarity + "...");
                            itemData.MaxStack = itemData.ItemDefinition.stackable;
                            rarityItemData.Add(itemData.ItemDefinition.shortname, itemData);
                            continue;
                        }

                        if (savedItemData.Spawn)
                        {
                            continue;
                        }

                        allItemData.RemoveAt(i);
                        i--;
                        removedItemsCount++;
                    }

                    Interface.Oxide.DataFileSystem.WriteObject<Dictionary<string, ItemData>>(_raritiesPath + "/" + rarityName, rarityItemData);

                    Puts("Loaded Rarity '" + rarityName + "' with " + allItemData.Count + " items...");
                }

                Puts("Skipping " + removedItemsCount + " items for Custom Loot Table items...");

                Puts("Loaded Rarities from : data/" + _raritiesPath);
            }
            catch (Exception e)
            {
                if (!(e is IOException))
                {                  
                    PrintError("Loading of Rarities failed! Resolve the conflict before the plugin can properly load!");
                    PrintError(e.Message + " " + e.StackTrace);
                    _initialized = false;
                    return false;
                }
                
                PrintWarning("Creating new Rarities in : data/" + _raritiesPath);

                foreach (var rarity in _sortedRarities)
                {
                    var rarityFile = Interface.Oxide.DataFileSystem.GetFile(_raritiesPath + "/" + rarity.Key);

                    var rarityItemData = new Dictionary<string, ItemData>();
                    foreach (var itemData in rarity.Value)
                    {
                        itemData.MaxStack = itemData.ItemDefinition.stackable;
                        rarityItemData.Add(itemData.ItemDefinition.shortname, itemData);
                    }

                    rarityFile.WriteObject<Dictionary<string, ItemData>>(rarityItemData);

                    PrintWarning("Creating new Rarity : " + rarity.Key + " with " + rarityItemData.Count + " items...");
                }
            }

            return true;
        }

        public class ItemData
        {
            [JsonProperty(PropertyName = "Should Spawn")]
            public bool Spawn = true;

            [JsonProperty(PropertyName = "Max Stack Spawn")]
            public int MaxStack;

            [JsonIgnore]
            public ItemDefinition ItemDefinition;
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            _initialized = true;

            if (!LoadData())
            {
                return;
            }

            Puts($"Loaded at x{_configuration.Settings.NonItemListMultiplier} vanilla rate" +
                $" | Manual Item List at x{_configuration.Settings.ItemListMultiplier} rate [Extra Loot: {_configuration.ExtraLoot.Enabled}]");

            RepopulateContainers();            
        }

        private object OnLootSpawn(LootContainer container)
        {
            if (!_initialized || container?.inventory?.itemList == null)
            {
                return null;
            }
            
            ContainerData containerData;
            if (!_configuration.ContainersData.TryGetValue(container.ShortPrefabName, out containerData))
            {
                _configuration.ContainersData.Add(container.ShortPrefabName, containerData = new ContainerData());
                SaveConfig();
            }

            if (IgnoreContainer(containerData))
            {
                return null;
            }

            PopulateContainer(container, containerData);

            if (container.shouldRefreshContents && container.isLootable)
            {
                container.Invoke(new Action(container.SpawnLoot), UnityEngine.Random.Range(
                    container.minSecondsBetweenRefresh, container.maxSecondsBetweenRefresh));
            }

            return container;
        }

        private void Unload()
        {
            _initialized = false;

            foreach (var container in UnityEngine.Object.FindObjectsOfType<LootContainer>())
            {
                if (container?.inventory == null)
                {
                    continue;
                }

                container.SpawnLoot();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Repopulates all LootContainers
        /// </summary>
        private void RepopulateContainers()
        {
            int count = 0;
            foreach (var container in UnityEngine.Object.FindObjectsOfType<LootContainer>())
            {
                ContainerData containerData;
                if (!_configuration.ContainersData.TryGetValue(container.ShortPrefabName, out containerData))
                {
                    _configuration.ContainersData.Add(container.ShortPrefabName, containerData = new ContainerData());
                }

                if (IgnoreContainer(containerData))
                {
                    continue;
                }

                container.inventory.Clear();
                ItemManager.DoRemoves();

                PopulateContainer(container, containerData);
                count++;
            }

            SaveConfig();

            Puts("Repopulated " + count.ToString() + " loot containers.");
        }

        /// <summary>
        /// Populates the Container
        /// </summary>
        /// <param name="container"></param>
        /// <param name="containerData"></param>
        private void PopulateContainer(LootContainer container, ContainerData containerData)
        {
            container.inventory.capacity = _maxContainerSlots;

            AddDefaultLoot(container, containerData, !(containerData.Enabled && !containerData.VanillaLootTablesDefault));

            AddExtraLoot(container, containerData);

            RandomizeDurability(container);

            //Generate scrap late so it is in the last slot
            container.GenerateScrap();

            ReinforceRules(container, containerData);
        }

        /// <summary>
        /// Removes duplicate blueprints, items or blacklisted items and applies multipliers
        /// </summary>
        /// <param name="container"></param>
        private void ReinforceRules(LootContainer container, ContainerData containerData)
        {
            for (int i = 0; i < container.inventory.itemList.Count; i++)
            {
                var item = container.inventory.itemList[i];

                if (_configuration.BlacklistedItems.Contains(item.info.shortname))
                {
                    item.RemoveFromContainer();
                    i--;
                    continue;
                }

                if (!_configuration.Settings.BlueprintDuplication && item.IsBlueprint()) 
                {
                    continue;
                }

                if (_configuration.Settings.RandomWorkshopSkins)
                {
                    item.skin = GetRandomSkin(item.info);
                    if (item.skin != 0)
                    {
                        var heldEntity = item.GetHeldEntity();
                        if (heldEntity != null)
                        {
                            heldEntity.skinID = item.skin;                            
                        }                        
                    }                       
                }

                float multiplier = 1f;
                var inItemList = _configuration.ManualItemMultipliers.TryGetValue(
                    item.info.shortname, out multiplier);

                if (!inItemList)
                {
                    multiplier = 1f;
                }

                //Do not multiply
                if (multiplier == 0f)
                {
                    continue;
                }

                multiplier *= containerData.Multiplier * (inItemList ? _configuration.Settings.ItemListMultiplier 
                    : _configuration.Settings.NonItemListMultiplier);
               
                item.amount = _configuration.Settings.LimitToStacksizes ? (int)Math.Min(item.amount * multiplier, item.info.stackable) 
                    : (int)(item.amount * multiplier);              
            }
        }

        /// <summary>
        /// Returns a list of items where duplicated items are found
        /// </summary>
        /// <param name="item"></param>
        /// <param name="items"></param>
        /// <returns></returns>
        private void FindDuplicates(int itemId, List<Item> items, ref List<Item> duplicateItems)
        {
            foreach (var cItem in items)
            {
                if (cItem.info.itemid != itemId)
                {
                    continue;
                }

                duplicateItems.Add(cItem);
            }
        }

        /// <summary>
        /// Adds default loot to the container
        /// </summary>
        /// <param name="container"></param>
        /// <param name="containerData"></param>
        private void AddDefaultLoot(LootContainer container, ContainerData containerData, bool useDefaultLootTables = true)
        {
            if (container.LootSpawnSlots.Length != 0)
            {
                for (int i = 0; i < container.LootSpawnSlots.Length; i++)
                {
                    var lootSpawnSlot = container.LootSpawnSlots[i];
                    for (int j = 0; j < lootSpawnSlot.numberToSpawn; j++)
                    {
                        if (UnityEngine.Random.Range(0f, 1f) <= lootSpawnSlot.probability)
                        {
                            if (useDefaultLootTables)
                            {
                                lootSpawnSlot.definition.SpawnIntoContainer(container.inventory);
                                continue;
                            }

                            AddRandomItem(container, containerData);
                        }
                    }
                }
            }
            else if (container.lootDefinition != null)
            {
                for (int k = 0; k < container.maxDefinitionsToSpawn; k++)
                {
                    if (useDefaultLootTables)
                    {
                        container.lootDefinition.SpawnIntoContainer(container.inventory);
                        continue;
                    }

                    AddRandomItem(container, containerData);
                }
            }
        }

        /// <summary>
        /// Adds extra loot to the container, depending on both the Extra Loot and Specific Container Data
        /// </summary>
        /// <param name="container"></param>
        private void AddExtraLoot(LootContainer container, ContainerData containerData)
        {
            var additionalItemsMin = 0;
            var additionalItemsMax = 0;

            if (_configuration.ExtraLoot.Enabled)
            {
                additionalItemsMin += _configuration.ExtraLoot.ExtraItemsMin;
                additionalItemsMax += _configuration.ExtraLoot.ExtraItemsMax;
            }

            if (containerData.Enabled) 
            {
                additionalItemsMin += containerData.ExtraItemsMin;
                additionalItemsMax += containerData.ExtraItemsMax;
            }

            var additionalItems = UnityEngine.Random.Range(additionalItemsMin, additionalItemsMax + 1);
            for (int i = 0; i < additionalItems; i++)
            {
                if (containerData.VanillaLootTablesExtra)
                {
                    AddRandomVanillaItem(container);
                }
                else
                {
                    AddRandomItem(container, containerData);
                }
            }
        }

        /// <summary>
        /// Adds a random Vanilla-LootTable Item to the Container
        /// </summary>
        /// <param name="container"></param>
        private void AddRandomVanillaItem(LootContainer container)
        {
            if (container.lootDefinition != null)
            {
                container.lootDefinition.SpawnIntoContainer(container.inventory);
            }
        }

        /// <summary>
        /// Adds a random Item to the Container
        /// </summary>
        /// <param name="container"></param>
        private void AddRandomItem(LootContainer container, ContainerData containerData)
        {
            if (containerData.Rarities.Count == 0)
            {
                PrintWarning("Using Non-Vanilla-LootTables but no set Rarity found in list for container : " + container.ShortPrefabName);
                containerData.Rarities.Add(Rarity.Common);
                SaveConfig();
            }

            var randomContainerRarity = Rarity.Common;            
            if (containerData.RandomRarities && container.inventory.itemList.Count > 0)
            {
                randomContainerRarity = container.inventory.itemList[UnityEngine.Random.Range(0, container.inventory.itemList.Count)].info.rarity;
            }
            else
            {
                randomContainerRarity = containerData.Rarities[UnityEngine.Random.Range(0, containerData.Rarities.Count)];
            }
            
            var rarities = _sortedRarities[randomContainerRarity];
            var randomItemData = rarities[UnityEngine.Random.Range(0, rarities.Count)];

            if (_configuration.ExtraLoot.PreventDuplicates)
            {
                var duplicateItems = Pool.GetList<Item>();

                for (int i = 0; i < _configuration.ExtraLoot.PreventDuplicatesRetries; i++)
                {
                    FindDuplicates(randomItemData.ItemDefinition.itemid, container.inventory.itemList, ref duplicateItems);

                    if (duplicateItems.Count > 0)
                    {                       
                        randomItemData = rarities[UnityEngine.Random.Range(0, rarities.Count)];
                        duplicateItems.Clear();

                        if (i >= _configuration.ExtraLoot.PreventDuplicatesRetries - 1)
                        {
                            PrintDebug("Unable to solve duplicate conflict with " + container.ShortPrefabName + " " + container.transform.position);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
              
                Pool.FreeList(ref duplicateItems);
            }

            container.inventory.AddItem(randomItemData.ItemDefinition, UnityEngine.Random.Range(1, randomItemData.MaxStack));
        }

        /// <summary>
        /// Randomizes the durability of the Items in the Container if ROADSIDE or TOWN Container
        /// </summary>
        /// <param name="container"></param>
        private void RandomizeDurability(LootContainer container)
        {
            if (container.SpawnType == LootContainer.spawnType.ROADSIDE || container.SpawnType == LootContainer.spawnType.TOWN)
            {
                foreach (Item item in container.inventory.itemList)
                {
                    if (!item.hasCondition)
                    {
                        continue;
                    }

                    item.condition = UnityEngine.Random.Range(item.info.condition.foundCondition.fractionMin, item.info.condition.foundCondition.fractionMax) * item.info.condition.max;
                }
            }
        }

        /// <summary>
        /// Generates a Rarity Dictionary containing only Item Definitions that are not Blacklisted
        /// </summary>
        /// <returns></returns>
        private Dictionary<Rarity, List<ItemData>> GetSortedRarities()
        {
            var sortedRarities = new Dictionary<Rarity, List<ItemData>>();

            foreach (var itemDef in ItemManager.itemList)
            {
                if (_configuration.BlacklistedItems.Contains(itemDef.shortname))
                {
                    continue;
                }

                if (!sortedRarities.ContainsKey(itemDef.rarity))
                {
                    sortedRarities.Add(itemDef.rarity, new List<ItemData>() { new ItemData() { ItemDefinition = itemDef } });
                    continue;
                }

                sortedRarities[itemDef.rarity].Add(new ItemData() { ItemDefinition = itemDef });
            }

            return sortedRarities;
        }

        /// <summary>
        /// Returns if the container is NOT needed to be custom populated
        /// </summary>
        /// <param name="container"></param>
        /// <returns></returns>
        private bool IgnoreContainer(ContainerData containerData)
        {
            return !containerData.Enabled;
        }

        private ulong GetRandomSkin(ItemDefinition itemDef)
        {
            List<ulong> possibleSkins;
            if (_skinsCache.TryGetValue(itemDef.itemid, out possibleSkins))
            {
                return possibleSkins[UnityEngine.Random.Range(0, possibleSkins.Count)];
            }

            possibleSkins = new List<ulong>() { 0 };

            foreach (var skin in ItemSkinDirectory.ForItem(itemDef))
            {
                possibleSkins.Add((ulong)skin.id);
            }

            foreach (var skin in Rust.Workshop.Approved.All.Values)
            {
                if (skin.Skinnable.ItemName != itemDef.shortname)
                {
                    continue;
                }

                possibleSkins.Add(skin.WorkshopdId);
            }

            _skinsCache.Add(itemDef.itemid, possibleSkins);

            return possibleSkins[UnityEngine.Random.Range(0, possibleSkins.Count)];
        }

        private void PrintDebug(object message)
        {
            if (!_configuration.Debug)
            {
                return;
            }

            Puts(message.ToString());
        }

        #endregion
    }
}

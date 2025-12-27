using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites
{
    /// <summary>
    /// 老箱子战利品生成器
    /// 负责生成营地内老箱子的每日刷新内容
    /// </summary>
    internal static class OldDuchestLootGenerator
    {
        private static readonly Random rand = new Random();

        /// <summary>
        /// 6分钟 = 360秒 = 21600帧
        /// </summary>
        internal const int RefreshInterval = 21600;

        /// <summary>
        /// 生成每日刷新的箱子内容
        /// </summary>
        public static List<Item> GenerateDailyLoot() {

            List<Item> loot = [];

            //钱币奖励
            AddCoinReward(loot, rand);

            //根据进度添加老公爵掉落物
            if (InWorldBossPhase.Downed23.Invoke() || InWorldBossPhase.Downed26.Invoke()) {
                //老公爵相关掉落物
                AddOldDukeDrops(loot, rand);
            }

            //海洋主题物品
            AddOceanThemeItems(loot, rand);

            //随机药水和消耗品
            AddPotionsAndConsumables(loot, rand);

            //稀有材料
            if (rand.NextDouble() < 0.3) {
                AddRareMaterials(loot, rand);
            }

            //特殊武器装备
            if (rand.NextDouble() < 0.2) {
                AddSpecialWeapons(loot, rand);
            }

            return loot;
        }

        /// <summary>
        /// 添加钱币奖励
        /// </summary>
        private static void AddCoinReward(List<Item> loot, Random random) {
            int coinAmount = random.Next(80, 300);
            int platinumCoins = coinAmount / 100;
            int goldCoins = (coinAmount % 100) / 10;
            int silverCoins = coinAmount % 10;

            if (platinumCoins > 0) {
                Item coin = new Item();
                coin.SetDefaults(ItemID.PlatinumCoin);
                coin.stack = platinumCoins;
                loot.Add(coin);
            }

            if (goldCoins > 0) {
                Item coin = new Item();
                coin.SetDefaults(ItemID.GoldCoin);
                coin.stack = goldCoins;
                loot.Add(coin);
            }

            if (silverCoins > 0) {
                Item coin = new Item();
                coin.SetDefaults(ItemID.SilverCoin);
                coin.stack = silverCoins;
                loot.Add(coin);
            }
        }

        /// <summary>
        /// 添加老公爵掉落物
        /// </summary>
        private static void AddOldDukeDrops(List<Item> loot, Random random) {
            HashSet<int> oldDukeDrops = VaultUtils.GetNPCDrops(CWRID.NPC_OldDuke, true);
            List<int> qualityDrops = [];

            //筛选高价值物品
            foreach (int drop in oldDukeDrops) {
                Item item = new Item(drop);
                if (item.value > 6000 * 30) {
                    qualityDrops.Add(drop);
                }
            }

            if (qualityDrops.Count == 0) {
                return;
            }

            //随机选择3到6个物品
            int itemCount = random.Next(3, 7);
            for (int i = 0; i < itemCount && loot.Count < 240; i++) {
                if (qualityDrops.Count == 0) {
                    break;
                }

                int randomIndex = random.Next(qualityDrops.Count);
                int itemType = qualityDrops[randomIndex];
                qualityDrops.RemoveAt(randomIndex);

                Item item = new Item();
                item.SetDefaults(itemType);
                item.stack = 1;

                //如果是武器类添加随机词缀
                if (IsWeapon(item)) {
                    item.Prefix(-1);
                }
                //如果是装备类添加随机词缀
                else if (IsEquipment(item)) {
                    item.Prefix(-1);
                }

                loot.Add(item);
            }
        }

        /// <summary>
        /// 添加海洋主题物品
        /// </summary>
        private static void AddOceanThemeItems(List<Item> loot, Random random) {
            int[] basicOceanItems = [
                ItemID.Coral,
                ItemID.Starfish,
                ItemID.Seashell,
                ItemID.SharkFin,
                ItemID.GillsPotion,
                ItemID.SonarPotion,
                ItemID.WaterWalkingPotion,
                ItemID.FlipperPotion
            ];

            int itemCount = random.Next(2, 5);
            for (int i = 0; i < itemCount && loot.Count < 240; i++) {
                int itemType = basicOceanItems[random.Next(basicOceanItems.Length)];
                Item item = new Item();
                item.SetDefaults(itemType);
                item.stack = random.Next(3, 15);
                loot.Add(item);
            }
        }

        /// <summary>
        /// 添加药水和消耗品
        /// </summary>
        private static void AddPotionsAndConsumables(List<Item> loot, Random random) {
            int[] usefulPotions = [
                ItemID.GreaterHealingPotion,
                ItemID.GreaterManaPotion,
                ItemID.IronskinPotion,
                ItemID.RegenerationPotion,
                ItemID.SwiftnessPotion,
                ItemID.EndurancePotion,
                ItemID.LifeforcePotion,
                ItemID.RagePotion,
                ItemID.WrathPotion,
                ItemID.ObsidianSkinPotion,
                ItemID.InfernoPotion,
                ItemID.SummoningPotion,
                ItemID.ArcheryPotion
            ];

            int potionCount = random.Next(3, 6);
            for (int i = 0; i < potionCount && loot.Count < 240; i++) {
                int potionType = usefulPotions[random.Next(usefulPotions.Length)];
                Item potion = new Item();
                potion.SetDefaults(potionType);
                potion.stack = random.Next(5, 20);
                loot.Add(potion);
            }

            //钓鱼饵料
            if (random.NextDouble() < 0.6) {
                int[] baitItems = [
                    ItemID.MasterBait,
                    ItemID.JourneymanBait,
                    ItemID.ApprenticeBait
                ];
                int baitType = baitItems[random.Next(baitItems.Length)];
                Item bait = new Item();
                bait.SetDefaults(baitType);
                bait.stack = random.Next(10, 30);
                loot.Add(bait);
            }
        }

        /// <summary>
        /// 添加稀有材料
        /// </summary>
        private static void AddRareMaterials(List<Item> loot, Random random) {
            int[] rareMaterials = [
                ItemID.SoulofLight,
                ItemID.SoulofNight,
                ItemID.SoulofFlight,
                ItemID.SoulofSight,
                ItemID.SoulofMight,
                ItemID.SoulofFright,
                ItemID.CrystalShard,
                ItemID.Ectoplasm,
                ItemID.ChlorophyteBar,
                ItemID.HallowedBar,
                ItemID.ShroomiteBar,
                ItemID.SpectreBar
            ];

            int materialCount = random.Next(1, 3);
            for (int i = 0; i < materialCount && loot.Count < 240; i++) {
                int materialType = rareMaterials[random.Next(rareMaterials.Length)];
                Item material = new Item();
                material.SetDefaults(materialType);
                material.stack = random.Next(5, 25);
                loot.Add(material);
            }
        }

        /// <summary>
        /// 添加特殊武器装备
        /// </summary>
        private static void AddSpecialWeapons(List<Item> loot, Random random) {
            int[] specialWeapons = [
                ItemID.FlowerofFrost,
                ItemID.Uzi,
                ItemID.ChainGun,
                ItemID.VenusMagnum,
                ItemID.Shotgun,
                ItemID.TacticalShotgun,
                ItemID.SniperRifle,
                ItemID.Tsunami,
                ItemID.RazorbladeTyphoon,
                ItemID.BubbleGun,
                ItemID.ToxicFlask,
                ItemID.NailGun,
                ItemID.PiranhaGun,
                ItemID.Flairon
            ];

            int weaponType = specialWeapons[random.Next(specialWeapons.Length)];
            Item weapon = new Item();
            weapon.SetDefaults(weaponType);

            //为武器添加随机词缀
            if (IsWeapon(weapon)) {
                weapon.Prefix(-1);
            }

            loot.Add(weapon);
        }

        /// <summary>
        /// 判断物品是否为武器
        /// </summary>
        private static bool IsWeapon(Item item) {
            return item.damage > 0;
        }

        /// <summary>
        /// 判断物品是否为装备
        /// </summary>
        private static bool IsEquipment(Item item) {
            return item.accessory || item.defense > 0;
        }

        /// <summary>
        /// 根据游戏时间生成种子
        /// 每6分钟(21600游戏帧)刷新一次
        /// </summary>
        public static int GetGameTimeSeed() {
            uint currentGameTime = Main.GameUpdateCount;
            int refreshCycle = (int)(currentGameTime / RefreshInterval);
            return refreshCycle;
        }

        /// <summary>
        /// 获取距离下次刷新的剩余时间(秒)
        /// </summary>
        public static int GetTimeUntilNextRefresh() {
            uint currentGameTime = Main.GameUpdateCount;
            int remainingFrames = (int)(RefreshInterval - (currentGameTime % RefreshInterval));
            return remainingFrames / 60; //转换为秒
        }
    }
}

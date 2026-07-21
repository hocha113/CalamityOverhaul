using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.Campsites
{
    /// <summary>老箱子战利品生成器</summary>
    internal static class OldDuchestLootGenerator
    {
        /// <summary>刷新周期 6min=21600帧</summary>
        internal const int RefreshInterval = 21600;

        public static List<Item> GenerateDailyLoot() {

            List<Item> loot = [];
            UnifiedRandom rand = Main.rand;

            AddCoinReward(loot, rand);

            if (InWorldBossPhase.Downed23.Invoke() || InWorldBossPhase.Downed26.Invoke()) {
                AddOldDukeDrops(loot, rand);
            }

            AddOceanThemeItems(loot, rand);

            AddPotionsAndConsumables(loot, rand);

            if (rand.NextDouble() < 0.3) {
                AddRareMaterials(loot, rand);
            }

            if (rand.NextDouble() < 0.2) {
                AddSpecialWeapons(loot, rand);
            }

            return loot;
        }

        private static void AddCoinReward(List<Item> loot, UnifiedRandom random) {
            int coinAmount = random.Next(80, 300);
            int platinumCoins = coinAmount / 100;
            int goldCoins = coinAmount % 100 / 10;
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

        private static void AddOldDukeDrops(List<Item> loot, UnifiedRandom random) {
            HashSet<int> oldDukeDrops = VaultUtils.GetNPCDrops(CWRID.NPC_OldDuke, true);
            List<int> qualityDrops = [];

            foreach (int drop in oldDukeDrops) {
                Item item = new Item(drop);
                if (item.value > 6000 * 30) {
                    qualityDrops.Add(drop);
                }
            }

            if (qualityDrops.Count == 0) {
                return;
            }

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

                if (IsWeapon(item)) {
                    item.Prefix(-1);
                }
                else if (IsEquipment(item)) {
                    item.Prefix(-1);
                }

                loot.Add(item);
            }
        }

        private static void AddOceanThemeItems(List<Item> loot, UnifiedRandom random) {
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

        private static void AddPotionsAndConsumables(List<Item> loot, UnifiedRandom random) {
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

        private static void AddRareMaterials(List<Item> loot, UnifiedRandom random) {
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

        private static void AddSpecialWeapons(List<Item> loot, UnifiedRandom random) {
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

            if (IsWeapon(weapon)) {
                weapon.Prefix(-1);
            }

            loot.Add(weapon);
        }

        private static bool IsWeapon(Item item) {
            return item.damage > 0;
        }

        private static bool IsEquipment(Item item) {
            return item.accessory || item.defense > 0;
        }

        /// <summary>按游戏时间种子，每6min一轮</summary>
        public static int GetGameTimeSeed() {
            uint currentGameTime = Main.GameUpdateCount;
            int refreshCycle = (int)(currentGameTime / RefreshInterval);
            return refreshCycle;
        }

        public static int GetTimeUntilNextRefresh() {
            uint currentGameTime = Main.GameUpdateCount;
            int remainingFrames = (int)(RefreshInterval - currentGameTime % RefreshInterval);
            return remainingFrames / 60;//秒
        }
    }
}

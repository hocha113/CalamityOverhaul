using CalamityMod;
using System;
using Terraria;
using Terraria.GameContent.Prefixes;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace CalamityOverhaul.Common
{
    internal class OnCalamityReforgeEvent
    {
        private static CalamityMod.CalamityMod mod => (CalamityMod.CalamityMod)ModLoader.GetMod("CalamityMod");

        // 获取当前前缀在指定前缀层级中的层级索引
        private static int GetPrefixTier(int[][] tiers, int currentPrefix) {
            for (int checkingTier = 0; checkingTier < tiers.Length; checkingTier++) {
                if (Array.Exists(tiers[checkingTier], prefix => prefix == currentPrefix))
                    return checkingTier;
            }
            return -1;
        }

        // 根据当前前缀的层级迭代新的前缀
        private static int IteratePrefix(UnifiedRandom rand, int[][] reforgeTiers, int currentPrefix) {
            int currentTier = GetPrefixTier(reforgeTiers, currentPrefix);
            int newTier = Math.Min(currentTier + 1, reforgeTiers.Length - 1);
            return rand.Next(reforgeTiers[newTier]);
        }

        private static int GetCalPrefix(string name) => mod.TryFind(name, out ModPrefix ret) ? ret.Type : 0;

        //修改的关键，决定什么物品可以获得近战加成前缀
        private static bool OverWeaponFixMeleePerg(Item item) {
            if (item.type == ItemID.None) {
                return false;
            }
            if (item.ModItem == null) {
                return false;
            }
            return item.ModItem.MeleePrefix() || item.CWR().GetMeleePrefix;
        }

        // 根据物品类型和当前前缀获取重铸后的前缀
        internal static int HandleCalamityReforgeModificationDueToMissingItemLoader(Item item, UnifiedRandom rand, int currentPrefix) {
            int prefix = -1; // 默认前缀，如果不匹配任何条件则返回此值

            // 判断物品类型并为其分配不同的重铸前缀逻辑

            if (item.accessory) // 配件
            {
                int[][] accessoryReforgeTiers =
                [
                [PrefixID.Hard, PrefixID.Jagged, PrefixID.Brisk, PrefixID.Wild, GetCalPrefix("Quiet")],
                [PrefixID.Guarding, PrefixID.Spiked, PrefixID.Precise, PrefixID.Fleeting, PrefixID.Rash, GetCalPrefix("Cloaked")],
                [PrefixID.Armored, PrefixID.Angry, PrefixID.Hasty2, PrefixID.Intrepid, PrefixID.Arcane, GetCalPrefix("Camouflaged")],
                [PrefixID.Warding, PrefixID.Menacing, PrefixID.Lucky, PrefixID.Quick2, PrefixID.Violent, GetCalPrefix("Silent")]
                ];
                prefix = IteratePrefix(rand, accessoryReforgeTiers, currentPrefix);
            }
            else if (item.CountsAsClass<MeleeDamageClass>() || item.CountsAsClass<MeleeRangedHybridDamageClass>()
                || item.CountsAsClass<SummonMeleeSpeedDamageClass>()) // 近战
            {
                int[][] meleeReforgeTiers;

                if (PrefixLegacy.ItemSets.ItemsThatCanHaveLegendary2[item.type]) // 特定武器的特殊前缀
                {
                    meleeReforgeTiers =
                    [
                    [PrefixID.Keen, PrefixID.Forceful, PrefixID.Strong],
                    [PrefixID.Hurtful, PrefixID.Ruthless, PrefixID.Zealous],
                    [PrefixID.Superior, PrefixID.Demonic, PrefixID.Godly],
                    [PrefixID.Legendary2]
                    ];
                }
                else if (PrefixLegacy.ItemSets.SwordsHammersAxesPicks[item.type]
                    || (item.ModItem != null && OverWeaponFixMeleePerg(item))) // 剑、锤子、斧头、镐子等
                {
                    meleeReforgeTiers =
                    [
                    [PrefixID.Keen, PrefixID.Nimble, PrefixID.Nasty, PrefixID.Light, PrefixID.Heavy, PrefixID.Light, PrefixID.Forceful, PrefixID.Strong],
                    [PrefixID.Hurtful, PrefixID.Ruthless, PrefixID.Zealous, PrefixID.Quick, PrefixID.Pointy, PrefixID.Bulky],
                    [PrefixID.Murderous, PrefixID.Agile, PrefixID.Large, PrefixID.Dangerous, PrefixID.Sharp],
                    [PrefixID.Massive, PrefixID.Unpleasant, PrefixID.Savage, PrefixID.Superior],
                    [PrefixID.Demonic, PrefixID.Deadly2, PrefixID.Godly],
                    [PrefixID.Legendary]
                    ];

                    if (item.IsTool()) {//我暂时没能考虑到这一点，原灾厄考虑到了，所以便补上这个，对于工具来讲，轻和传奇是一样重要的，在最后一行添加上轻的标签让一切看起来显得合理
                        meleeReforgeTiers =
                        [
                        [PrefixID.Keen, PrefixID.Nimble, PrefixID.Nasty, PrefixID.Heavy, PrefixID.Forceful, PrefixID.Strong],
                        [PrefixID.Hurtful, PrefixID.Ruthless, PrefixID.Zealous, PrefixID.Quick, PrefixID.Pointy, PrefixID.Bulky],
                        [PrefixID.Murderous, PrefixID.Agile, PrefixID.Large, PrefixID.Dangerous, PrefixID.Sharp],
                        [PrefixID.Massive, PrefixID.Unpleasant, PrefixID.Savage, PrefixID.Superior],
                        [PrefixID.Demonic, PrefixID.Deadly2, PrefixID.Godly],
                        [PrefixID.Legendary, PrefixID.Light]
                        ];
                    }
                }
                else // 其他近战武器
                {
                    meleeReforgeTiers =
                    [
                    [PrefixID.Keen, PrefixID.Forceful, PrefixID.Strong],
                    [PrefixID.Hurtful, PrefixID.Ruthless, PrefixID.Zealous],
                    [PrefixID.Superior, PrefixID.Demonic],
                    [PrefixID.Godly]
                    ];
                }
                prefix = IteratePrefix(rand, meleeReforgeTiers, currentPrefix);
            }
            else if (item.CountsAsClass<RangedDamageClass>()) // 远程
            {
                int[][] rangedReforgeTiers =
                [
                [PrefixID.Keen, PrefixID.Nimble, PrefixID.Nasty, PrefixID.Powerful, PrefixID.Forceful, PrefixID.Strong],
                [PrefixID.Hurtful, PrefixID.Ruthless, PrefixID.Zealous, PrefixID.Quick, PrefixID.Intimidating],
                [PrefixID.Murderous, PrefixID.Agile, PrefixID.Hasty, PrefixID.Staunch, PrefixID.Unpleasant],
                [PrefixID.Superior, PrefixID.Demonic, PrefixID.Sighted],
                [PrefixID.Godly, PrefixID.Rapid, PrefixID.Deadly, PrefixID.Deadly2],
                [PrefixID.Unreal]
                ];
                prefix = IteratePrefix(rand, rangedReforgeTiers, currentPrefix);
            }
            else if (item.CountsAsClass<MagicDamageClass>() || item.CountsAsClass<MagicSummonHybridDamageClass>()) // 魔法
            {
                int[][] magicReforgeTiers =
                [
                [PrefixID.Keen, PrefixID.Nimble, PrefixID.Nasty, PrefixID.Furious, PrefixID.Forceful, PrefixID.Strong],
                [PrefixID.Hurtful, PrefixID.Ruthless, PrefixID.Zealous, PrefixID.Quick, PrefixID.Taboo, PrefixID.Manic],
                [PrefixID.Murderous, PrefixID.Agile, PrefixID.Adept, PrefixID.Celestial, PrefixID.Unpleasant],
                [PrefixID.Superior, PrefixID.Demonic, PrefixID.Mystic],
                [PrefixID.Godly, PrefixID.Masterful, PrefixID.Deadly2],
                [PrefixID.Mythical]
                ];
                prefix = IteratePrefix(rand, magicReforgeTiers, currentPrefix);
            }
            else if (item.CountsAsClass<SummonDamageClass>()) // 召唤
            {
                int[][] summonReforgeTiers =
                [
                [PrefixID.Nimble, PrefixID.Furious],
                [PrefixID.Forceful, PrefixID.Strong, PrefixID.Quick, PrefixID.Taboo, PrefixID.Manic],
                [PrefixID.Hurtful, PrefixID.Adept, PrefixID.Celestial],
                [PrefixID.Superior, PrefixID.Demonic, PrefixID.Mystic, PrefixID.Deadly2],
                [PrefixID.Masterful, PrefixID.Godly],
                [PrefixID.Mythical, PrefixID.Ruthless]
                ];
                prefix = IteratePrefix(rand, summonReforgeTiers, currentPrefix);
            }
            else if (item.CountsAsClass<ThrowingDamageClass>()) // 投掷/盗贼
            {
                int[][] rogueReforgeTiers =
                [
                [PrefixID.Keen, PrefixID.Nimble, PrefixID.Nasty, PrefixID.Forceful, PrefixID.Strong, GetCalPrefix("Radical"), GetCalPrefix("Pointy")],
                [PrefixID.Hurtful, PrefixID.Ruthless, PrefixID.Zealous, PrefixID.Quick, GetCalPrefix("Sharp"), GetCalPrefix("Glorious")],
                [PrefixID.Murderous, PrefixID.Agile, PrefixID.Unpleasant, GetCalPrefix("Feathered"), GetCalPrefix("Sleek"), GetCalPrefix("Hefty")],
                [PrefixID.Superior, PrefixID.Demonic, GetCalPrefix("Mighty"), GetCalPrefix("Serrated")],
                [PrefixID.Godly, PrefixID.Deadly2, GetCalPrefix("Vicious"), GetCalPrefix("Lethal")],
                [GetCalPrefix("Flawless")]
                ];
                prefix = IteratePrefix(rand, rogueReforgeTiers, currentPrefix);
            }

            return prefix;
        }
    }

}

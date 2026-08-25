using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Crushers
{
    /// <summary>
    /// 可粉碎矿物白名单:2 矿进 3 矿出的直接增产方案,产物与投入同种,
    /// 下游熔炼走焚化炉既有配方,零新增物品。
    /// 自包含名单不依赖其他系统的加载时序,留 <see cref="RegisterCrushable"/> 扩展点
    /// </summary>
    internal static class CrusherRecipes
    {
        /// <summary>单次作业投入的矿数</summary>
        public const int InputStack = 2;
        /// <summary>单次作业产出的矿数</summary>
        public const int OutputStack = 3;

        private static HashSet<int> _crushables;

        private static HashSet<int> Crushables {
            get {
                _crushables ??= [
                    //基础八矿
                    ItemID.CopperOre, ItemID.TinOre, ItemID.IronOre, ItemID.LeadOre,
                    ItemID.SilverOre, ItemID.TungstenOre, ItemID.GoldOre, ItemID.PlatinumOre,
                    //邪恶矿与特殊矿
                    ItemID.DemoniteOre, ItemID.CrimtaneOre, ItemID.Meteorite,
                    ItemID.Obsidian, ItemID.Hellstone,
                    //困难模式矿
                    ItemID.CobaltOre, ItemID.PalladiumOre, ItemID.MythrilOre, ItemID.OrichalcumOre,
                    ItemID.AdamantiteOre, ItemID.TitaniumOre, ItemID.ChlorophyteOre, ItemID.LunarOre,
                ];
                return _crushables;
            }
        }

        public static bool CanCrush(Item item)
            => item != null && !item.IsAir && Crushables.Contains(item.type);

        public static bool CanCrushType(int itemType) => Crushables.Contains(itemType);

        /// <summary>登记额外可粉碎矿物(供扩展/灾厄矿后续接入)</summary>
        public static void RegisterCrushable(int itemType) => Crushables.Add(itemType);

        public static void Unload() => _crushables = null;
    }
}

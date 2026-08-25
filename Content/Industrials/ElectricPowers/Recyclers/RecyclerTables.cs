using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Recyclers
{
    /// <summary>
    /// 回收拆解表:武器/盔甲/饰品按稀有度换锭,不做配方逆向(微光分解归转化槽管)。<br/>
    /// 锭种按世界矿层解析(铁/铅这类二选一跟世界走),硬模式矿层未探明时逐级向下回退;
    /// 防循环护栏是产出价值上限(锭总买价不超过物品价值的一半),不设禁用名单
    /// </summary>
    internal static class RecyclerTables
    {
        /// <summary>产出价值占物品价值的上限比例</summary>
        private const float ValueCapRatio = 0.5f;

        /// <summary>装备可否拆解:武器/盔甲/饰品,不收耗材/堆叠物/收藏品/任务品</summary>
        public static bool CanRecycle(Item item) {
            if (item == null || item.IsAir) {
                return false;
            }
            if (item.maxStack != 1 || item.consumable) {
                return false;
            }
            if (item.favorited || item.questItem) {
                return false;
            }
            return item.damage > 0 || item.defense > 0 || item.accessory;
        }

        /// <summary>世界铁层锭</summary>
        private static int IronTierBar()
            => WorldGen.SavedOreTiers.Iron == TileID.Lead ? ItemID.LeadBar : ItemID.IronBar;

        private static int SilverTierBar()
            => WorldGen.SavedOreTiers.Silver == TileID.Tungsten ? ItemID.TungstenBar : ItemID.SilverBar;

        private static int GoldTierBar()
            => WorldGen.SavedOreTiers.Gold == TileID.Platinum ? ItemID.PlatinumBar : ItemID.GoldBar;

        //硬模式三层:祭坛未破(-1)时逐级回退到已知层
        private static int CobaltTierBar() {
            int saved = WorldGen.SavedOreTiers.Cobalt;
            if (saved < 0) {
                return GoldTierBar();
            }
            return saved == TileID.Palladium ? ItemID.PalladiumBar : ItemID.CobaltBar;
        }

        private static int MythrilTierBar() {
            int saved = WorldGen.SavedOreTiers.Mythril;
            if (saved < 0) {
                return CobaltTierBar();
            }
            return saved == TileID.Orichalcum ? ItemID.OrichalcumBar : ItemID.MythrilBar;
        }

        private static int AdamantiteTierBar() {
            int saved = WorldGen.SavedOreTiers.Adamantite;
            if (saved < 0) {
                return MythrilTierBar();
            }
            return saved == TileID.Titanium ? ItemID.TitaniumBar : ItemID.AdamantiteBar;
        }

        /// <summary>稀有度→(锭种,基础数量);锭种确定性解析,各端一致</summary>
        public static (int BarType, int BaseCount) ResolveByRarity(int rarity) {
            if (rarity <= ItemRarityID.White) {
                return (IronTierBar(), 2);
            }
            return rarity switch {
                ItemRarityID.Blue => (SilverTierBar(), 2),
                ItemRarityID.Green => (GoldTierBar(), 2),
                ItemRarityID.Orange => (GoldTierBar(), 3),
                ItemRarityID.LightRed => (CobaltTierBar(), 2),
                ItemRarityID.Pink => (MythrilTierBar(), 2),
                ItemRarityID.LightPurple => (AdamantiteTierBar(), 2),
                ItemRarityID.Lime => (AdamantiteTierBar(), 3),
                ItemRarityID.Yellow => (ItemID.ChlorophyteBar, 2),
                //Cyan 及以上(含模组自定稀有度)统一封顶叶绿
                _ => (ItemID.ChlorophyteBar, 3),
            };
        }

        /// <summary>UI 预估产物锭种(不掷骰的确定性部分)</summary>
        public static int PreviewBar(Item source) => ResolveByRarity(source.rare).BarType;

        /// <summary>
        /// 权威端掷骰:数量带 ±1 抖动,再套价值上限;
        /// 被上限削空时降级为 1 枚铁层锭保底(垃圾装备拆不出高价锭)
        /// </summary>
        public static void RollOutput(Item source, UnifiedRandom rand, out int barType, out int count) {
            (barType, int baseCount) = ResolveByRarity(source.rare);
            count = baseCount + rand.Next(-1, 2);
            if (count < 1) {
                count = 1;
            }

            //价值护栏:锭总买价不超过物品价值的一半
            float cap = source.value * ValueCapRatio;
            int barValue = ContentSamples.ItemsByType.TryGetValue(barType, out Item bar) ? bar.value : 0;
            while (count > 1 && barValue * count > cap) {
                count--;
            }
            if (barValue * count > cap) {
                barType = IronTierBar();
                count = 1;
            }
        }
    }
}

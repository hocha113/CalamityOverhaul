using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics
{
    /// <summary>
    /// 残酷遗物系列基类：只负责贴图路径、系列稀有度、默认体积与尾行系列标识。
    /// 职责就此封死，效果逻辑全部由子类实现，不要往这里加任何字段或逻辑
    /// </summary>
    internal abstract class BaseBrutalRelic : ModItem
    {
        public override string Texture => CWRConstant.Item_BrutalRelic + GetType().Name;

        public override void SetDefaults() {
            Item.width = 40;
            Item.height = 40;
            Item.accessory = true;
            Item.maxStack = 1;
            Item.rare = ModContent.RarityType<BrutalRelicRarity>();
            Item.value = Item.buyPrice(0, 10, 0, 0);//默认价，子类按同期基准覆写
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "BrutalRelicSeries", CWRItem.BrutalRelicSeriesText.Value) {
                OverrideColor = BrutalRelicRarity.PulseColor
            });
        }
    }
}

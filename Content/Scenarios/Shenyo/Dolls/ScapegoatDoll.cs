using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Dolls
{
    /// <summary>
    /// 替死娃娃:沈幽在邪恶地形初访时送出的护身物。
    /// 放在背包里即可生效,替玩家挡下一次死亡后碎裂消失,
    /// 结算逻辑在 <see cref="ScapegoatDollPlayer"/>;贴图暂借原版巫毒玩偶
    /// </summary>
    internal class ScapegoatDoll : ModItem
    {
        /// <summary>挡死结算时的漂浮字</summary>
        public static LocalizedText ShatterText { get; private set; }

        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 1;
            ShatterText = this.GetLocalization(nameof(ShatterText), () => "替死娃娃碎了");
        }

        public override void SetDefaults() {
            Item.width = 22;
            Item.height = 30;
            Item.maxStack = Item.CommonMaxStack;
            Item.value = Item.sellPrice(gold: 1);
            Item.rare = ItemRarityID.Green;
        }
    }
}

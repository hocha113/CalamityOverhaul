using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp
{
    /// <summary>渊晶海虾宝藏袋：货币 + 共享掉落池（签名武器五取一与深渊材料，表在 <see cref="SeaShrimpBoss.RegisterSharedLoot"/>）</summary>
    internal class SeaShrimpTreasureBag : SeaShrimpModItem
    {
        //自绘贴图（原稿 20×17，按项目 2x 约定近邻放大为 40×34）
        public override string Texture => CWRConstant.NPC + "SeaShrimp/SeaShrimpTreasureBag";

        public override void SetStaticDefaults() {
            ItemID.Sets.BossBag[Type] = true;
            Item.ResearchUnlockCount = 3;
        }

        public override void SetDefaults() {
            Item.width = 40;
            Item.height = 34;
            Item.maxStack = 9999;
            Item.consumable = true;
            Item.rare = ItemRarityID.Cyan;
            Item.expert = true;
        }

        public override bool CanRightClick() => true;

        public override void ModifyItemLoot(ItemLoot itemLoot) {
            itemLoot.Add(ItemDropRule.CoinsBasedOnNPCValue(ModContent.NPCType<SeaShrimpBoss>()));
            SeaShrimpBoss.RegisterSharedLoot(rule => itemLoot.Add(rule), expert: true);
        }
    }
}

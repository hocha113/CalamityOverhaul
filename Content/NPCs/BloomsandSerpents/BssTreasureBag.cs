using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>荒花沙蟒宝藏袋：货币 + 共享掉落池（荒花兵装五取一、荒漠沙器四取一与沙漠材料，表在 <see cref="BssHead.RegisterSharedLoot"/>）</summary>
    internal class BssTreasureBag : BssModItem
    {
        public override string Texture => CWRConstant.NPC + "BSS/TreasureBag";

        public override void SetStaticDefaults() {
            ItemID.Sets.BossBag[Type] = true;
            //克眼后的困难前 Boss：开发者套装照原版只在特殊种子掉
            ItemID.Sets.PreHardmodeLikeBossBag[Type] = true;
            Item.ResearchUnlockCount = 3;
        }

        public override void SetDefaults() {
            Item.width = 40;
            Item.height = 40;
            Item.maxStack = 9999;
            Item.consumable = true;
            Item.rare = ItemRarityID.Cyan;
            Item.expert = true;
        }

        public override bool CanRightClick() => true;

        public override void ModifyItemLoot(ItemLoot itemLoot) {
            itemLoot.Add(ItemDropRule.CoinsBasedOnNPCValue(ModContent.NPCType<BssHead>()));
            BssHead.RegisterSharedLoot(rule => itemLoot.Add(rule), expert: true);
        }
    }
}

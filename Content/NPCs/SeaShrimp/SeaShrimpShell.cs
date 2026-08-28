using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp
{
    /// <summary>渊晶虾壳，渊晶海虾的专属掉落材料，深渊系列武器的必需合成料</summary>
    internal class SeaShrimpShell : ModItem
    {
        public override string Texture => CWRConstant.NPC + "SeaShrimp/SeaShrimpShell";

        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 25;
        }

        public override void SetDefaults() {
            Item.width = 34;
            Item.height = 34;
            Item.maxStack = Item.CommonMaxStack;
            Item.value = Item.sellPrice(0, 0, 40);
            Item.rare = ItemRarityID.Lime;
        }
    }
}

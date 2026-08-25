using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 狱水明珠：不溺者必掉的兑换材料（3~6 枚）。后续波次的亡灵集市联动接口预留，
    /// 本波只作为战利品存在。贴图借原版黑珍珠（零新画像素）
    /// </summary>
    internal class SumpPearl : UndrownedModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.BlackPearl;

        public override void SetDefaults() {
            Item.width = 18;
            Item.height = 18;
            Item.maxStack = Item.CommonMaxStack;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(0, 0, 40);
        }
    }
}

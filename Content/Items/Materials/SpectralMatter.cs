using CalamityOverhaul.Content.UIs.SupertableUIs;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class SpectralMatter : ModItem
    {
        public override string Texture => CWRConstant.Item + "Materials/SpectralMatter";
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 9999;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 5));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
        }
        public override void SetDefaults() {
            Item.width = Item.height = 25;
            Item.maxStack = 99;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(gold: 153);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems_SpectralMatter;
        }
    }
}

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items
{
    internal class Oceanfragments : ModItem
    {
        public override string Texture => CWRConstant.Item_Other + "Oceanfragments";

        public override void SetDefaults() {
            Item.useTime = Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.width = Item.height = 32;
            Item.maxStack = 9999;
            Item.value = 6000;
            Item.rare = ItemRarityID.Green;
        }

        public override bool? UseItem(Player player) {
            return true;
        }
    }
}

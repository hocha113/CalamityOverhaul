using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Sulfseas.OldDukes.Items
{
    internal class Oceanfragments : ModItem
    {
        public override string Texture => CWRConstant.Item_Other + "Oceanfragments";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.value = 6000;
            Item.rare = ItemRarityID.Green;
        }
    }
}

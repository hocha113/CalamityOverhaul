using CalamityOverhaul.Common;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria;

namespace CalamityOverhaul.Content.Items
{
    internal class MuzzleBrake : ModItem
    {
        public override string Texture => CWRConstant.Item + "MuzzleBrake";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.rare = ItemRarityID.Lime;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.CWR().LoadMuzzleBrake = true;
            player.CWR().PressureIncrease -= 0.5f;
        }
    }
}

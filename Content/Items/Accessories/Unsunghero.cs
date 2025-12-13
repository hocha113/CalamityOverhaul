using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    internal class Unsunghero : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "Unsunghero";
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(0, 15, 22, 0);
            Item.rare = ItemRarityID.Orange;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.CWR().IsUnsunghero = true;
            player.RefPlayerRogueStealthMax() += 0.1f;
        }
    }
}

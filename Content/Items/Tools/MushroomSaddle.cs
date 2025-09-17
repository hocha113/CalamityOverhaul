using CalamityOverhaul.Content.NPCs.Modifys;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class MushroomSaddle : ModItem
    {
        [VaultLoaden(CWRConstant.Item_Tools)]
        public static Asset<Texture2D> MushroomSaddlePlace;
        public ModifyCrabulon ModifyCrabulon;
        public override string Texture => CWRConstant.Item + "Tools/MushroomSaddle";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = Item.useAnimation = 12;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.consumable = true;
            Item.value = 25800;
            Item.rare = ItemRarityID.Cyan;
        }

        public override bool? UseItem(Player player) {
            return true;
        }
    }
}

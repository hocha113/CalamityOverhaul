using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class BlackMatterStick : ModItem
    {
        public override string Texture => CWRConstant.Item + "Materials/BlackMatterStick";
        public new string LocalizationCategory => "Items.Materials";

        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 9999;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 6));
        }

        public override void SetDefaults() {
            Item.width = Item.height = 25;
            Item.maxStack = 99;
            Item.rare = ItemRarityID.Lime;
            Item.value = Terraria.Item.sellPrice(gold: 999);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems5;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            spriteBatch.Draw(TextureAssets.Item[Type].Value, Item.Center - Main.screenPosition, null, lightColor, Main.GameUpdateCount * 0.1f, TextureAssets.Item[Type].Value.Size() / 2, 1, SpriteEffects.None, 0);
            return false;
        }
    }
}

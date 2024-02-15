using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria.GameContent;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using CalamityOverhaul.Content.UIs.SupertableUIs;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class SpectralMatter : ModItem
    {
        public override string Texture => CWRConstant.Item + "Materials/SpectralMatter";
        public new string LocalizationCategory => "Items.Materials";

        public override void SetDefaults() {
            Item.width = Item.height = 25;
            Item.maxStack = 99;
            Item.rare = ItemRarityID.Lime;
            Item.value = Terraria.Item.sellPrice(gold: 153);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems14;
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            DecayParticles.DrawItemIcon(spriteBatch, position, Color.White, Type);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            spriteBatch.Draw(TextureAssets.Item[Type].Value, Item.Center - Main.screenPosition, null, lightColor, Main.GameUpdateCount * 0.1f, TextureAssets.Item[Type].Value.Size() / 2, 1, SpriteEffects.None, 0);
            return false;
        }
    }
}

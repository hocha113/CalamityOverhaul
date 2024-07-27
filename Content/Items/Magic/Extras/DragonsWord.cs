using Terraria.ModLoader;
using Terraria.ID;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Terraria;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using Microsoft.Xna.Framework.Graphics;
using CalamityMod;
using CalamityMod.NPCs.Providence;

namespace CalamityOverhaul.Content.Items.Magic.Extras
{
    internal class DragonsWord : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "DragonsWord";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.damage = 122;
            Item.DamageType = DamageClass.Magic;
            Item.useTime = Item.useAnimation = 36;
            Item.rare = ItemRarityID.Red;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = Providence.HolyRaySound;
            Item.mana = 20;
            Item.shootSpeed = 6;
            Item.shoot = ModContent.ProjectileType<DragonsWordProj>();
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {
            Texture2D mainValue = CWRUtils.GetT2DValue(Texture + "Glow");
            spriteBatch.Draw(mainValue, Item.position - Main.screenPosition + new Vector2(Item.width / 2, -Item.height / 2), null, Color.White, rotation, mainValue.Size() / 2, scale, SpriteEffects.None, 0);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            for (int i = 0; i < 3; i++) {
                Vector2 vr = (MathHelper.TwoPi / 3f * i + Main.GameUpdateCount * 0.1f).ToRotationVector2();
                Projectile.NewProjectile(source, player.Center + vr * Main.rand.Next(22, 38), vr.RotatedByRandom(0.32f) * 3
                , type, damage, knockback, player.whoAmI, 0f, 0.03f);
            }
            return false;
        }
    }
}

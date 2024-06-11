using CalamityOverhaul.Common;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    internal class MarbleSpear : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "MarbleSpear";
        public override void SetDefaults() {
            Item.width = Item.height = 35;
            Item.damage = 18;
            Item.DamageType = DamageClass.Melee;
            Item.rare = ItemRarityID.Cyan;
            Item.value = Item.buyPrice(0, 0, 5, 0);
            Item.shoot = ModContent.ProjectileType<MarbleSpearHeld>();
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3.5f;
            Item.shootSpeed = 5f;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.channel = true;
        }
        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;
    }

    internal class MarbleSpearHeld : BaseRapiers
    {
        public override string Texture => CWRConstant.Item_Melee + "MarbleSpear";
        public override void SetRapiers() {
            overHitModeing = 60;
            SkialithVarSpeedMode = 3;
            PremanentToSkialthRot = -50;
            maxStabNum = 1;
            StabAmplitudeMin = 50;
            StabAmplitudeMax = 90;
            ShurikenOut = SoundID.Item1 with { Pitch = 0.24f };
        }

        public override void Draw3(Texture2D tex, Vector2 off, float fade, Color lightColor) {
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition + off + Projectile.velocity * 83
                , null, lightColor * fade, Projectile.rotation - MathHelper.ToRadians(20) * (Projectile.velocity.X > 0 ? 1 : -1)
                , new Vector2(Projectile.velocity.X > 0 ? 0 : tex.Width, tex.Height), Projectile.scale
                , Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
        }
    }
}

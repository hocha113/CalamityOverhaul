using CalamityOverhaul.Common;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class SporeBubbleBlaster : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "SporeBubbleBlaster";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.damage = 16;
            Item.DamageType = DamageClass.Ranged;
            Item.useAnimation = 60;
            Item.useTime = 2;
            Item.useLimitPerAnimation = 10;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.rare = ItemRarityID.Orange;
            Item.value = 600;
            Item.shootSpeed = 12;
            Item.shoot = ModContent.ProjectileType<SporeBobo>();
            Item.UseSound = CWRSound.SporeBubble;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 orgVelocity = velocity;
            velocity *= Main.rand.NextFloat(0.3f, 1f);
            velocity = velocity.RotatedByRandom(0.12f);
            Vector2 targetPos = position + orgVelocity * 300;
            if (Main.dayTime) {
                damage /= 2;//在白天只造成一半的伤害
            }
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, targetPos.X, targetPos.Y);
            return false;
        }
    }

    internal class SporeBobo : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.timeLeft = 300;
            Projectile.friendly = true;
            Projectile.extraUpdates = 13;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            if (++Projectile.ai[2] > 30) {
                Projectile.SmoothHomingBehavior(new Vector2(Projectile.ai[0], Projectile.ai[1]), 1, 0.1f);
            }
            Projectile.velocity *= 0.99f;

            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_SporeBobo>(Projectile.Center, Projectile.velocity / 2);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 30);
        }
    }

    internal class PRT_SporeBobo : BasePRT
    {
        public override string Texture => CWRConstant.Other + "SporeBobo";
        public override void SetProperty() {
            Scale = Main.rand.NextFloat(0.8f, 1.22f);
            Lifetime = Main.rand.Next(18, 36);
            Frame = TexValue.GetRectangle(Main.rand.Next(6), 6);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Opacity = Main.rand.NextFloat(0.2f, 0.6f);
        }

        public override void AI() {
            Lighting.AddLight(Position, Color.White.ToVector3() * 0.2f);//孢子需要微微发光
            Rotation += Velocity.X * 0.1f;
            if (Framing.GetTileSafely(Position.ToTileCoordinates16()).HasTile) {//如果和物块接触，迅速变小消失
                Scale -= 0.1f;
                if (Scale <= 0) {
                    Kill();
                }
                Scale = MathHelper.Clamp(Scale, 0, 2f);
            }

            if (LifetimeCompletion > 0.6f) {
                if (Opacity > 0f) {
                    Opacity -= 0.1f;
                }
            }
            else {
                if (Opacity < 1f) {
                    Opacity += 0.1f;
                }
            }

            Opacity = MathHelper.Clamp(Opacity, 0, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Color drawColor = Lighting.GetColor(Position.ToTileCoordinates()) * Opacity;
            spriteBatch.Draw(TexValue, Position - Main.screenPosition, Frame
                , drawColor, Rotation, Frame.Size() / 2, Scale, SpriteEffects.None, 0);
            return false;
        }
    }
}

using CalamityMod;
using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Rogue.Extras
{
    /// <summary>
    /// 沙匕
    /// </summary>
    internal class SandDagger : ModItem
    {
        public override string Texture => CWRConstant.Item + "Rogue/SandDagger";
        public override void SetDefaults() {
            Item.width = 48;
            Item.height = 48;
            Item.damage = 16;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 18;
            Item.knockBack = 3f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.value = CalamityGlobalItem.RarityGreenBuyPrice;
            Item.rare = ItemRarityID.Green;
            Item.shoot = ModContent.ProjectileType<SandDaggerThrow>();
            Item.shootSpeed = 10f;
            Item.DamageType = CWRLoad.RogueDamageClass;
        }
    }

    internal class SandDaggerThrow : BaseThrowable
    {
        public override string Texture => CWRConstant.Item + "Rogue/SandDaggerProj";
        private bool onTIle;
        private float tileRot;
        public override void SetThrowable() {
            Projectile.width = Projectile.height = 12;
            HandOnTwringMode = -20;
        }

        public override void FlyToMovementAI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (++Projectile.ai[2] > 20 && !onTIle) {
                Projectile.velocity.Y += 0.3f;
                Projectile.velocity.X *= 0.99f;
            }
            if (onTIle) {
                Projectile.rotation = tileRot;
                Projectile.velocity *= 0.6f;
            }
        }

        public override bool PreThrowOut() {
            SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
            Projectile.velocity = UnitToMouseV * 17.5f;
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            if (stealthStrike) {
                Projectile.damage *= 2;
                Projectile.ArmorPenetration = 10;
                Projectile.penetrate = 6;
                Projectile.extraUpdates = 3;
                Projectile.scale = 1.5f;
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            base.OnKill(timeLeft);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (!onTIle) {
                Projectile.velocity /= 10;
                tileRot = Projectile.rotation;
                onTIle = true;
            }

            Projectile.alpha -= 15;
            return false;
        }

        public override void DrawThrowable(Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation + (MathHelper.PiOver2 + OffsetRoting) * (Projectile.velocity.X > 0 ? 1 : -1)
                , TextureValue.Size() / 2, Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically, 0);
        }
    }
}

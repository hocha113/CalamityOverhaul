using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class FlintSpearProj : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "Spikes/FlintSpearProj";

        public Player Owner => Main.player[Projectile.owner];

        public Item elementalLance => Owner.HeldItem;

        public int dirk;

        protected float HoldoutRangeMin => -14f;
        protected float HoldoutRangeMax => 36f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 90;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 7;
            Projectile.hide = true;

        }

        public override void AI() {
            if (dirk == 0) {
                dirk = Math.Sign(Projectile.velocity.X);
            }
            Player player = Main.player[Projectile.owner];
            player.heldProj = Projectile.whoAmI;
            int duration = player.itemAnimationMax;
            if (Projectile.timeLeft > duration) {
                Projectile.timeLeft = duration;
            }
            Projectile.velocity = Vector2.Normalize(Projectile.velocity);
            float halfDuration = duration * 0.5f;
            float progress = Projectile.timeLeft < halfDuration ? Projectile.timeLeft / halfDuration : (duration - Projectile.timeLeft) / halfDuration;
            Projectile.Center = player.MountedCenter + Projectile.velocity.UnitVector() * 23 + Vector2.SmoothStep(Projectile.velocity * HoldoutRangeMin, Projectile.velocity * HoldoutRangeMax, progress);
            Projectile.rotation = Projectile.velocity.ToRotation();
            player.direction = dirk;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = CWRUtils.GetT2DValue(Texture);
            float rot = Projectile.rotation + MathHelper.ToRadians(60) + (Owner.direction > 0 ? 0 : MathHelper.ToRadians(60));
            Vector2 drawPosition = Projectile.Center - Main.screenPosition - Projectile.velocity.UnitVector() * 23;
            Vector2 origin = CWRUtils.GetOrig(texture);
            Main.EntitySpriteDraw(texture, drawPosition, null, Projectile.GetAlpha(lightColor)
                , rot, origin, Projectile.scale * 0.7f, Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
            return false;
        }
    }
}

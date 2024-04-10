using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons
{
    internal class TheRelicLuxorMagic : ModProjectile
    {
        internal PrimitiveTrail TrailDrawer;

        public override string Texture => CWRConstant.Projectile + "TheRelicLuxorMagicProj";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.alpha = 255;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center + Projectile.velocity * 5, Color.Gold.ToVector3());
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitNPC(target, hit, damageDone);
        }

        internal Color ColorFunction(float completionRatio) {
            return Color.Gold;
        }

        internal float WidthFunction(float completionRatio) {
            float amount = MathF.Pow(1f - completionRatio, 2.0f);
            return MathHelper.Lerp(0f, 22f * Projectile.scale, amount);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (TrailDrawer == null) {
                TrailDrawer = new PrimitiveTrail(WidthFunction, ColorFunction, null, GameShaders.Misc["CalamityMod:TrailStreak"]);
            }

            GameShaders.Misc["CalamityMod:TrailStreak"].SetMiscShaderAsset_1(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/Trails/ScarletDevilStreak"));
            TrailDrawer.Draw(Projectile.oldPos, Projectile.Size * 0.5f - Main.screenPosition, 30);

            Texture2D texture = CWRUtils.GetT2DValue(Texture);
            Color color = Color.White;
            float alp = Projectile.alpha / 255f;
            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                color * alp,
                Projectile.rotation + MathHelper.PiOver2,
                CWRUtils.GetOrig(texture),
                Projectile.scale,
                SpriteEffects.None,
                0
                );
            return false;
        }
    }
}

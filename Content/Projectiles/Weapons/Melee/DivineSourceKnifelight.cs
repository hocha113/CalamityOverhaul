using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class DivineSourceKnifelight : ModProjectile
    {
        internal PrimitiveTrail TrailDrawer;

        public override string Texture => CWRConstant.Placeholder;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 25;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 600;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void AI() {
            Player owners = CWRUtils.GetPlayerInstance(Projectile.owner);
            if (owners == null) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = owners.itemLocation + (owners.itemRotation - MathHelper.ToRadians(80)).ToRotationVector2() * Projectile.ai[0];
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {

        }

        public override void OnKill(int timeLeft) {
            base.OnKill(timeLeft);
        }

        internal Color ColorFunction(float completionRatio) {
            float amount = MathHelper.Lerp(0.65f, 1f, (float)Math.Cos((0f - Main.GlobalTimeWrappedHourly) * 3f) * 0.5f + 0.5f);
            float num = Utils.GetLerpValue(1f, 0.64f, completionRatio, clamped: true) * Projectile.Opacity;

            Color value = Color.Lerp(new Color(255, 223, 186), new Color(255, 218, 185), (float)Math.Sin(completionRatio * MathF.PI * 1.6f - Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f);

            return Color.Lerp(new Color(255, 248, 220), value, amount) * num;
        }

        internal float WidthFunction(float completionRatio) {
            float amount = (float)Math.Pow(1f - completionRatio, 3.0);
            return MathHelper.Lerp(0f, 42f * Projectile.scale * Projectile.Opacity, amount);
        }

        public override bool PreDraw(ref Color lightColor) {
            //if (TrailDrawer == null)
            //{
            //    TrailDrawer = new PrimitiveTrail(WidthFunction, ColorFunction, null, GameShaders.Misc["CalamityMod:TrailStreak"]);
            //}

            //GameShaders.Misc["CalamityMod:TrailStreak"].SetShaderTexture(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/Trails/ScarletDevilStreak"));
            //TrailDrawer.Draw(Projectile.oldPos, Projectile.Size * 0.5f - Main.screenPosition, 30);

            return false;
        }
    }
}

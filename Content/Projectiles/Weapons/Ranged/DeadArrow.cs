using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Common.Interfaces;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Drawing;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class DeadArrow : ModProjectile
    {
        internal PrimitiveTrail TrailDrawer;

        public override string Texture => CWRConstant.Cay_Proj_Ranged + "DWArrow2";

        public int Status { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        public int Behavior { get => (int)Projectile.ai[1]; set => Projectile.ai[1] = value; }
        public int ThisTimeValue { get => (int)Projectile.ai[2]; set => Projectile.ai[2] = value; }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 25;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.scale = 1;
            Projectile.alpha = 80;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 3;
            Projectile.MaxUpdates = 2;
            Projectile.timeLeft = 70 * Projectile.MaxUpdates;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12 * Projectile.MaxUpdates;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity += new Vector2(0, 0.1f);
        }

        internal Color ColorFunction(float completionRatio) {
            float amount = MathHelper.Lerp(0.65f, 1f, (float)Math.Cos((0f - Main.GlobalTimeWrappedHourly) * 3f) * 0.5f + 0.5f);
            float num = Utils.GetLerpValue(1f, 0.64f, completionRatio, clamped: true) * Projectile.Opacity;
            Color value = Color.Lerp(new Color(192, 192, 192), new Color(211, 211, 211), (float)Math.Sin(completionRatio * MathF.PI * 1.6f - Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f);
            return Color.Lerp(new Color(255, 255, 255), value, amount) * num;
        }

        internal float WidthFunction(float completionRatio) {
            float amount = (float)Math.Pow(1f - completionRatio, 3.0);
            return MathHelper.Lerp(0f, 12f * Projectile.scale * Projectile.Opacity, amount);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (TrailDrawer == null) {
                TrailDrawer = new PrimitiveTrail(WidthFunction, ColorFunction, null, GameShaders.Misc["CalamityMod:TrailStreak"]);
            }

            GameShaders.Misc["CalamityMod:TrailStreak"].SetShaderTexture(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/Trails/ScarletDevilStreak"));
            TrailDrawer.Draw(Projectile.oldPos, Projectile.Size * 0.5f - Main.screenPosition, 30);
            Texture2D value = ModContent.Request<Texture2D>(Texture).Value;
            Main.EntitySpriteDraw(
                value,
                Projectile.Center - Main.screenPosition,
                null,
                Color.Lerp(lightColor, Color.White, 0.5f),
                Projectile.rotation + MathHelper.PiOver2,
                value.Size() / 2f,
                Projectile.scale,
                SpriteEffects.None,
                0
                );
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int j = 0; j < 10; j++) {
                int num2 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.ShadowbeamStaff, 0f, 0f, 100, default, 2f);
                Main.dust[num2].noGravity = true;
                Main.dust[num2].velocity *= 5f;
                num2 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.ShadowbeamStaff, 0f, 0f, 100, default, 1.5f);
                Main.dust[num2].velocity *= 2f;
            }
        }

        public override bool ShouldUpdatePosition() {
            return true;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return null;
        }
    }
}

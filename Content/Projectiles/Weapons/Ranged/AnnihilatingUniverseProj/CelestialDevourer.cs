using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.AnnihilatingUniverseProj
{
    internal class CelestialDevourer : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override bool IsLoadingEnabled(Mod mod) {
            return !CWRServerConfig.Instance.AddExtrasContent ? false : base.IsLoadingEnabled(mod);
        }
        public override void SetDefaults() {
            Projectile.height = 24;
            Projectile.width = 24;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            if (Projectile.ai[0] == -1f)
                return;

            if (!Main.npc[(int)Projectile.ai[0]].active)
                Projectile.Kill();

            Projectile.ai[1] += MathHelper.Pi / 30f;
            Projectile.Opacity = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);

            if (Projectile.Opacity == 1f && Main.rand.NextBool(15)) {
                Dust dust = Main.dust[Dust.NewDust(Projectile.Top, 0, 0, DustID.RainbowMk2, 0f, 0f, 100, new Color(150, 100, 255, 255), 1f)];
                dust.velocity.X = 0f;
                dust.noGravity = true;
                dust.fadeIn = 1f;
                dust.position = Projectile.Center + Vector2.UnitY.RotatedByRandom(MathHelper.TwoPi) * (4f * Main.rand.NextFloat() + 26f);
                dust.scale = 0.5f;
            }
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode();
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 36; i++)//生成这种粒子不是好主意
                {
                    Vector2 particleSpeed = CWRUtils.GetRandomVevtor(0, 360, Main.rand.Next(6, 9));
                    Vector2 pos = Projectile.Center;
                    BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                        , Main.rand.NextFloat(0.3f, 0.7f), Color.Purple, 30, 1, 1.5f, hueShift: 0.0f);
                    PRTLoader.AddParticle(energyLeak);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float lerpMult = Utils.GetLerpValue(15f, 30f, Projectile.timeLeft, clamped: true) * Utils.GetLerpValue(240f, 200f, Projectile.timeLeft, clamped: true) * (1f + 0.2f * (float)Math.Cos(Main.GlobalTimeWrappedHourly % 30f / 0.5f * (MathHelper.Pi * 2f) * 3f)) * 0.8f;

            Texture2D texture = ModContent.Request<Texture2D>("CalamityMod/Projectiles/StarProj").Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY);
            Color baseColor = new Color(150, 100, 255, 255) * Projectile.Opacity;
            baseColor *= 0.5f;
            baseColor.A = 0;
            Color colorA = baseColor;
            Color colorB = baseColor * 0.5f;
            colorA *= lerpMult;
            colorB *= lerpMult;
            Vector2 origin = texture.Size() / 2f;
            Vector2 scale = new Vector2(3f, 9f) * Projectile.Opacity * lerpMult * Projectile.scale;

            SpriteEffects spriteEffects = SpriteEffects.None;
            if (Projectile.spriteDirection == -1)
                spriteEffects = SpriteEffects.FlipHorizontally;

            Main.EntitySpriteDraw(texture, drawPos, null, colorA, MathHelper.PiOver2, origin, scale, spriteEffects, 0);
            Main.EntitySpriteDraw(texture, drawPos, null, colorA, 0f, origin, scale, spriteEffects, 0);
            Main.EntitySpriteDraw(texture, drawPos, null, colorB, MathHelper.PiOver2, origin, scale * 0.8f, spriteEffects, 0);
            Main.EntitySpriteDraw(texture, drawPos, null, colorB, 0f, origin, scale * 0.8f, spriteEffects, 0);

            Main.EntitySpriteDraw(texture, drawPos, null, colorA, MathHelper.PiOver2 + Projectile.ai[1] * 0.25f, origin, scale, spriteEffects, 0);
            Main.EntitySpriteDraw(texture, drawPos, null, colorA, Projectile.ai[1] * 0.25f, origin, scale, spriteEffects, 0);
            Main.EntitySpriteDraw(texture, drawPos, null, colorB, MathHelper.PiOver2 + Projectile.ai[1] * 0.5f, origin, scale * 0.8f, spriteEffects, 0);
            Main.EntitySpriteDraw(texture, drawPos, null, colorB, Projectile.ai[1] * 0.5f, origin, scale * 0.8f, spriteEffects, 0);

            Main.EntitySpriteDraw(texture, drawPos, null, colorA, MathHelper.PiOver4, origin, scale * 0.6f, spriteEffects, 0);
            Main.EntitySpriteDraw(texture, drawPos, null, colorA, MathHelper.PiOver4 * 3f, origin, scale * 0.6f, spriteEffects, 0);
            Main.EntitySpriteDraw(texture, drawPos, null, colorB, MathHelper.PiOver4, origin, scale * 0.4f, spriteEffects, 0);
            Main.EntitySpriteDraw(texture, drawPos, null, colorB, MathHelper.PiOver4 * 3f, origin, scale * 0.4f, spriteEffects, 0);

            Main.EntitySpriteDraw(texture, drawPos, null, colorA, MathHelper.PiOver4 + Projectile.ai[1] * 0.75f, origin, scale * 0.6f, spriteEffects, 0);
            Main.EntitySpriteDraw(texture, drawPos, null, colorA, MathHelper.PiOver4 * 3f + Projectile.ai[1] * 0.75f, origin, scale * 0.6f, spriteEffects, 0);
            Main.EntitySpriteDraw(texture, drawPos, null, colorB, MathHelper.PiOver4 + Projectile.ai[1], origin, scale * 0.4f, spriteEffects, 0);
            Main.EntitySpriteDraw(texture, drawPos, null, colorB, MathHelper.PiOver4 * 3f + Projectile.ai[1], origin, scale * 0.4f, spriteEffects, 0);

            return false;
        }
    }
}

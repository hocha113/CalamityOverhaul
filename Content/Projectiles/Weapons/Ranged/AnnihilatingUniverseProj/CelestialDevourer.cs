using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.AnnihilatingUniverseProj
{
    internal class CelestialDevourer : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        [VaultLoaden("@CalamityMod/Projectiles/StarProj")]
        private static Asset<Texture2D> starAsset = null;
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
            if (Projectile.ai[0] == -1f || !Main.npc[(int)Projectile.ai[0]].active) {
                Projectile.Kill();
                return;
            }

            Projectile.ai[1] += MathHelper.Pi / 30f;
            Projectile.Opacity = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);

            if (Projectile.Opacity == 1f && Main.rand.NextBool(15)) {
                GenerateDust();
            }
        }

        private void GenerateDust() {
            Vector2 dustPosition = Projectile.Center + Vector2.UnitY.RotatedByRandom(MathHelper.TwoPi)
                                   * (4f * Main.rand.NextFloat() + 26f);
            Dust dust = Main.dust[Dust.NewDust(Projectile.Top, 0, 0, DustID.RainbowMk2, 0f, 0f, 100,
                                               new Color(150, 100, 255, 255), 1f)];
            dust.velocity = Vector2.Zero;
            dust.noGravity = true;
            dust.fadeIn = 1f;
            dust.position = dustPosition;
            dust.scale = 0.5f;
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode();
            if (!VaultUtils.isServer) {//生成这种粒子不是好主意
                for (int i = 0; i < 36; i++) {
                    Vector2 particleSpeed = VaultUtils.RandVrInAngleRange(0, 360, Main.rand.Next(16, 49));
                    Vector2 pos = Projectile.Center;
                    BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                        , Main.rand.NextFloat(0.3f, 0.7f), Color.Purple, 60, 1, 1.5f, hueShift: 0.0f);
                    PRTLoader.AddParticle(energyLeak);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float lerpMult = Utils.GetLerpValue(15f, 30f, Projectile.timeLeft, clamped: true)
                            * Utils.GetLerpValue(240f, 200f, Projectile.timeLeft, clamped: true)
                            * (1f + 0.2f * (float)Math.Cos(Main.GlobalTimeWrappedHourly % 30f / 0.5f * (MathHelper.Pi * 2f) * 3f)) * 0.8f;

            Texture2D texture = starAsset.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY);
            Color baseColor = new Color(150, 100, 255, 255) * Projectile.Opacity * 0.5f;
            baseColor.A = 0;
            Color colorA = baseColor * lerpMult;
            Color colorB = baseColor * 0.5f * lerpMult;
            Vector2 origin = texture.Size() / 2f;
            Vector2 scale = new Vector2(3f, 9f) * Projectile.Opacity * lerpMult * Projectile.scale;

            SpriteEffects spriteEffects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            void DrawSprite(float rotation, Vector2 scaleModifier, Color color) {
                Main.EntitySpriteDraw(texture, drawPos, null, color, rotation, origin, scale * scaleModifier, spriteEffects, 0);
            }

            for (int i = 0; i < 2; i++) {
                float offset = i * MathHelper.PiOver2;
                DrawSprite(offset, Vector2.One, colorA);
                DrawSprite(offset, Vector2.One * 0.8f, colorB);

                DrawSprite(offset + Projectile.ai[1] * 0.25f, Vector2.One, colorA);
                DrawSprite(offset + Projectile.ai[1] * 0.5f, Vector2.One * 0.8f, colorB);
            }

            for (int i = 0; i < 2; i++) {
                float offset = MathHelper.PiOver4 * (1 + i * 2);
                DrawSprite(offset, Vector2.One * 0.6f, colorA);
                DrawSprite(offset, Vector2.One * 0.4f, colorB);

                DrawSprite(offset + Projectile.ai[1] * 0.75f, Vector2.One * 0.6f, colorA);
                DrawSprite(offset + Projectile.ai[1], Vector2.One * 0.4f, colorB);
            }

            return false;
        }
    }
}

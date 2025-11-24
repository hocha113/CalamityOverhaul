using CalamityMod;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// <summary>
    /// 伽马射线
    /// </summary>
    internal class GammaRayBeam : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int MaxTrailLength = 20;
        private float beamWidth = 8f;
        private float maxBeamWidth = 24f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = MaxTrailLength;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            //宽度变化 - 前期展开，后期收缩
            float lifeRatio = 1f - Projectile.timeLeft / 180f;
            if (lifeRatio < 0.2f) {
                beamWidth = MathHelper.Lerp(4f, maxBeamWidth, lifeRatio / 0.2f);
            }
            else if (lifeRatio > 0.8f) {
                beamWidth = MathHelper.Lerp(maxBeamWidth, 4f, (lifeRatio - 0.8f) / 0.2f);
            }
            else {
                beamWidth = maxBeamWidth;
            }

            //光尘轨迹
            if (Main.rand.NextBool(2)) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(beamWidth, beamWidth);
                Dust dust = Dust.NewDustPerfect(dustPos, DustID.Electric, Vector2.Zero, 100,
                    Color.Lerp(Color.Cyan, Color.White, Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.4f));
                dust.noGravity = true;
            }

            //追踪效果
            if (Projectile.ai[0] == 0) {
                CalamityUtils.HomeInOnNPC(Projectile, true, 600f, 12f, 20f);
            }

            //发光
            Lighting.AddLight(Projectile.Center, 0.5f, 0.8f, 1f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中特效
            SoundEngine.PlaySound(SoundID.Item94 with {
                Volume = 0.4f,
                Pitch = 0.5f
            }, Projectile.Center);

            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    Vector2 velocity = Main.rand.NextVector2Circular(5f, 5f);
                    Dust dust = Dust.NewDustPerfect(target.Center, DustID.Electric, velocity, 100,
                        Color.Cyan, Main.rand.NextFloat(1f, 1.5f));
                    dust.noGravity = true;
                }
            }

            //穿透减伤
            Projectile.damage = (int)(Projectile.damage * 0.85f);
        }

        public override void OnKill(int timeLeft) {
            //消失特效
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 12; i++) {
                    Vector2 velocity = Projectile.velocity.RotatedByRandom(0.5f) * Main.rand.NextFloat(0.3f, 0.8f);
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.BlueTorch, velocity, 100,
                        Color.Lerp(Color.Cyan, Color.White, Main.rand.NextFloat()),
                        Main.rand.NextFloat(1f, 1.8f));
                    dust.noGravity = true;
                }
            }
        }

        public override Color? GetAlpha(Color lightColor) {
            return Color.Lerp(Color.Cyan, Color.White, (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.5f + 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            //绘制光束效果
            DrawBeamTrail();
            return false;
        }

        private void DrawBeamTrail() {
            Texture2D glowTexture = VaultAsset.placeholder2.Value;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color drawColor = Color.Lerp(Color.DeepSkyBlue, Color.Cyan, progress) * progress;
                drawColor.A = 0;

                float scale = beamWidth / glowTexture.Width * progress;

                Main.EntitySpriteDraw(
                    glowTexture,
                    drawPos,
                    null,
                    drawColor,
                    Projectile.rotation,
                    glowTexture.Size() / 2,
                    new Vector2(2f, scale),
                    SpriteEffects.None,
                    0
                );
            }

            //绘制核心亮点
            Vector2 corePos = Projectile.Center - Main.screenPosition;
            Color coreColor = Color.White;
            coreColor.A = 0;

            Main.EntitySpriteDraw(
                glowTexture,
                corePos,
                null,
                coreColor * 0.8f,
                Projectile.rotation,
                glowTexture.Size() / 2,
                new Vector2(1.5f, beamWidth / glowTexture.Width * 0.6f),
                SpriteEffects.None,
                0
            );
        }
    }
}

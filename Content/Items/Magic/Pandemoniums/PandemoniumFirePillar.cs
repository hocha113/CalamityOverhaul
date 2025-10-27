using CalamityMod.Dusts;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Pandemoniums
{
    /// <summary>
    /// 硫磺火柱弹幕
    /// </summary>
    internal class PandemoniumFirePillar : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private ref float Life => ref Projectile.ai[0];
        private ref float MaxLife => ref Projectile.ai[1];

        private float radius;
        private const int MaxLifeTime = 80;

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> GlowAsset = null;

        public override void SetDefaults() {
            Projectile.width = 240;
            Projectile.height = 240;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 5;
        }

        public override void AI() {
            if (Life == 0) {
                //初始化
                MaxLife = MaxLifeTime;
                radius = Main.rand.NextFloat(180f, 220f);
                Projectile.width = Projectile.height = (int)(radius * 2);

                //生成爆发特效
                SpawnInitialEffect();
            }

            Life++;

            //持续粒子效果
            if (Main.rand.NextBool(2)) {
                SpawnParticles();
            }

            //强烈照明
            float lightIntensity = GetAlphaValue() * 3f;
            Lighting.AddLight(Projectile.Center, 2.5f * lightIntensity, 0.8f * lightIntensity, 0.3f * lightIntensity);
        }

        private float GetAlphaValue() {
            float lifeRatio = Life / MaxLife;
            if (lifeRatio < 0.2f) {
                return lifeRatio / 0.2f;
            }
            else if (lifeRatio > 0.8f) {
                return (1f - lifeRatio) / 0.2f;
            }
            return 1f;
        }

        private void SpawnInitialEffect() {
            //火柱生成爆发
            for (int i = 0; i < 60; i++) {
                Vector2 velocity = new Vector2(
                    Main.rand.NextFloat(-8f, 8f),
                    Main.rand.NextFloat(-20f, -5f)
                );

                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(3f, 5f)
                );
                brimstone.noGravity = true;
                brimstone.fadeIn = 2f;
            }

            //火焰核心
            for (int i = 0; i < 40; i++) {
                Dust fire = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Torch,
                    Main.rand.NextVector2Circular(10f, 10f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(2.5f, 4f)
                );
                fire.noGravity = true;
            }

            //地面冲击环
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 velocity = angle.ToRotationVector2() * 12f;

                Dust ring = Dust.NewDustPerfect(
                    Projectile.Center,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2.5f, 4f)
                );
                ring.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with {
                Volume = 1.2f,
                Pitch = -0.4f
            }, Projectile.Center);
        }

        private void SpawnParticles() {
            float lifeRatio = Life / MaxLife;

            //上升火焰
            for (int i = 0; i < 3; i++) {
                Vector2 spawnPos = Projectile.Center + Main.rand.NextVector2Circular(radius * 0.5f, 10f);
                Dust flame = Dust.NewDustPerfect(
                    spawnPos,
                    (int)CalamityDusts.Brimstone,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-12f, -6f)),
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3.5f) * (1f - lifeRatio * 0.5f)
                );
                flame.noGravity = true;
            }

            //火焰余烬
            if (Main.rand.NextBool(2)) {
                Dust ember = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(radius * 0.7f, radius * 0.7f),
                    DustID.Torch,
                    new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-8f, -3f)),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                ember.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 240);
        }

        public override void OnKill(int timeLeft) {
            //消散特效
            for (int i = 0; i < 30; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);

                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3f)
                );
                brimstone.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!(GlowAsset?.IsLoaded ?? false)) return false;

            SpriteBatch sb = Main.spriteBatch;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            float alpha = GetAlphaValue();

            //火柱基础辉光
            for (int i = 0; i < 4; i++) {
                float scale = (radius / GlowAsset.Value.Width) * (2f + i * 0.2f);
                float layerAlpha = alpha * (0.6f - i * 0.12f);

                sb.Draw(
                    GlowAsset.Value,
                    screenPos,
                    null,
                    new Color(255, 100, 50) with { A = 0 } * layerAlpha,
                    Life * 0.05f,
                    GlowAsset.Value.Size() / 2,
                    scale,
                    SpriteEffects.None,
                    0
                );
            }

            //核心白光
            sb.Draw(
                GlowAsset.Value,
                screenPos,
                null,
                Color.White with { A = 0 } * alpha * 0.5f,
                Life * 0.08f,
                GlowAsset.Value.Size() / 2,
                (radius / GlowAsset.Value.Width) * 1.2f,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}

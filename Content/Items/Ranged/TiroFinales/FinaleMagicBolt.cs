using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.TiroFinales
{
    /// <summary>
    /// 环阵魔弹。幻影燧发枪鸣出的纯魔力弹，无实体弹丸:
    /// 白热梭芯+金辉鞘，头部缀四芒星闪，飞行沿途撒星屑。<br/>
    /// ai0=扰动种子
    /// </summary>
    internal class FinaleMagicBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        private static Asset<Texture2D> StreakTex = null;
        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> GlowTex = null;
        [VaultLoaden(CWRConstant.Masking + "StarTexture_White")]
        private static Asset<Texture2D> StarTex = null;

        private float Seed => Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 6;
            Projectile.friendly = true;
            Projectile.DamageType = RangedMagicDamageClass.Instance;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 320;
            Projectile.extraUpdates = 2;
        }

        public override void AI() {
            if (Projectile.localAI[0] < 1f) {
                Projectile.localAI[0] += 1f / (Projectile.extraUpdates + 1);
                Projectile.position -= Projectile.velocity;
            }
            if (Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = Projectile.velocity.Length();
            }
            float speed = Projectile.velocity.Length();
            if (speed < Projectile.localAI[1] * 1.6f) {
                Projectile.velocity *= 1.006f;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.86f, 0.5f) * 0.32f);

            if (Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(4f, 4f)
                    , Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(0.6f, 0.6f)
                    , new Color(255, 228, 150), Main.rand.NextFloat(0.22f, 0.38f))
                    ?.Configure(true, Main.rand.Next(7, 12));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_Sparkle>(target.Center, Vector2.Zero
                , new Color(255, 244, 200), Main.rand.NextFloat(0.55f, 0.85f))
                ?.Configure(new Color(255, 206, 100), 12, 0.06f, 0.8f);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.UnitX);
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero
                , new Color(255, 220, 140) * 0.5f, 0.08f)?.Configure(Vector2.One, 0f, 0.4f, 10);
            for (int i = 0; i < 3; i++) {
                Vector2 ev = back.RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f)) * Main.rand.NextFloat(1.2f, 3.4f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, ev, new Color(255, 226, 150)
                    , Main.rand.NextFloat(0.26f, 0.44f))?.Configure(true, Main.rand.Next(10, 17));
            }
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.88f, 0.52f) * 0.42f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D streak = StreakTex?.Value;
            Texture2D glow = GlowTex?.Value;
            Texture2D star = StarTex?.Value;
            if (streak == null || glow == null) {
                return false;
            }

            //段带式拖尾：金辉鞘裹白热芯
            Vector2 half = Projectile.Size * 0.5f;
            int len = Projectile.oldPos.Length;
            for (int i = len - 2; i >= 0; i--) {
                Vector2 a = Projectile.oldPos[i + 1];
                Vector2 b = Projectile.oldPos[i];
                if (a == Vector2.Zero || b == Vector2.Zero) {
                    continue;
                }
                Vector2 seg = b - a;
                float segLen = seg.Length();
                if (segLen < 0.5f) {
                    continue;
                }
                float u = i / (float)len;
                Vector2 mid = (a + b) * 0.5f + half - Main.screenPosition;
                float rot = seg.ToRotation() + MathHelper.PiOver2;
                float sy = segLen * 1.3f / (streak.Height * 0.58f);
                float fade = 1f - u;
                Main.EntitySpriteDraw(streak, mid, null, (new Color(214, 148, 52) with { A = 0 }) * (fade * 0.42f)
                    , rot, streak.Size() * 0.5f, new Vector2(0.15f, sy), SpriteEffects.None, 0);
                if (u < 0.6f) {
                    Main.EntitySpriteDraw(streak, mid, null, (new Color(255, 248, 220) with { A = 0 }) * (fade * 0.6f)
                        , rot, streak.Size() * 0.5f, new Vector2(0.055f, sy * 0.85f), SpriteEffects.None, 0);
                }
            }

            //弹体：顺速拉丝梭形+头部四芒星
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float speed = Projectile.velocity.Length();
            float jitter = 0.92f + 0.16f * MathF.Sin((Projectile.timeLeft + Seed) * 2.4f);
            float rotBody = Projectile.rotation + MathHelper.PiOver2;
            var stretch = new Vector2(0.13f, MathHelper.Clamp(speed * 0.07f, 0.5f, 1.4f) * jitter);
            Main.EntitySpriteDraw(streak, drawPos, null, (new Color(255, 206, 104) with { A = 0 }) * 0.85f, rotBody
                , streak.Size() * 0.5f, stretch, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(streak, drawPos, null, (new Color(255, 250, 228) with { A = 0 }) * 0.9f, rotBody
                , streak.Size() * 0.5f, stretch * new Vector2(0.45f, 0.7f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, (new Color(255, 220, 130) with { A = 0 }) * 0.55f, 0f
                , glow.Size() * 0.5f, 0.2f, SpriteEffects.None, 0);
            if (star != null) {
                float twinkle = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + Seed);
                Main.EntitySpriteDraw(star, drawPos, null, (new Color(255, 244, 206) with { A = 0 }) * (0.5f * twinkle)
                    , Seed + Main.GlobalTimeWrappedHourly * 0.8f, star.Size() * 0.5f, 0.05f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}

using CalamityOverhaul.Content.Items.Magic.WheezingWyrms;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.SneezingWyrms
{
    /// <summary>
    /// 龙击弹。灼热的速射弹头，弹身即光源：头亮尾冷，
    /// 拖尾逐节沿 <see cref="Wyrmfire"/> 黑体色带降温；沿途甩烬，命中舔焰点燃。<br/>
    /// ai0=出生温度(0~1)，ai1=扰动种子
    /// </summary>
    internal class WyrmstrikeRound : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        private static Asset<Texture2D> StreakTex = null;
        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> GlowTex = null;

        private float Temp0 => Projectile.ai[0];
        private float Seed => Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 9;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 6;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 540;
            Projectile.extraUpdates = 2;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, Wyrmfire.TempColor(Temp0).ToVector3() * 0.3f);

            //沿途甩烬(extraUpdates 下按几率稀释)
            if (Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_WyrmEmber>(Projectile.Center + Main.rand.NextVector2Circular(3f, 3f)
                    , Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.5f, 0.5f)
                    , default, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(10, 18), Temp0 * 0.8f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //烧满的枪膛打出的弹头点燃地狱烈火
            if (Temp0 >= 0.85f) {
                target.AddBuff(BuffID.OnFire3, 240);
            }
            else {
                target.AddBuff(BuffID.OnFire, 300);
            }

            if (VaultUtils.isServer) {
                return;
            }
            //弹头热量舔上目标
            Vector2 od = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f));
            PRTLoader.NewParticle<PRT_WyrmTongue>(target.Center, od * 1.2f, default, Main.rand.NextFloat(0.5f, 0.8f))
                ?.Configure(od, Main.rand.NextFloat(0.6f, 1f), Main.rand.Next(6, 11), Temp0);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_WyrmEmber>(target.Center, Main.rand.NextVector2Circular(2.5f, 2.5f)
                    , default, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(12, 20), Temp0);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //撞击余韵：溅烬回弹+一缕薄烟，活得比弹头久
            Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 3; i++) {
                Vector2 ev = back.RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(1.2f, 3.2f);
                PRTLoader.NewParticle<PRT_WyrmEmber>(Projectile.Center, ev, default, Main.rand.NextFloat(0.4f, 0.8f))
                    ?.Configure(Main.rand.Next(12, 22), Temp0 * 0.9f);
            }
            if (Temp0 < 0.75f) {
                PRTLoader.NewParticle<PRT_WyrmSmoke>(Projectile.Center, back * 0.6f - Vector2.UnitY * 0.4f
                    , new Color(84, 76, 70) * 0.5f, Main.rand.NextFloat(0.08f, 0.13f))
                    ?.Configure(Main.rand.Next(18, 28), 0.07f);
            }
            Lighting.AddLight(Projectile.Center, Wyrmfire.TempColor(Temp0).ToVector3() * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D streak = StreakTex?.Value;
            Texture2D glow = GlowTex?.Value;
            if (streak == null || glow == null) {
                return false;
            }

            //头亮尾冷：拖尾逐节沿黑体色带降温
            Vector2 half = Projectile.Size * 0.5f;
            int len = Projectile.oldPos.Length;
            for (int i = len - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float u = i / (float)len;
                Color col = Wyrmfire.TempColor(Temp0 - u * 0.5f) with { A = 0 };
                Vector2 pos = Projectile.oldPos[i] + half - Main.screenPosition;
                Main.EntitySpriteDraw(glow, pos, null, col * ((1f - u) * 0.6f), 0f
                    , glow.Size() * 0.5f, 0.16f * (1f - u * 0.55f), SpriteEffects.None, 0);
            }

            //弹体：顺速拉丝的炽热核，逐帧抖动是火的时域签名
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float speed = Projectile.velocity.Length();
            float jitter = 0.9f + 0.2f * MathF.Sin((Projectile.timeLeft + Seed) * 2.7f);
            Color body = Wyrmfire.TempColor(Temp0) with { A = 0 };
            Color core = Wyrmfire.TempColor(Temp0 + 0.3f) with { A = 0 };
            float rot = Projectile.rotation + MathHelper.PiOver2;
            var stretch = new Vector2(0.14f, MathHelper.Clamp(speed * 0.075f, 0.5f, 1.5f) * jitter);
            Main.EntitySpriteDraw(streak, drawPos, null, body * 0.85f, rot
                , streak.Size() * 0.5f, stretch, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(streak, drawPos, null, core * 0.9f, rot
                , streak.Size() * 0.5f, stretch * new Vector2(0.5f, 0.72f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, core * 0.9f, 0f
                , glow.Size() * 0.5f, 0.16f, SpriteEffects.None, 0);
            return false;
        }
    }
}

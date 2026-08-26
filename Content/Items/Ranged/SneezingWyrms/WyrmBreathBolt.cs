using CalamityOverhaul.Content.Items.Magic.WheezingWyrms;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.SneezingWyrms
{
    /// <summary>
    /// 龙息弹。嚏龙铳攒满一口气呵出的活火：蛇行前进、轻微寻敌，
    /// 整条弹道是一串沿 <see cref="Wyrmfire"/> 黑体色带向尾端降温的火舌；
    /// 命中炸开舔焰，穿透数个目标。<br/>
    /// ai0=出生温度(0~1)，ai1=扰动种子
    /// </summary>
    internal class WyrmBreathBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "TearFlame01")]
        private static Asset<Texture2D> FlameTex = null;
        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> GlowTex = null;

        private float Temp0 => Projectile.ai[0];
        private float Seed => Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 300;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            //出生tick驻留：与龙击弹同理，首帧绘制锁在枪口
            if (Projectile.localAI[0] < 1f) {
                Projectile.localAI[0] += 1f / (Projectile.extraUpdates + 1);
                Projectile.position -= Projectile.velocity;
            }

            //蛇行：龙息是条活的火
            Projectile.velocity = Projectile.velocity.RotatedBy(MathF.Sin(Projectile.timeLeft * 0.17f + Seed) * 0.022f);
            //轻微寻敌
            NPC target = Projectile.Center.FindClosestNPC(420f, false, chasedByNPC: npc => npc.CanBeChasedBy(Projectile));
            if (target != null) {
                Projectile.SmoothHomingBehavior(target.Center, 1f, 0.045f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, Wyrmfire.TempColor(Temp0).ToVector3() * 0.5f);

            //沿途甩烬(extraUpdates 下按几率稀释)
            if (Main.rand.NextBool(6)) {
                PRTLoader.NewParticle<PRT_WyrmEmber>(Projectile.Center + Main.rand.NextVector2Circular(5f, 5f)
                    , Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.6f, 0.6f)
                    , default, Main.rand.NextFloat(0.4f, 0.75f))
                    ?.Configure(Main.rand.Next(12, 20), Temp0 * 0.85f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Temp0 >= 0.8f) {
                target.AddBuff(BuffID.OnFire3, 420);
            }
            else {
                target.AddBuff(BuffID.OnFire3, 240);
            }

            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Vector2 od = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f));
                PRTLoader.NewParticle<PRT_WyrmTongue>(target.Center + Main.rand.NextVector2Circular(target.width * 0.3f, target.height * 0.3f)
                    , od * 1.4f, default, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(od, Main.rand.NextFloat(0.8f, 1.3f), Main.rand.Next(8, 14), Temp0);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with { Volume = 0.45f, MaxInstances = 3 }, Projectile.Center);

            PRT_DWave wave = PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, Wyrmfire.TempColor(Temp0), 0.4f);
            wave?.Configure(new Vector2(1f, 1f), 0f, 0.7f, 12);

            //火散成舔焰与烬
            for (int i = 0; i < 5; i++) {
                Vector2 od = (MathHelper.TwoPi / 5f * i + Seed).ToRotationVector2();
                PRTLoader.NewParticle<PRT_WyrmTongue>(Projectile.Center + od * 4f, od * 1.6f, default, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(od, Main.rand.NextFloat(0.8f, 1.3f), Main.rand.Next(7, 12), Temp0);
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_WyrmEmber>(Projectile.Center, Main.rand.NextVector2Circular(2.5f, 2.5f) - Vector2.UnitY * 0.8f
                    , default, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(14, 24), Temp0);
            }
            Lighting.AddLight(Projectile.Center, Wyrmfire.TempColor(Temp0).ToVector3() * 0.9f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D flame = FlameTex?.Value;
            Texture2D glow = GlowTex?.Value;
            if (flame == null || glow == null) {
                return false;
            }

            Vector2 half = Projectile.Size * 0.5f;
            var origin = new Vector2(flame.Width * 0.5f, flame.Height);
            int len = Projectile.oldPos.Length;

            //火蛇身：沿轨迹逐节铺火舌，头亮尾冷、尾端收窄
            for (int i = len - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero || Projectile.oldPos[i - 1] == Vector2.Zero) {
                    continue;
                }
                float u = i / (float)len;
                Vector2 seg = Projectile.oldPos[i - 1] - Projectile.oldPos[i];
                if (seg == Vector2.Zero) {
                    continue;
                }
                //舌尖指向轨迹后方，火贴着弹道向后流
                float segRot = (-seg).ToRotation() + MathHelper.PiOver2;
                float flick = 0.72f + 0.28f * MathF.Sin((Projectile.timeLeft + i * 2.3f + Seed) * 1.9f);
                Color col = Wyrmfire.TempColor(Temp0 - u * 0.55f) with { A = 0 };
                var stretch = new Vector2(0.3f * (1f - u * 0.5f), (0.3f - u * 0.16f) * flick);
                Vector2 segPos = Projectile.oldPos[i] + half - Main.screenPosition;
                Main.EntitySpriteDraw(flame, segPos, null, col * ((1f - u) * 0.55f * flick), segRot
                    , origin, stretch * (92f / flame.Height), SpriteEffects.None, 0);
            }

            //弹头：焰核+白热心
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color body = Wyrmfire.TempColor(Temp0) with { A = 0 };
            Color core = Wyrmfire.TempColor(Temp0 + 0.3f) with { A = 0 };
            float pulse = 0.85f + 0.15f * MathF.Sin(Projectile.timeLeft * 0.9f + Seed);
            float headRot = Projectile.rotation - MathHelper.PiOver2;//舌尖顺速度向前
            float headJit = 0.8f + 0.3f * MathF.Sin((Projectile.timeLeft + Seed) * 2.6f);
            Main.EntitySpriteDraw(flame, drawPos, null, body * (0.9f * pulse), headRot
                , origin, new Vector2(0.34f, 0.3f * headJit) * (92f / flame.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, body * (0.55f * pulse), 0f
                , glow.Size() * 0.5f, 0.42f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, core * (0.95f * pulse), 0f
                , glow.Size() * 0.5f, 0.22f, SpriteEffects.None, 0);
            return false;
        }
    }
}

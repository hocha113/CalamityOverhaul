using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Deerclops
{
    /// <summary>
    /// 白化风暴爆发：以玩家为心的暴雪冲击波，随环扩张滚动命中(每目标一次)，
    /// 命中致盲(混乱)+白盲(重度减速+冻伤)。表现在各端AI首帧自播
    /// </summary>
    internal class WhiteoutBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<WhiteoutStormCore>();

        private const int Life = 26;
        /// <summary>环扩张用时</summary>
        private const int ExpandTime = 18;

        private int Elapsed => Life - Projectile.timeLeft;

        /// <summary>当前波前半径(易出缓动)</summary>
        private float RingRadius {
            get {
                float t = MathHelper.Clamp(Elapsed / (float)ExpandTime, 0f, 1f);
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                return WhiteoutStormCore.BurstRadius * ease;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Life;
            Projectile.DamageType = DamageClass.Generic;
            //每目标只被波前撞一次
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (owner.active) {
                Projectile.Center = owner.Center;
            }

            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //触发拍：各端AI首帧自播，音效随距离自然衰减，震屏带距离衰减
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DeerclopsScream with { Volume = 0.7f, Pitch = 0.45f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.DeerclopsIceAttack with { Volume = 1f, Pitch = -0.15f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.BlizzardStrongLoop with { Volume = 0.5f, Pitch = 0.2f }, Projectile.Center);
                    DeerclopsMotion.CameraPunch(Projectile.Center, 7f, 18, "BRelicWhiteoutBurst");
                    SpawnBurstParticles();
                }
            }

            //判定窗与波前扩张同窗
            Projectile.friendly = Elapsed <= ExpandTime + 2;
        }

        /// <summary>盘形判定：波前扫过即命中(局部免疫保证每目标一次)</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 closest = new Vector2(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.Distance(Projectile.Center, closest) <= RingRadius;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //击退方向沿爆心向外
            modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //致盲=仇恨混乱(3s)，Boss级不吃混乱(折扣规则)；白盲减速在GlobalNPC里分档
            if (!WhiteoutStormGlobalNPC.IsBossLike(target)) {
                target.AddBuff(BuffID.Confused, WhiteoutStormCore.ConfuseTicks);
            }
            target.AddBuff(ModContent.BuffType<WhiteblindDebuff>(), WhiteoutStormCore.StormTicks);

            if (!VaultUtils.isServer) {
                //命中处碎晶(所有者端反馈；旁观者靠伤害数字+目标身上的蒙雪层)
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_DefCrystalShard>(target.Center,
                        Main.rand.NextVector2Circular(3f, 3f) - Vector2.UnitY * 2f,
                        DeerclopsMotion.IceBlue * 0.9f, Main.rand.NextFloat(0.35f, 0.6f))
                        .Configure(Main.rand.Next(16, 26), Main.rand.NextFloat(-0.2f, 0.2f));
                }
            }
        }

        /// <summary>爆发粒子群：径向碎晶+环绕寒雾+外抛雪尘+晶闪</summary>
        private void SpawnBurstParticles() {
            Vector2 center = Projectile.Center;
            for (int i = 0; i < 26; i++) {
                float angle = MathHelper.TwoPi * i / 26f + Main.rand.NextFloat(-0.1f, 0.1f);
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 14f);
                PRTLoader.NewParticle<PRT_DefCrystalShard>(center, vel,
                    Color.Lerp(DeerclopsMotion.IceBlue, DeerclopsMotion.ColdWhite, Main.rand.NextFloat()) * 0.95f,
                    Main.rand.NextFloat(0.4f, 0.75f))
                    .Configure(Main.rand.Next(20, 34), Main.rand.NextFloat(-0.25f, 0.25f), 0.12f);
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_DefCryoMist>(center, Vector2.Zero,
                    DeerclopsMotion.ColdWhite * 0.5f, Main.rand.NextFloat(0.9f, 1.5f))
                    .Configure(Main.rand.Next(26, 40), center, Main.rand.NextFloat(40f, 95f));
            }
            for (int i = 0; i < 22; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 11f);
                Dust dust = Dust.NewDustPerfect(center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.Snow, vel, 90, default, Main.rand.NextFloat(1.1f, 2f));
                dust.noGravity = Main.rand.NextBool();
            }
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_DefFrostGlint>(center + Main.rand.NextVector2Circular(90f, 90f),
                    Main.rand.NextVector2Circular(1.5f, 1.5f),
                    DeerclopsMotion.ColdWhite, Main.rand.NextFloat(2.2f, 4f))
                    .Configure(Main.rand.Next(18, 30));
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(center + Main.rand.NextVector2Circular(36f, 30f),
                    Main.rand.NextVector2Circular(2f, 1.4f) - Vector2.UnitY * 0.8f,
                    DeerclopsMotion.ColdWhite * 0.45f, Main.rand.NextFloat(1f, 1.5f))
                    .Configure(Main.rand.Next(30, 50), 0.55f, Main.rand.NextFloat(-0.04f, 0.04f));
            }
        }

        #region 绘制：共享参数化冲击环(波前)+回响环+爆心冷闪
        public override bool PreDraw(ref Color lightColor) {
            float t = MathHelper.Clamp(Elapsed / (float)ExpandTime, 0f, 1f);
            float fade = MathHelper.Clamp((Life - Elapsed) / 8f, 0f, 1f);
            float radius = RingRadius;
            if (radius < 4f) {
                return false;
            }

            //主波前环
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, radius, 24f + 20f * t,
                DeerclopsMotion.ColdWhite, DeerclopsMotion.IceBlue, DeerclopsMotion.DeepIce,
                alpha: fade * (1f - t * 0.45f), tearPx: -1f, squish: 1f,
                innerGlow: 0.3f * (1f - t), timeSeed: Projectile.identity * 0.37f);

            //回响环(慢半拍，读作雪压回涌)
            if (t > 0.2f) {
                float echoR = radius * 0.55f;
                ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, echoR, 14f,
                    DeerclopsMotion.IceBlue, DeerclopsMotion.DeepIce, DeerclopsMotion.ShadowViolet,
                    alpha: 0.5f * fade * (1f - t * 0.5f), tearPx: -1f, squish: 1f,
                    innerGlow: 0f, timeSeed: Projectile.identity * 0.61f);
            }

            //爆心冷闪(Extra_98真alpha，A=0加色画法，前8帧)
            float flash = MathHelper.Clamp(1f - Elapsed / 8f, 0f, 1f);
            if (flash > 0f) {
                Texture2D glow = CWRAsset.Extra_98.Value;
                Vector2 drawPos = Projectile.Center - Main.screenPosition;
                Color flashColor = DeerclopsMotion.ColdWhite with { A = 0 } * (0.85f * flash);
                Main.EntitySpriteDraw(glow, drawPos, null, flashColor, 0f, glow.Size() / 2f,
                    new Vector2(4.2f, 4.2f) * (0.6f + 0.4f * t), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, drawPos, null, DeerclopsMotion.IceBlue with { A = 0 } * (0.6f * flash),
                    MathHelper.PiOver2, glow.Size() / 2f, new Vector2(6f, 2.4f), SpriteEffects.None, 0);
            }
            return false;
        }
        #endregion
    }
}

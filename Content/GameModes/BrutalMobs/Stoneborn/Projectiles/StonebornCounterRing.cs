using CalamityOverhaul.Content.Items.Stones;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Stoneborn.Projectiles
{
    /// <summary>
    /// 花岗岩魔像·入壳反震：ai[0]=锚NPC索引 ai[1]=锚NPC类型 ai[2]=脉冲半径。
    /// 预告段 <see cref="OmenFrames"/> 帧画出目标半径环（omen，无判定）→
    /// 脉冲段环带由体表扫至目标半径，判定=扫过的可见环带（伤害窗=可见窗），伤害低、击退为主。
    /// 预告段锚体死亡即取消（击杀=有效反制）；脉冲一旦开始即为已提交，不再取消
    /// </summary>
    internal class StonebornCounterRing : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>预告帧（任务单明定 24 帧：近身体术反制脉冲，≥24 姿态契约口径）</summary>
        internal const int OmenFrames = 24;
        /// <summary>脉冲扫掠帧（判定窗）</summary>
        private const int PulseFrames = 10;
        /// <summary>收尾淡出帧（无判定）</summary>
        private const int FadeFrames = 6;
        /// <summary>脉冲环带半厚（判定与绘制同读）</summary>
        private const float PulseBandHalf = 22f;
        /// <summary>脉冲起始半径（体表）</summary>
        private const float PulseStartRadius = 26f;
        /// <summary>预告环节点数（纯绘制密度）</summary>
        private const int RingDots = 12;

        private static readonly Color ShellDark = new Color(20, 26, 46);

        private int AnchorIndex => (int)Projectile.ai[0];
        private int AnchorType => (int)Projectile.ai[1];
        private float TargetRadius => Projectile.ai[2];
        private int Elapsed => OmenFrames + PulseFrames + FadeFrames - Projectile.timeLeft;
        private bool InPulse => Elapsed >= OmenFrames && Elapsed < OmenFrames + PulseFrames;

        /// <summary>脉冲当前半径（各端由同步时序确定性推得）</summary>
        private float CurrentRadius {
            get {
                float t = MathHelper.Clamp((Elapsed - OmenFrames) / (float)PulseFrames, 0f, 1f);
                //缓出：前段扫得快，读作爆发
                return MathHelper.Lerp(PulseStartRadius, TargetRadius, 1f - (1f - t) * (1f - t));
            }
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = OmenFrames + PulseFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        /// <summary>只有脉冲扫掠段有判定（伤害窗=可见窗）</summary>
        public override bool? CanDamage() => InPulse ? null : false;

        /// <summary>环带判定：目标中心到环心的距离落在当前扫掠环带内才命中</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!InPulse) {
                return false;
            }
            float dist = Vector2.Distance(targetHitbox.Center.ToVector2(), Projectile.Center);
            float slack = Math.Min(targetHitbox.Width, targetHitbox.Height) * 0.25f;
            return Math.Abs(dist - CurrentRadius) < PulseBandHalf + slack;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;
            //预告段跟随锚体并校验来源；锚体死亡=反震取消。脉冲段已提交，不再回收
            if (elapsed < OmenFrames) {
                if (!AnchorIndex.TryGetNPC(out NPC anchor) || !anchor.Alives() || anchor.type != AnchorType) {
                    Projectile.Kill();
                    return;
                }
                Projectile.Center = anchor.Center;
            }

            if (!Main.dedServ) {
                if (elapsed == 0) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.4f, Pitch = -0.15f, MaxInstances = 4 }, Projectile.Center);
                }
                if (elapsed == OmenFrames) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, Pitch = 0.15f, MaxInstances = 4 }, Projectile.Center);
                }
                //预告段：环位向内收的警示尘（≤2 粒/帧）
                if (elapsed < OmenFrames && Main.rand.NextBool(2)) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 from = Projectile.Center + ang.ToRotationVector2() * TargetRadius;
                    Dust dust = Dust.NewDustPerfect(from, DustID.Electric,
                        -ang.ToRotationVector2() * Main.rand.NextFloat(0.8f, 1.8f), 90, default, 0.8f);
                    dust.noGravity = true;
                }
                //脉冲段：环带上的电石飞屑
                if (InPulse) {
                    for (int i = 0; i < 2; i++) {
                        float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                        Dust dust = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * CurrentRadius,
                            Main.rand.NextBool() ? DustID.Electric : DustID.Stone,
                            ang.ToRotationVector2() * Main.rand.NextFloat(1.5f, 3f), 70, default, 1f);
                        dust.noGravity = true;
                    }
                }
                Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3() * 0.2f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            Texture2D shell = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Color coreBlue = GraniteMarbleVFX.GraniteCore with { A = 0 };

            if (elapsed < OmenFrames) {
                //预告环：目标半径上的点阵渐亮（omen 语义：这一圈即将被扫过）
                float fadeIn = MathHelper.Clamp(elapsed / 8f, 0f, 1f);
                float urgency = elapsed / (float)OmenFrames;
                float pulse = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.identity);
                for (int i = 0; i < RingDots; i++) {
                    float ang = MathHelper.TwoPi * i / RingDots + Main.GlobalTimeWrappedHourly * 0.8f;
                    Vector2 dot = center + ang.ToRotationVector2() * TargetRadius;
                    Main.EntitySpriteDraw(glow, dot, null, coreBlue * (0.45f * fadeIn * pulse * (0.5f + 0.5f * urgency)),
                        0f, glow.Size() / 2f, 0.09f + 0.05f * urgency, SpriteEffects.None, 0);
                }
                return false;
            }

            //脉冲/淡出段：扫掠环带（暗石壳段 + 电蓝芯段，M5 双层配方按段拼环）
            float strength = InPulse ? 1f : MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f);
            float radius = CurrentRadius;
            int segments = 16;
            for (int i = 0; i < segments; i++) {
                float ang = MathHelper.TwoPi * i / segments;
                Vector2 seg = center + ang.ToRotationVector2() * radius;
                Vector2 scale = new Vector2(PulseBandHalf * 2f / shell.Width, PulseBandHalf * 1.2f / shell.Height);
                Main.EntitySpriteDraw(shell, seg, null, ShellDark * (0.8f * strength), ang + MathHelper.PiOver2,
                    shell.Size() / 2f, scale * 1.18f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(shell, seg, null, coreBlue * (0.7f * strength), ang + MathHelper.PiOver2,
                    shell.Size() / 2f, scale * 0.7f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}

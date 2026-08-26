using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles
{
    /// <summary>
    /// 月明湮灭：残血压轴的巨幅横扫死光。宽度近三倍于弧光死光，
    /// 自锁定起始角起以梯形角速度包络扫过 ~470°（起步缓升→恒速→收尾缓落），
    /// 持续约九秒半；扫向与起始角在蓄力期锁死（预告即承诺，出束后绝不追踪）。
    /// 公平声明（契约3）：全程单束、角速度封顶 0.0182 rad/f（700px 半径切向 ≈12.8px/f，
    /// 顺扫向绕行即可跑赢），扫掠余辉纯视觉无判定。
    /// ai[0]=核心 whoAmI，ai[1]=起始角，ai[2]=扫向 ±1
    /// </summary>
    internal class MLordAnnihilationRayProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        //―――― 生命周期 ――――
        internal const int BurstTime = 12;
        internal const int SweepFrames = 520;
        internal const int CollapseTime = 30;
        internal const int TotalLife = BurstTime + SweepFrames + CollapseTime;
        internal const float BeamLength = 4600f;
        /// <summary>巨束满宽（扫描 86 / 弧光 104 的近三倍档，屏占感拉满）</summary>
        internal const float MaxWidth = 300f;
        /// <summary>总扫角 rad（~470°，超过一整圈的压迫感）</summary>
        internal const float TotalSweep = 8.2f;
        /// <summary>角速度梯形包络：加速段 / 减速段帧长</summary>
        private const int RampUp = 80;
        private const int RampDown = 60;
        /// <summary>恒速段角速度（由总扫角反解，封顶可跑赢）</summary>
        internal const float PeakRate = TotalSweep / (SweepFrames - RampUp * 0.5f - RampDown * 0.5f);

        private ref float Timer => ref Projectile.localAI[0];
        private NPC Host => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;
        private float SweepDir => Projectile.ai[2] >= 0f ? 1f : -1f;

        private float beamWidth;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4800;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife + 30;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>梯形角速度包络的累计扫角（起步缓升给玩家起跑余量）</summary>
        internal static float SweepOffsetAt(float sweepFrame) {
            float t = MathHelper.Clamp(sweepFrame, 0f, SweepFrames);
            //加速段积分
            float upEnd = Math.Min(t, RampUp);
            float angle = PeakRate * upEnd * upEnd / (2f * RampUp);
            if (t <= RampUp) {
                return angle;
            }
            angle = PeakRate * RampUp * 0.5f;
            //恒速段
            float holdEnd = Math.Min(t, SweepFrames - RampDown);
            angle += PeakRate * (holdEnd - RampUp);
            if (t <= SweepFrames - RampDown) {
                return angle;
            }
            //减速段积分
            float dt = t - (SweepFrames - RampDown);
            angle += PeakRate * (dt - dt * dt / (2f * RampDown));
            return angle;
        }

        public override void AI() {
            NPC host = Host;

            //宿主消失或状态被抢占（死亡演出等）：快进收束
            bool hostValid = host.Alives() && host.type == NPCID.MoonLordCore
                && MLordFacts.GetCoreState(host) == MLordStateIndex.LunarAnnihilation;
            if (!hostValid && Timer < TotalLife - CollapseTime) {
                Timer = TotalLife - CollapseTime;
            }

            //角度推进：锁定起始角 + 梯形包络累计扫角（绝不追踪）
            float sweepFrame = MathHelper.Clamp(Timer - BurstTime, 0f, SweepFrames);
            Projectile.rotation = Projectile.ai[1] + SweepDir * SweepOffsetAt(sweepFrame);
            if (host.Alives()) {
                Projectile.Center = host.Center + Projectile.rotation.ToRotationVector2() * 34f;
            }

            //宽度包络：陡峭出束→巨幅恒宽（缓慢呼吸）→收束
            float collapseStart = TotalLife - CollapseTime;
            if (Timer < BurstTime) {
                float t = Timer / BurstTime;
                beamWidth = MathHelper.Lerp(4f, MaxWidth, VaultUtils.EaseOutCubic(t));
            }
            else if (Timer >= collapseStart) {
                float t = (Timer - collapseStart) / CollapseTime;
                beamWidth = MathHelper.Lerp(MaxWidth, 0f, VaultUtils.EaseInQuad(t));
            }
            else {
                //90f 周期慢涌 + 高频微息：巨兵在喘
                float surge = 0.94f + 0.06f * (float)Math.Sin((Timer - BurstTime) * MathHelper.TwoPi / 90f);
                beamWidth = MaxWidth * surge;
            }
            beamWidth *= 1f + 0.03f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 24f);

            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }

            Vector2 dir = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 8; i++) {
                Lighting.AddLight(Projectile.Center + dir * (BeamLength / 8f * i),
                    MLordDirector.Phantasmal.ToVector3() * 0.9f);
            }

            if (VaultUtils.isServer || beamWidth < MaxWidth * 0.3f) {
                return;
            }

            //持续低鸣与震屏（巨束存在感的地鸣层）
            if ((int)Timer % 6 == 0) {
                MLordScreenFX.Punch(Projectile.Center, 2.6f, 7, dir);
            }
            if ((int)Timer % 34 == 0) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.4f, Pitch = -0.62f, MaxInstances = 3 }, Projectile.Center);
            }
            //沿束星屑（密度对齐巨束体量）
            for (int i = 0; i < 2; i++) {
                float along = Main.rand.NextFloat();
                Vector2 pos = Projectile.Center + dir * BeamLength * along
                    + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-beamWidth * 0.45f, beamWidth * 0.45f);
                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos,
                    dir.RotatedBy(Main.rand.NextFloat(-0.25f, 0.25f)) * Main.rand.NextFloat(3f, 8f),
                    Color.Lerp(MLordDirector.Phantasmal, MLordDirector.MoonWhite, Main.rand.NextFloat(0.6f)),
                    Main.rand.NextFloat(0.6f, 1.1f))?.Configure(false, Main.rand.Next(14, 24));
            }
            //口部向心聚流：束根始终在进食
            if (Main.rand.NextBool(2)) {
                Vector2 gatherPos = Projectile.Center + Main.rand.NextVector2CircularEdge(150f, 150f);
                PRTLoader.NewParticle<PRT_HeavenfallStar>(gatherPos, (Projectile.Center - gatherPos) * 0.11f,
                    MLordDirector.DeepViolet, Main.rand.NextFloat(0.6f, 1f))?.Configure(false, 14);
            }
        }

        //伤害窗与可见巨束精确对齐（契约2.3）
        public override bool? CanDamage() => Timer > BurstTime && beamWidth > MaxWidth * 0.4f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * BeamLength,
                beamWidth * 0.6f, ref p);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            float opacity = MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f);
            if (opacity <= 0.02f) {
                return;
            }
            //扫掠余辉：紧贴扫向后方的四道衰减残束（纯视觉动态涂抹，读出旋转方向；
            //无判定，不侵占逃生语义——余辉在束已扫过的安全侧）
            float wake = SweepDir * 0.05f;
            for (int k = 4; k >= 1; k--) {
                float ghostAlpha = opacity * (0.3f - 0.062f * k);
                if (ghostAlpha <= 0.01f) {
                    continue;
                }
                MLordRayRender.DrawBeam(Projectile.Center, Projectile.rotation - wake * k,
                    BeamLength * (1f - 0.03f * k), beamWidth * (1f - 0.13f * k),
                    ghostAlpha, (Projectile.whoAmI * 0.311f + k * 0.17f) % 1f);
            }
            //主束 + 内芯束：双层错种子叠出巨束密度
            MLordRayRender.DrawBeam(Projectile.Center, Projectile.rotation, BeamLength, beamWidth,
                opacity, Projectile.whoAmI * 0.311f % 1f);
            MLordRayRender.DrawBeam(Projectile.Center, Projectile.rotation, BeamLength * 0.99f,
                beamWidth * 0.44f, opacity, (Projectile.whoAmI * 0.311f + 0.53f) % 1f);
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float opacity = MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f);
            //巨口辉团：按满宽折算的加大口部
            MLordRayRender.DrawMuzzle(Projectile.Center, beamWidth / 86f, opacity, additiveBatch: true);
        }
    }
}

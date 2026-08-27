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
    /// 且随扫掠推进自 1 倍渐胀至 <see cref="GrowthCap"/> 倍（根部锚定本体近乎恒定，
    /// 束身外扩成喇叭，判定随视觉同步生长），
    /// 扫向与起始角在蓄力期锁死（预告即承诺，出束后绝不追踪）。
    /// 三幕扫掠 + 回放加速，共约十二秒半：
    /// 扫掠时间轴推进速率自 1 倍速匀升至 <see cref="SpeedCap"/> 倍速（爬满恰在正扫收尾，
    /// 刹停与回刮全程按封顶倍速播放）——巨炮越扫越急，压轴不拖沓；
    /// 正扫渐快（角速度自 <see cref="StartRate"/> 一路升到 <see cref="PeakRate"/>，扫过 ~435°，
    /// 起手快步能跟、越到后面越追人）→刹停一拍（减速可见、束定格，读向窗）
    /// →反向回刮一次（自零起速扫回 ~280°，正扫提初速省下的帧全数拨给回刮，
    /// 收割穿到束后方"已扫过所以安全"那侧蹲着的人；
    /// 顺扫向领跑的玩家反而被拉开距离，两种走位各有各的算盘）。
    /// 公平声明（契约3）：全程单束、有效角速度封顶 <see cref="PeakRate"/>×<see cref="SpeedCap"/> rad/f
    /// （700px 半径切向 ≈19.1px/f——后段纯顺扫绕行跑不赢，解法收半径：
    /// 400px 切向需求仅 ≈10.9px/f，贴近本体越近越稳），加速斜坡平缓可预期；
    /// 回旋前有 <see cref="BrakeFrames"/>+<see cref="StallFrames"/> 包络帧减速与定格预告
    /// （封顶倍速下实际约 0.8 秒），反扫另有 <see cref="ReverseRampFrames"/> 帧起速段，
    /// 扫掠余辉纯视觉无判定。
    /// ai[0]=核心 whoAmI，ai[1]=起始角，ai[2]=扫向 ±1
    /// </summary>
    internal class MLordAnnihilationRayProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        //―――― 生命周期 ――――
        internal const int BurstTime = 12;
        /// <summary>正扫段：角速度自 <see cref="StartRate"/> 线性升到 <see cref="PeakRate"/>
        /// （三幕时长 ×1.2 延展档，扫角 ~435°；角速度包络不动，公平封顶不变）</summary>
        internal const int ForwardFrames = 540;
        /// <summary>刹车段：自峰值线性归零——可见的减速本身就是回旋预告</summary>
        internal const int BrakeFrames = 31;
        /// <summary>定格段：束停住不动的一拍（玩家读反扫方向的窗口）</summary>
        internal const int StallFrames = 41;
        /// <summary>反扫段：回刮已扫过的那一侧（承接正扫匀出的帧，回刮扫角 ~280° 足量）</summary>
        internal const int ReverseFrames = 324;
        /// <summary>反扫起速段：角速度自零升到峰值（回刮同样不瞬时到速）</summary>
        internal const int ReverseRampFrames = 108;
        /// <summary>包络总帧：三幕角速度包络的名义时长（真实播放经回放加速压缩）</summary>
        internal const int SweepFrames = ForwardFrames + BrakeFrames + StallFrames + ReverseFrames;
        /// <summary>回放加速封顶倍率：扫掠时间轴推进速率自 1 匀升至此值后恒速（压轴巨炮越扫越急）</summary>
        internal const float SpeedCap = 1.5f;
        /// <summary>加速爬升段真实帧长（梯形逆解：爬满时包络恰走完正扫段）</summary>
        internal const float AccelFrames = ForwardFrames * 2f / (1f + SpeedCap);
        /// <summary>真实扫掠帧长：加速回放走完全部包络帧所需的实际帧数（+1 收尾余量）</summary>
        internal const int RealSweepFrames = (int)(AccelFrames + (SweepFrames - ForwardFrames) / SpeedCap) + 1;
        internal const int CollapseTime = 30;
        internal const int TotalLife = BurstTime + RealSweepFrames + CollapseTime;
        internal const float BeamLength = 4600f;
        /// <summary>巨束基准满宽（扫描 86 / 弧光 104 的近三倍档，屏占感拉满；增幅后再 ×<see cref="GrowthCap"/>）</summary>
        internal const float MaxWidth = 300f;
        /// <summary>束身增幅封顶：宽度随扫掠推进自 1 倍平滑渐胀至此值，收束帧恰好胀满（巨束越扫越盛开）</summary>
        internal const float GrowthCap = 1.5f;
        /// <summary>束根增幅封顶：口部锚在本体上近乎恒定，只随束身微涨——胀大读作喇叭外扩而非整束换粗</summary>
        internal const float RootGrowthCap = 1.08f;
        /// <summary>起手角速度（700px 半径切向 ≈7px/f，快步或收半径即可跟上；峰值与回放封顶不变）</summary>
        internal const float StartRate = 0.010f;
        /// <summary>角速度包络峰值（700px 半径切向 ≈12.8px/f；回放加速后有效封顶再 ×<see cref="SpeedCap"/>，见类注）</summary>
        internal const float PeakRate = 0.0182f;
        /// <summary>刹车起点（扫掠帧计）</summary>
        private const int BrakeStart = ForwardFrames;
        /// <summary>反扫起点（扫掠帧计）</summary>
        private const int ReverseStart = ForwardFrames + BrakeFrames + StallFrames;

        private ref float Timer => ref Projectile.localAI[0];
        private NPC Host => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;
        private float SweepDir => Projectile.ai[2] >= 0f ? 1f : -1f;
        /// <summary>当前真实扫掠帧（0 ~ <see cref="RealSweepFrames"/>；喂包络前先过 <see cref="WarpedSweepFrame"/>）</summary>
        private float SweepFrame => MathHelper.Clamp(Timer - BurstTime, 0f, RealSweepFrames);

        private float beamWidth;
        /// <summary>当前根宽/束身宽（1 → <see cref="RootGrowthCap"/>/<see cref="GrowthCap"/>），喂给 DrawBeam 的喇叭塑形</summary>
        private float rootWidthRatio = 1f;

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

        /// <summary>
        /// 三幕角速度包络的累计扫角（沿扫向为正）：正扫渐快 → 刹停 → 反向回刮。
        /// 逐段解析积分，各端同式推导，不靠逐帧累加（避免丢帧端轨迹分叉）
        /// </summary>
        internal static float SweepOffsetAt(float sweepFrame) {
            float t = MathHelper.Clamp(sweepFrame, 0f, SweepFrames);
            //正扫：角速度线性自 StartRate 升到 PeakRate，积分即梯形面积
            float fwd = Math.Min(t, ForwardFrames);
            float angle = StartRate * fwd + (PeakRate - StartRate) * fwd * fwd / (2f * ForwardFrames);
            if (t <= BrakeStart) {
                return angle;
            }
            //刹车：自峰值线性归零
            float dt = Math.Min(t - BrakeStart, BrakeFrames);
            angle += PeakRate * (dt - dt * dt / (2f * BrakeFrames));
            //定格段不进角
            if (t <= ReverseStart) {
                return angle;
            }
            //反扫：起速段线性升到峰值，其后恒速回刮
            float rt = t - ReverseStart;
            float ramp = Math.Min(rt, ReverseRampFrames);
            angle -= PeakRate * ramp * ramp / (2f * ReverseRampFrames);
            if (rt > ReverseRampFrames) {
                angle -= PeakRate * (rt - ReverseRampFrames);
            }
            return angle;
        }

        /// <summary>
        /// 回放加速的时间轴折算：真实扫掠帧 → 包络帧。
        /// 推进速率自 1 线性升至 <see cref="SpeedCap"/>（爬满恰在正扫收尾）后恒速；
        /// 解析积分，各端同式推导（与 <see cref="SweepOffsetAt"/> 同一确定性口径）
        /// </summary>
        internal static float WarpedSweepFrame(float realFrame) {
            float t = MathHelper.Clamp(realFrame, 0f, RealSweepFrames);
            if (t <= AccelFrames) {
                return t + (SpeedCap - 1f) * t * t / (2f * AccelFrames);
            }
            return AccelFrames * (1f + SpeedCap) * 0.5f + SpeedCap * (t - AccelFrames);
        }

        /// <summary>当前回放倍率（1 → <see cref="SpeedCap"/>）：余辉铺距与本体反扭随提速加剧，加速要被看见</summary>
        internal static float PlaybackRateAt(float realFrame) {
            float t = MathHelper.Clamp(realFrame, 0f, RealSweepFrames);
            return t >= AccelFrames ? SpeedCap : 1f + (SpeedCap - 1f) * t / AccelFrames;
        }

        /// <summary>
        /// 当前扫掠符号：+1 顺扫 / 0 刹停 / -1 反扫。
        /// 本体反扭倾斜、余辉铺向与回旋预告共用（表现必须跟着真实转向走）
        /// </summary>
        internal static float SweepSignAt(float sweepFrame) {
            if (sweepFrame < BrakeStart) {
                return 1f;
            }
            if (sweepFrame >= ReverseStart) {
                return -1f;
            }
            //刹车段仍在前进但已在减速，定格段完全停住
            return sweepFrame < BrakeStart + BrakeFrames ? 1f : 0f;
        }

        /// <summary>回旋预告强度 0~1：自刹车起爬升，定格段满值（反扫起速后归零）</summary>
        internal static float ReverseCueAt(float sweepFrame) {
            if (sweepFrame < BrakeStart || sweepFrame >= ReverseStart) {
                return 0f;
            }
            return MathHelper.Clamp((sweepFrame - BrakeStart) / BrakeFrames, 0f, 1f);
        }

        public override void AI() {
            NPC host = Host;

            //宿主消失或状态被抢占（死亡演出等）：快进收束
            bool hostValid = host.Alives() && host.type == NPCID.MoonLordCore
                && MLordFacts.GetCoreState(host) == MLordStateIndex.LunarAnnihilation;
            if (!hostValid && Timer < TotalLife - CollapseTime) {
                Timer = TotalLife - CollapseTime;
            }

            //角度推进：锁定起始角 + 回放加速折算 + 三幕包络累计扫角（绝不追踪）
            float sweepFrame = SweepFrame;
            float envFrame = WarpedSweepFrame(sweepFrame);
            Projectile.rotation = Projectile.ai[1] + SweepDir * SweepOffsetAt(envFrame);
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

            //增幅：束身随扫掠自 1 倍渐胀至 GrowthCap、束根只涨到 RootGrowthCap——巨束越扫越大而口部锚定本体；
            //判定走 beamWidth 同步生长，且 0.6 系数下恒窄于根部可见宽（0.90 倍基准 < 1.08 倍根宽），契约2.3 对齐保持
            float growT = MathHelper.Clamp(SweepFrame / RealSweepFrames, 0f, 1f);
            float growth = MathHelper.SmoothStep(1f, GrowthCap, growT);
            beamWidth *= growth;
            rootWidthRatio = MathHelper.SmoothStep(1f, RootGrowthCap, growT) / growth;

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
            //回旋两记定音：刹车起（"它在减速"）与反扫起（"它回来了"）——
            //加速回放下包络帧一帧可跨 >1，用跨越判定不用整点相等
            float envPrev = WarpedSweepFrame(sweepFrame - 1f);
            if (envPrev < BrakeStart && envFrame >= BrakeStart) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = -0.7f }, Projectile.Center);
            }
            else if (envPrev < ReverseStart && envFrame >= ReverseStart) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f, Pitch = -0.55f }, Projectile.Center);
                MLordScreenFX.Punch(Projectile.Center, 8f, 14, dir);
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
            //扫掠余辉：紧贴当前行进方向后方的四道衰减残束（纯视觉动态涂抹，读出旋转方向；
            //无判定，不侵占逃生语义——余辉在束已扫过的安全侧）。
            //反扫段随之翻面，否则余辉会铺到束正要去的危险侧，把逃生语义读反；
            //铺距随回放倍率加宽，提速被看见
            float wake = SweepDir * SweepSignAt(WarpedSweepFrame(SweepFrame)) * 0.05f * PlaybackRateAt(SweepFrame);
            for (int k = 4; k >= 1; k--) {
                float ghostAlpha = opacity * (0.3f - 0.062f * k);
                if (ghostAlpha <= 0.01f) {
                    continue;
                }
                MLordRayRender.DrawBeam(Projectile.Center, Projectile.rotation - wake * k,
                    BeamLength * (1f - 0.03f * k), beamWidth * (1f - 0.13f * k),
                    ghostAlpha, (Projectile.whoAmI * 0.311f + k * 0.17f) % 1f, rootWidthRatio);
            }
            //主束 + 内芯束：双层错种子叠出巨束密度（增幅期同吃根宽比，喇叭形层层一致）
            MLordRayRender.DrawBeam(Projectile.Center, Projectile.rotation, BeamLength, beamWidth,
                opacity, Projectile.whoAmI * 0.311f % 1f, rootWidthRatio);
            MLordRayRender.DrawBeam(Projectile.Center, Projectile.rotation, BeamLength * 0.99f,
                beamWidth * 0.44f, opacity, (Projectile.whoAmI * 0.311f + 0.53f) % 1f, rootWidthRatio);
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float opacity = MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f);
            //回旋预告：刹车与定格期向反扫侧铺三段渐弱引导线，明说下一步往哪边刮（契约2）
            float cue = ReverseCueAt(WarpedSweepFrame(SweepFrame));
            if (cue > 0.01f) {
                float back = -SweepDir;
                for (int i = 1; i <= 3; i++) {
                    MLordRayRender.DrawGuideLine(Projectile.Center, Projectile.rotation + back * 0.16f * i,
                        BeamLength * 0.62f, cue * (0.5f - i * 0.12f), additiveBatch: true);
                }
            }
            //巨口辉团：按根宽折算——束身增幅时口部随根不随身，锚在本体嘴上不跟涨
            MLordRayRender.DrawMuzzle(Projectile.Center, beamWidth * rootWidthRatio / 86f, opacity, additiveBatch: true);
        }
    }
}

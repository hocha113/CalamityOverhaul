using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams
{
    /// <summary>
    /// 鬼梦相位的逐帧推进：包络全部是 PhaseTimer 的确定性函数，远端从快照 timer 自算同一形状。
    /// 状态切换（落定/中断）走 <see cref="KikasaDomainPlayer"/> 的内部结算方法，本类不直接改相位
    /// </summary>
    internal static class KikasaDreamDirector
    {
        //==================== 拉入 ====================

        //节拍：凶兆沸腾 0-64 → 窥犬驻留 64-120 → 倒转 120-210 → 落定 210-244
        //结算 165=倒转段时间过半，163-183 的近全红硬闪盖住世界切换

        public static void UpdatePull(KikasaDomainPlayer domain) {
            domain.SpreadProgress = 1f;
            domain.RiseT = 1f;
            int t = domain.PhaseTimer;
            float prevRoll = domain.DreamRollAngle;

            //沸腾：64f 拉满，比异化翻转的 34f 更沉，整面湖从底往上滚；结算后随红闪退场
            float boilIn = Smooth01(t / 64f);
            float boilOut = t < KikasaDream.PullCommitFrame ? 1f
                : 1f - Smooth01((t - KikasaDream.PullCommitFrame) / 40f);
            domain.DreamBoil = boilIn * boilOut;

            //镜面预览向梦侧调色："猛地变暗"，沸腾期陡坡压到 0.72，驻留段再浸到 0.92
            domain.DreamMix = t <= KikasaDream.PullBoilEnd
                ? Smooth01(t / 64f) * 0.72f
                : MathHelper.Lerp(0.72f, 0.92f, Smooth01(
                    (t - KikasaDream.PullBoilEnd)
                    / (float)(KikasaDream.PullDwellEnd - KikasaDream.PullBoilEnd)));

            //窥犬凝视：驻留段里双目自暗处亮起，倒转期仍燃着，直到结算被红闪吞掉
            domain.DreamGaze = t <= KikasaDream.PullBoilEnd ? 0f
                : t < KikasaDream.PullCommitFrame
                    ? Smooth01((t - KikasaDream.PullBoilEnd) / 36f)
                    : 1f - Smooth01((t - KikasaDream.PullCommitFrame) / 26f);

            //倒转角：反向蓄势一小口，再 0→π 先慢后快再慢
            domain.DreamRollAngle = RollAngle(t, KikasaDream.PullDwellEnd, KikasaDream.PullRollEnd);
            domain.DreamRollVelocity = domain.DreamRollAngle - prevRoll;

            //结算后镜面向上吞满全屏；调色让位给已切换的梦境氛围
            domain.DreamSwallow = t < KikasaDream.PullCommitFrame ? 0f
                : Smooth01((t - KikasaDream.PullCommitFrame) / 37f);
            domain.DreamGrade = t < KikasaDream.PullCommitFrame ? 1f
                : 1f - Smooth01((t - KikasaDream.PullCommitFrame) / 41f);

            domain.DreamFlash = FlashEnvelope(t, KikasaDream.PullCommitFrame);
            domain.DreamSeamGlow = t <= KikasaDream.PullRollEnd ? 1f
                : 1f - Smooth01((t - KikasaDream.PullRollEnd)
                    / (float)(KikasaDream.PullTotalFrames - KikasaDream.PullRollEnd));

            //异样脉冲：驻留中段镜里错位一晃，涟漪从人脚下荡开
            const int glimpseStart = 92;
            const int glimpseFrames = 20;
            domain.DreamGlimpse = t >= glimpseStart && t < glimpseStart + glimpseFrames
                ? MathF.Sin(MathHelper.Pi * (t - glimpseStart) / glimpseFrames) : 0f;
            domain.DreamGlimpseRing = t >= glimpseStart && t < glimpseStart + glimpseFrames + 14
                ? MathHelper.Clamp((t - glimpseStart) / (float)(glimpseFrames + 14), 0f, 1f) : 0f;

            SpawnPullFx(domain);
            PlayPullBeats(domain);

            //中途死亡：梦拽不住死人，直接落定回血湖
            if (domain.Player.dead) {
                domain.DreamAbort();
                return;
            }
            if (t >= KikasaDream.PullTotalFrames) {
                domain.DreamSettleToDreaming();
            }
        }

        //==================== 梦中 ====================

        public static void UpdateDreaming(KikasaDomainPlayer domain) {
            domain.SpreadProgress = 1f;
            //湖不在梦里：水位归零，鬼奴/湖藏/湖面物理全数让位
            domain.RiseT = 0f;

            if (domain.Player.dead) {
                domain.DreamAbort();
            }
        }

        //==================== 归返 ====================

        public static void UpdateReturn(KikasaDomainPlayer domain) {
            domain.SpreadProgress = 1f;
            int t = domain.PhaseTimer;
            float prevRoll = domain.DreamRollAngle;

            //湖水自屏底涌回：物理水位真实回涨，涌满后湖面重新托人
            domain.RiseT = Smooth01(t / (float)KikasaDream.ReturnSurgeEnd);

            //短沸：水涌到位前后翻起，结算后退场
            float boilIn = Smooth01((t - 24) / 40f);
            float boilOut = t < KikasaDream.ReturnCommitFrame ? 1f
                : 1f - Smooth01((t - KikasaDream.ReturnCommitFrame) / 36f);
            domain.DreamBoil = boilIn * boilOut;

            //镜面预览向真实侧靠拢；归途没有凝视，犬已随行
            domain.DreamMix = t <= KikasaDream.ReturnDwellEnd
                ? Smooth01(t / 70f) * 0.8f
                : MathHelper.Lerp(0.8f, 0.92f, Smooth01(
                    (t - KikasaDream.ReturnDwellEnd)
                    / (float)(KikasaDream.ReturnRollEnd - KikasaDream.ReturnDwellEnd)));
            domain.DreamGaze = 0f;

            domain.DreamRollAngle = RollAngle(t, KikasaDream.ReturnDwellEnd, KikasaDream.ReturnRollEnd);
            domain.DreamRollVelocity = domain.DreamRollAngle - prevRoll;

            domain.DreamSwallow = t < KikasaDream.ReturnCommitFrame ? 0f
                : Smooth01((t - KikasaDream.ReturnCommitFrame) / 37f);
            domain.DreamGrade = t < KikasaDream.ReturnCommitFrame ? 1f
                : 1f - Smooth01((t - KikasaDream.ReturnCommitFrame) / 41f);

            domain.DreamFlash = FlashEnvelope(t, KikasaDream.ReturnCommitFrame);
            domain.DreamSeamGlow = t <= KikasaDream.ReturnRollEnd ? 1f
                : 1f - Smooth01((t - KikasaDream.ReturnRollEnd)
                    / (float)(KikasaDream.ReturnTotalFrames - KikasaDream.ReturnRollEnd));

            SpawnReturnFx(domain);
            PlayReturnBeats(domain);

            if (domain.Player.dead) {
                domain.DreamAbort();
                return;
            }
            if (t >= KikasaDream.ReturnTotalFrames) {
                domain.DreamSettleToOpen();
            }
        }

        //==================== 相位粒子与节拍 ====================

        /// <summary>拉入期表现：沸腾气泡蒸汽沿水线翻滚，色向梦侧先行</summary>
        private static void SpawnPullFx(KikasaDomainPlayer domain) {
            if (!IsLocalVisual(domain)) {
                return;
            }
            int t = domain.PhaseTimer;
            //气泡颜色向梦境红黑先行渐变
            float dreamMix = domain.DreamMix;
            if (t < KikasaDream.PullCommitFrame && domain.DreamBoil > 0.05f && t % 2 == 0) {
                KikasaDomainDeco.BoilBurst(domain, domain.DreamBoil, dreamMix);
            }
            if (t < KikasaDream.PullCommitFrame && domain.DreamBoil > 0.3f && t % 6 == 0) {
                KikasaDomainDeco.BoilSteam(domain, domain.DreamBoil, dreamMix);
            }
        }

        /// <summary>归返期表现：涌水与短沸</summary>
        private static void SpawnReturnFx(KikasaDomainPlayer domain) {
            if (!IsLocalVisual(domain)) {
                return;
            }
            int t = domain.PhaseTimer;
            if (t < KikasaDream.ReturnCommitFrame && domain.DreamBoil > 0.1f && t % 3 == 0) {
                KikasaDomainDeco.BoilBurst(domain, domain.DreamBoil, 1f - domain.DreamMix);
            }
            //落定确认拍：世界"落"回湖面
            if (t == KikasaDream.ReturnRollEnd) {
                Vector2 lakeAt = new(domain.Player.Center.X, domain.LakeWorldY);
                KikasaDomainDeco.SplashAt(lakeAt, 14);
                KikasaDomainDeco.RippleAt(lakeAt, 1.5f);
            }
        }

        /// <summary>拉入节拍音，全部落在观看者本机；具体拍点随演出细化</summary>
        private static void PlayPullBeats(KikasaDomainPlayer domain) {
            if (!IsLocalVisual(domain)) {
                return;
            }
            KikasaDreamFX.PullBeat(domain);
        }

        private static void PlayReturnBeats(KikasaDomainPlayer domain) {
            if (!IsLocalVisual(domain)) {
                return;
            }
            KikasaDreamFX.ReturnBeat(domain);
        }

        //==================== 曲线 ====================

        /// <summary>本机此刻看的是不是这个域，节拍与粒子只在观看端落地</summary>
        private static bool IsLocalVisual(KikasaDomainPlayer domain)
            => !Main.dedServ && ReferenceEquals(KikasaDomain.Viewed, domain);

        /// <summary>倒转角：dwellEnd 前静止，之后反向蓄势 10% 再 0→π</summary>
        private static float RollAngle(int t, int dwellEnd, int rollEnd) {
            if (t <= dwellEnd) {
                return 0f;
            }
            float p = MathHelper.Clamp((t - dwellEnd) / (float)(rollEnd - dwellEnd), 0f, 1f);
            const float antic = 0.10f;
            return p < antic
                ? -0.03f * MathHelper.Pi * Smooth01(p / antic)
                : MathHelper.Lerp(-0.03f * MathHelper.Pi, MathHelper.Pi,
                    CubicInOut((p - antic) / (1f - antic)));
        }

        /// <summary>结算闪：2f 陡起，18f 长尾退潮</summary>
        private static float FlashEnvelope(int t, int commitFrame) {
            if (t >= commitFrame - 2 && t < commitFrame) {
                return (t - (commitFrame - 2)) / 2f;
            }
            if (t >= commitFrame) {
                return MathHelper.Clamp(1f - (t - commitFrame) / 18f, 0f, 1f);
            }
            return 0f;
        }

        private static float Smooth01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }

        private static float CubicInOut(float t) {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;
        }
    }
}

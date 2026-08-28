using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>
    /// 月明湮灭（残血循环压轴，裸露期血量低于 <see cref="MLordDirector.AnnihilationLifeRatio"/>
    /// 后接替死光扫描席；跌破 <see cref="MLordDirector.AnnihilationForceRatio"/> 仍一次未放则强制补放）：
    /// 残口四爪张成 X 形抓桩定身（发射架）→心口聚星蓄力，
    /// 引导线锁死起始角与扫向（预告即承诺）→一拍寂静→喷发巨幅横扫死光，
    /// 三幕扫掠约十二秒半（回放自 1 倍速匀加速至 <see cref="MLordAnnihilationRayProj.SpeedCap"/> 倍速封顶，
    /// 正扫渐快 ~435° → 刹停一拍 → 反向回刮 ~280°，越扫越急）
    /// →收束后长硬直大惩罚窗。
    /// 公平声明（契约3）：清场先行、单束、有效角速度封顶（包络峰值×回放倍率）、
    /// 起始角落后玩家角位 0.7 rad（出束先给起跑余量）、扫向锁定后绝不追踪，
    /// 顺扫向绕行即为解（后段提速，收半径绕行更稳）；
    /// 回旋不是偷袭——减速、定格与反侧引导线三重预告后才回刮
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.LunarAnnihilation, typeof(MLordContext))]
    internal class MLordLunarAnnihilationState : MLordStateBase
    {
        public override string StateName => "LunarAnnihilation";
        public override MLordStateIndex StateIndex => MLordStateIndex.LunarAnnihilation;

        //―――― 时间轴（原始帧，蓄力/收尾吃节奏压缩，巨束本体定长——束内另有回放加速，见弹体）――――
        /// <summary>蓄力段结束（黑臂抓桩与聚星并行）</summary>
        internal const int ChargeEnd = 140;
        /// <summary>引导线亮起并锁定承诺的时刻</summary>
        internal const int AimLockTick = 96;
        /// <summary>寂静一拍帧长（喷发前的黑）</summary>
        internal const int SilenceLen = 16;
        /// <summary>收束后的硬直惩罚窗</summary>
        internal const int StaggerLen = 74;

        private int fireTick;
        private int stateLength;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            fireTick = Frames(context, ChargeEnd) + SilenceLen;
            stateLength = fireTick + MLordAnnihilationRayProj.TotalLife + Frames(context, StaggerLen);
            if (!VaultUtils.isClient) {
                //记一次已放：保底强制线据此判断这场还欠不欠压轴巨束（常规席位不受影响）
                context.Owner.ai[MLordAiSlots.OvUltUsed] =
                    MLordUltFlags.With(context.Owner.ai[MLordAiSlots.OvUltUsed], MLordUltFlags.Annihilation);
                context.Owner.ai[MLordAiSlots.OvEyeCommand] = MLordEyeCommand.Retreat;
                context.Owner.ai[MLordAiSlots.OvAttackSeed] = Main.rand.Next(1, 100000);
                MLordBlackFlashState.ClearHostileStage();
                context.Npc.netUpdate = true;
            }
            if (!VaultUtils.isServer) {
                //开场低吼：湮灭仪式启幕
                SoundEngine.PlaySound(SoundID.Zombie96 with { Volume = 1.15f, Pitch = -0.6f }, context.Npc.Center);
            }
        }

        public override void OnExit(MLordContext context) {
            base.OnExit(context);
            if (!VaultUtils.isClient) {
                context.Owner.ai[MLordAiSlots.OvEyeCommand] = MLordEyeCommand.Solo;
                context.Npc.netUpdate = true;
            }
        }

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;

            //全程抓桩：黑臂四锚是这门炮的发射架
            RequestMove(context, npc.Center, 0.6f, MLordMovePolicy.Brace);
            context.EclipseDrive = 1f;

            if (Timer < fireTick - SilenceLen) {
                UpdateCharge(context);
            }
            else if (Timer < fireTick) {
                UpdateSilence(context);
            }
            else if (Timer == fireTick) {
                FireBeam(context);
            }
            else if (Timer < fireTick + MLordAnnihilationRayProj.TotalLife) {
                UpdateSweep(context);
            }
            else {
                //收束硬直：巨械过热的大惩罚窗
                context.StaggerVulnerable = true;
                context.HeartExposure = 1f;
                npc.velocity *= 0.9f;
                npc.rotation = npc.rotation.AngleLerp(0f, 0.06f);
            }

            Timer++;
            if (Timer >= stateLength) {
                return NextAttack(context);
            }
            return null;
        }

        #region 阶段推进

        /// <summary>蓄力：抓桩进度可见 + 聚星升调；末段锁定承诺（起始角+扫向写同步槽）</summary>
        private void UpdateCharge(MLordContext context) {
            NPC npc = context.Npc;
            int chargeEnd = fireTick - SilenceLen;
            float charge = MathHelper.Clamp(Timer / (float)chargeEnd, 0f, 1f);
            context.SetChargeState(charge);
            context.HeartExposure = 1f;

            //锁定承诺：起始角落后玩家角位 0.7 rad（起跑余量），扫向取玩家切向速度
            int aimLock = (int)(AimLockTick / (float)ChargeEnd * chargeEnd);
            if (!VaultUtils.isClient && Timer == aimLock) {
                Player target = context.Target;
                Vector2 toTarget = target.Center - npc.Center;
                float targetAngle = toTarget.ToRotation();
                //切向速度决定扫向：顺着玩家惯性追压；近乎静止时骰
                Vector2 tangent = toTarget.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                float lateral = Vector2.Dot(target.velocity, tangent);
                float sweepDir = Math.Abs(lateral) > 0.6f ? Math.Sign(lateral)
                    : (MLordConstellationProj.Hash01((int)context.Owner.ai[MLordAiSlots.OvAttackSeed], 7) > 0.5f ? 1f : -1f);
                context.Owner.ai[MLordAiSlots.OvAnchorX] = MathHelper.WrapAngle(targetAngle - sweepDir * 0.7f);
                context.Owner.ai[MLordAiSlots.OvAnchorY] = sweepDir;
                npc.netUpdate = true;
            }

            if (VaultUtils.isServer) {
                return;
            }
            //―――― 客户端表现 ――――
            MLordScreenEffects.PushGravityDim(npc.Center, charge * 0.8f);
            MLordScreenFX.ConvergeStreak(npc.Center, 520f, charge);
            //三拍升调蜂鸣（固定节拍可内化）
            int beat = chargeEnd / 3;
            if (beat > 0 && (Timer == beat || Timer == beat * 2 || Timer == beat * 3 - 4)) {
                int n = Timer / beat;
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.9f, Pitch = -0.45f + n * 0.28f }, npc.Center);
                MLordScreenFX.Punch(npc.Center, 2.5f + n * 1.2f, 8);
            }
        }

        /// <summary>寂静一拍：一切收干，只剩锁死的引导线（爆发前的黑）</summary>
        private void UpdateSilence(MLordContext context) {
            NPC npc = context.Npc;
            context.SetChargeState(1f);
            context.HeartExposure = 1f;
            npc.velocity *= 0.7f;
            if (!VaultUtils.isServer && Timer == fireTick - SilenceLen) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.75f, Pitch = -0.85f }, npc.Center);
            }
        }

        /// <summary>喷发：服务端生成巨束，各端冲击帧</summary>
        private void FireBeam(MLordContext context) {
            NPC npc = context.Npc;
            float startAngle = context.Owner.ai[MLordAiSlots.OvAnchorX];
            Vector2 beamDir = startAngle.ToRotationVector2();

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1.25f, Pitch = -0.5f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.1f, Pitch = -0.7f }, npc.Center);
                SoundEngine.PlaySound(CWRSound.BlackHole with { Volume = 0.9f, Pitch = -0.2f }, npc.Center);
                MLordScreenEffects.PushStarRing(npc.Center, 1.2f, 1100f, 38);
                MLordScreenFX.StarBurst(npc.Center, 2.2f, 30);
                MLordScreenFX.Punch(npc.Center, 13f, 20, beamDir);
            }
            if (VaultUtils.isClient) {
                return;
            }
            int damage = ScaleDamage(context, MLordDirector.AnnihilationRayDamage);
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<MLordAnnihilationRayProj>(), damage, 0f, Main.myPlayer,
                npc.whoAmI, startAngle, context.Owner.ai[MLordAiSlots.OvAnchorY]);
        }

        /// <summary>扫掠段：本体逆当前行进方向微倾（发射架承受反扭），心口白热</summary>
        private void UpdateSweep(MLordContext context) {
            NPC npc = context.Npc;
            context.HeartExposure = 1f;
            context.SetChargeState(1f);
            float sweepDir = context.Owner.ai[MLordAiSlots.OvAnchorY] >= 0f ? 1f : -1f;
            //反扭倾斜：巨束的质量反作用写在身体姿态上，随回放提速加剧。
            //刹停时回正、反扫时倒向另一侧——身体先于束交代这门炮要回刮了
            float sweepFrame = Timer - fireTick - MLordAnnihilationRayProj.BurstTime;
            float sign = MLordAnnihilationRayProj.SweepSignAt(MLordAnnihilationRayProj.WarpedSweepFrame(sweepFrame));
            context.LeanAngle = -sweepDir * sign * 0.055f * MLordAnnihilationRayProj.PlaybackRateAt(sweepFrame);
            npc.rotation = npc.rotation.AngleLerp(context.LeanAngle, 0.08f);
        }

        #endregion

        #region 引导线（客户端绘制经由屏幕效果层的补充：锁定后的承诺可视化）

        /// <summary>
        /// 锁定后到出束前的引导线强度（0=未锁）。
        /// 渲染由 <see cref="MoonLordCoreAI"/> 的 Draw 经 <see cref="DrawAimGuide"/> 调用
        /// </summary>
        internal static void DrawAimGuide(NPC core, MoonLordCoreAI coreAI) {
            if (MLordFacts.GetCoreState(core) != MLordStateIndex.LunarAnnihilation) {
                return;
            }
            int timer = coreAI.StateTimer;
            bool asura = CWRWorld.Asura;
            int chargeEnd = MLordDirector.Frames(ChargeEnd, asura);
            int fire = chargeEnd + SilenceLen;
            int aimLock = (int)(AimLockTick / (float)ChargeEnd * chargeEnd);
            if (timer < aimLock || timer >= fire) {
                return;
            }
            float startAngle = coreAI.ai[MLordAiSlots.OvAnchorX];
            float sweepDir = coreAI.ai[MLordAiSlots.OvAnchorY] >= 0f ? 1f : -1f;
            //锁定渐亮，寂静拍收细定格（承诺不熄灭）
            float strength = MathHelper.Clamp((timer - aimLock) / 18f, 0f, 1f);
            if (timer >= fire - SilenceLen) {
                strength = 0.55f;
            }
            MLordRayRender.DrawGuideLine(core.Center, startAngle, MLordAnnihilationRayProj.BeamLength,
                strength, additiveBatch: false);
            //扫向暗示：起始角前方一小段渐弱虚线（三点渐隐，读出旋转方向）
            for (int i = 1; i <= 3; i++) {
                MLordRayRender.DrawGuideLine(core.Center, startAngle + sweepDir * 0.12f * i,
                    MLordAnnihilationRayProj.BeamLength * 0.5f, strength * (0.42f - i * 0.11f),
                    additiveBatch: false);
            }
        }

        #endregion
    }
}

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
    /// 虚空撕裂（低血大招，一场一次）：光被吸走的蓄势
    /// →锁角拍冻结三叉起始角与扫向，预警线两闪读秒约半秒
    /// →三叉死光沿线出膛，按固定扫角刚性横扫（出束后绝不追踪，预告即承诺）
    /// →星陨/波列崩解收束→长硬直大惩罚窗（受击加伤）。
    /// 旧版限窗追踪在伤害窗内咬住玩家，被判"锁定玩家躲不了"，2026-08-28 废除
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.VoidRupture, typeof(MLordContext))]
    internal class MLordVoidRuptureState : MLordStateBase
    {
        public override string StateName => "VoidRupture";
        public override MLordStateIndex StateIndex => MLordStateIndex.VoidRupture;

        internal const int ChargeEnd = 100;
        /// <summary>
        /// 三叉锁角拍：此帧锁死三束起始角与扫向，预警线第一闪亮起。
        /// 距出束 32 帧（约半秒）双闪读秒；闪线 32 帧 + 弧光自身
        /// <see cref="MLordArcRayProj.ExpandTime"/> 帧静默成束，短于光束类预警预算
        /// <see cref="MLordDirector.BeamTelegraphFrames"/>——补偿是出束后绝不追踪、
        /// 起始角留 <see cref="StartLeadAngle"/> 起跑余量、扫速 InOut 缓起
        /// </summary>
        internal const int AimLockTick = 68;
        /// <summary>第一闪熄灭拍（短暗隙后第二闪，暗隙让"闪烁"成立）</summary>
        internal const int FlashGapTick = AimLockTick + 12;
        /// <summary>第二闪亮起拍：直亮到出束，束就在线上显形（预告即承诺）</summary>
        internal const int FlashTwoTick = AimLockTick + 18;
        /// <summary>
        /// 公平阀（契约3）：起始角自玩家角位后撤此角度（后撤侧取玩家来路），
        /// 出束帧没有任何一束压在玩家身上，扫回锁定点前还有缓起段（起跑余量）
        /// </summary>
        internal const float StartLeadAngle = 0.7f;
        /// <summary>
        /// 固定扫角（公平声明，契约3）：三束刚性 120° 相位同旋，扫角小于扇区间距——
        /// 贴住相邻束起始线后方站位可全程免伤；顺扫向以 ~0.013 rad/f（700px 处约 9px/f）
        /// 匀速绕行也稳赢缓起段给出的领先量，已扫过侧恒安全
        /// </summary>
        internal const float SweepArc = 1.9f;
        internal const int RaysEnd = ChargeEnd + MLordArcRayProj.TotalLife;
        internal const int BurstEnd = RaysEnd + 26;
        internal const int StaggerEnd = BurstEnd + 78;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isClient) {
                context.Owner.ai[MLordAiSlots.OvUltUsed] =
                    MLordUltFlags.With(context.Owner.ai[MLordAiSlots.OvUltUsed], MLordUltFlags.VoidRupture);
                context.Owner.ai[MLordAiSlots.OvEyeCommand] = MLordEyeCommand.Retreat;
                context.Owner.ai[MLordAiSlots.OvAttackSeed] = Main.rand.Next(1, 100000);
                //清自家死光（含真眼链束与月明湮灭巨束），让大招独占舞台
                foreach (Projectile p in Main.ActiveProjectiles) {
                    if (p.type == ModContent.ProjectileType<MLordScanRayProj>()
                        || p.type == ModContent.ProjectileType<MLordArcRayProj>()
                        || p.type == ModContent.ProjectileType<MLordEyeLinkProj>()
                        || p.type == ModContent.ProjectileType<MLordAnnihilationRayProj>()) {
                        p.Kill();
                    }
                }
                context.Npc.netUpdate = true;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie96 with { Volume = 1.2f, Pitch = -0.75f }, context.Npc.Center);
            }
        }

        public override void OnExit(MLordContext context) {
            base.OnExit(context);
            if (!VaultUtils.isClient) {
                context.Owner.ai[MLordAiSlots.OvEyeCommand] = MLordEyeCommand.Solo;
            }
        }

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;

            //大招发射架：黑臂四爪抓桩定身；蓄势期锁伤（玩家该走位而非抢血线），出弧后放开
            RequestMove(context, target.Center + new Vector2(0f, -430f), 0.5f, MLordMovePolicy.Brace);
            UpdateLean(context);
            context.EclipseDrive = 1f;
            npc.dontTakeDamage = Timer < ChargeEnd;

            if (Timer < ChargeEnd) {
                UpdateCharge(context);
            }
            else if (Timer == ChargeEnd) {
                FireTriArc(context);
            }
            else if (Timer > RaysEnd && Timer <= BurstEnd) {
                UpdateCollapseBurst(context);
            }
            else if (Timer > BurstEnd) {
                //硬直惩罚窗：心脏洞开，受击加伤
                context.StaggerVulnerable = true;
                context.HeartExposure = 1f;
                npc.velocity *= 0.9f;
            }

            //弧光期间点缀星陨
            if (!VaultUtils.isClient && (Timer == ChargeEnd + 66 || Timer == ChargeEnd + 128)) {
                SpawnPunctuationComets(context);
            }

            Timer++;
            if (Timer >= StaggerEnd) {
                return NextAttack(context);
            }
            return null;
        }

        /// <summary>蓄势三拍：吸光、聚星、升调蜂鸣；锁角拍写死三叉起始角（引导线据此绘制）</summary>
        private void UpdateCharge(MLordContext context) {
            NPC npc = context.Npc;
            context.SetChargeState(Timer / (float)ChargeEnd);
            context.HoldAllParts = true;

            LockAim(context);

            if (VaultUtils.isServer) {
                return;
            }
            MLordScreenEffects.PushGravityDim(npc.Center, Timer / (float)ChargeEnd * 0.85f);
            MLordScreenFX.ConvergeStreak(npc.Center, 560f, Timer / (float)ChargeEnd);

            //两拍升调蜂鸣铺垫（28f 节拍），其后读秒交给预警线双闪
            if (Timer == 28 || Timer == 56) {
                float pitch = -0.4f + (Timer / 28) * 0.3f;
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.9f, Pitch = pitch }, npc.Center);
                MLordScreenFX.Punch(npc.Center, 3f + Timer / 28f, 8);
            }
            //双闪各配一记短促高音：闪即声，听觉读秒与视觉对齐
            if (Timer == AimLockTick || Timer == FlashTwoTick) {
                float pitch = Timer == AimLockTick ? 0.25f : 0.55f;
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.85f, Pitch = pitch }, npc.Center);
                MLordScreenFX.Punch(npc.Center, 4.5f, 7);
            }
        }

        /// <summary>
        /// 锁角：起始角 = 玩家角位后撤 <see cref="StartLeadAngle"/>（后撤侧取玩家切向惯性的反侧，
        /// 迎着来路展开），扫向取后撤的反侧（束自来路越过锁定点、顺着玩家惯性方向刮）。
        /// 双双写同步槽供预警线与出束共读——线在哪，束就在哪，往哪转也先说清（预告即承诺，契约2.2）
        /// </summary>
        private void LockAim(MLordContext context) {
            if (VaultUtils.isClient || Timer != AimLockTick) {
                return;
            }
            NPC npc = context.Npc;
            Player target = context.Target;
            Vector2 toTarget = target.Center - npc.Center;
            Vector2 tangent = toTarget.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            float lateral = Vector2.Dot(target.velocity, tangent);
            //玩家横移时朝其来路后撤（起跑余量给在他要去的那侧）；近乎静止时骰
            float leadSide = Math.Abs(lateral) > 0.6f ? -Math.Sign(lateral)
                : (MLordConstellationProj.Hash01((int)context.Owner.ai[MLordAiSlots.OvAttackSeed], 11) > 0.5f ? 1f : -1f);
            context.Owner.ai[MLordAiSlots.OvAnchorX] =
                MathHelper.WrapAngle(toTarget.ToRotation() + leadSide * StartLeadAngle);
            context.Owner.ai[MLordAiSlots.OvAnchorY] = -leadSide;
            npc.netUpdate = true;
        }

        /// <summary>
        /// 三辉弧：120° 相位差的三叉死光沿锁定的预警线出膛，各束同值带符号扫角
        /// （<see cref="SweepArc"/>×扫向）刚性同旋——整个三叉阵按固定包络转动，
        /// 任意时刻保证两个完整 120° 逃生扇区，出束后绝不追踪（预告即承诺）
        /// </summary>
        private void FireTriArc(MLordContext context) {
            if (!VaultUtils.isServer) {
                MLordScreenEffects.PushStarRing(context.Npc.Center, 1.1f, 980f, 34);
                MLordScreenFX.Punch(context.Npc.Center, 10f, 18);
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1.2f, Pitch = -0.6f }, context.Npc.Center);
            }
            if (VaultUtils.isClient) {
                return;
            }
            NPC npc = context.Npc;
            int damage = ScaleDamage(context, MLordDirector.UltRayDamage);
            float baseAngle = context.Owner.ai[MLordAiSlots.OvAnchorX];
            float sweepDir = context.Owner.ai[MLordAiSlots.OvAnchorY] >= 0f ? 1f : -1f;
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<MLordArcRayProj>(), damage, 0f, Main.myPlayer,
                    npc.whoAmI, baseAngle + MathHelper.TwoPi / 3f * i, sweepDir * SweepArc);
            }
        }

        /// <summary>
        /// 三叉预警线：锁角拍到出束拍之间双闪读秒（第一闪→短暗隙→第二闪直亮到出束），
        /// 束沿线出、线即承诺；第二闪期沿扫向铺渐弱短线，预告出束后往哪边刮。
        /// 渲染由 <see cref="MoonLordCoreAI"/> 的 Draw 调用，与月明湮灭同式
        /// </summary>
        internal static void DrawAimGuide(NPC core, MoonLordCoreAI coreAI) {
            if (MLordFacts.GetCoreState(core) != MLordStateIndex.VoidRupture) {
                return;
            }
            int timer = coreAI.StateTimer;
            if (timer < AimLockTick || timer >= ChargeEnd) {
                return;
            }
            //双闪包络：两段亮窗之间整线熄灭，闪烁才成立；第二闪满亮托住出束帧
            float strength;
            if (timer < FlashGapTick) {
                strength = MathHelper.Clamp((timer - AimLockTick) / 4f, 0f, 1f) * 0.85f;
            }
            else if (timer < FlashTwoTick) {
                return;
            }
            else {
                strength = MathHelper.Clamp((timer - FlashTwoTick) / 4f, 0f, 1f);
            }
            float baseAngle = coreAI.ai[MLordAiSlots.OvAnchorX];
            float sweepDir = coreAI.ai[MLordAiSlots.OvAnchorY] >= 0f ? 1f : -1f;
            for (int i = 0; i < 3; i++) {
                float angle = baseAngle + MathHelper.TwoPi / 3f * i;
                MLordRayRender.DrawGuideLine(core.Center, angle, MLordArcRayProj.BeamLength, strength);
                //扫向暗示只挂第二闪：读秒末拍同时交代旋转方向（与月明湮灭回旋预告同语法）
                if (timer >= FlashTwoTick) {
                    for (int k = 1; k <= 2; k++) {
                        MLordRayRender.DrawGuideLine(core.Center, angle + sweepDir * 0.1f * k,
                            MLordArcRayProj.BeamLength * 0.5f, strength * (0.38f - k * 0.13f));
                    }
                }
            }
        }

        /// <summary>崩解收束：星环 + 双重旋转缺口波列</summary>
        private void UpdateCollapseBurst(MLordContext context) {
            NPC npc = context.Npc;
            if (Timer == RaysEnd + 4 && !VaultUtils.isServer) {
                MLordScreenEffects.PushStarRing(npc.Center, 1.2f, 1200f, 40);
                MLordScreenFX.StarBurst(npc.Center, 2.2f, 30);
                MLordScreenFX.Punch(npc.Center, 9f, 16);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.1f, Pitch = -0.5f }, npc.Center);
            }
            if (VaultUtils.isClient) {
                return;
            }
            int seed = (int)context.Owner.ai[MLordAiSlots.OvAttackSeed];
            //两圈缺口环：不同起始相位，缺口错开。由退避收拢中的真眼放出
            //（它们正贴核心悬停，环心仍在本体附近）——心脏不喷小弹幕
            if (Timer == RaysEnd + 6 || Timer == RaysEnd + 20) {
                int ring = Timer == RaysEnd + 6 ? 0 : 1;
                NPC emitter = MLordFacts.GetFreeEye(npc, ring);
                if (emitter == null) {
                    return;
                }
                int count = 14;
                int gapAt = (int)(MLordConstellationProj.Hash01(seed, 60 + ring) * count);
                float baseAngle = MLordConstellationProj.Hash01(seed, 70 + ring) * MathHelper.TwoPi;
                int damage = ScaleDamage(context, MLordDirector.BoltDamage);
                for (int i = 0; i < count; i++) {
                    //连缺三位形成可穿越走廊
                    int delta = (i - gapAt + count) % count;
                    if (delta <= 2) {
                        continue;
                    }
                    float angle = baseAngle + MathHelper.TwoPi / count * i;
                    Projectile.NewProjectile(emitter.GetSource_FromAI(), emitter.Center,
                        angle.ToRotationVector2() * (5.2f + ring * 1.6f),
                        ModContent.ProjectileType<MLordBoltProj>(), damage, 0f, Main.myPlayer);
                }
            }
        }

        /// <summary>弧光期间的点缀彗星（直落玩家两侧，无星火）</summary>
        private void SpawnPunctuationComets(MLordContext context) {
            Player target = context.Target;
            int damage = ScaleDamage(context, MLordDirector.CometDamage);
            float groundY = MLordScreenFX.FindGroundBelow(target.Center).Y + 40f;
            for (int i = 0; i < 4; i++) {
                float offsetX = (i - 1.5f) * 260f;
                Vector2 spawn = target.Center + new Vector2(offsetX, -760f);
                Projectile.NewProjectile(context.Npc.GetSource_FromAI(), spawn,
                    new Vector2(0f, 9f), ModContent.ProjectileType<MLordCometProj>(),
                    damage, 0f, Main.myPlayer, 0f, 0f, groundY);
            }
        }
    }
}

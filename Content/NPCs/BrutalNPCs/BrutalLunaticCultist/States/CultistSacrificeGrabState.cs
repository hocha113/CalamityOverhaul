using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 仪式献祭投技：轮盘博弈中带印记再次打错分身→仪式锁阵就地收拢（48帧可逃）→
    /// 锁身吊上祭坛→幻龙掠影两拍→远古光汇聚引爆掷出；
    /// 判定服务端权威，受害者位移/伤害/运镜全在其本机（CultistSacrificePlayer）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.SacrificeGrab, typeof(CultistStateContext))]
    internal class CultistSacrificeGrabState : CultistStateBase
    {
        public override string StateName => "SacrificeGrab";
        public override CultistStateIndex StateIndex => CultistStateIndex.SacrificeGrab;

        #region 时间轴常量（状态/锁阵弹幕/受害者侧/运镜共用）
        /// <summary>锁阵收拢判定帧（telegraph 结束，可逃窗口）</summary>
        internal const int SealCloseEnd = 48;
        /// <summary>锁身顿帧结束，开始吊升</summary>
        internal const int SnapEnd = 60;
        /// <summary>吊升结束</summary>
        internal const int LiftEnd = 88;
        /// <summary>幻龙掠影第一击</summary>
        internal const int Beat1Hit = 114;
        /// <summary>幻龙掠影第二击</summary>
        internal const int Beat2Hit = 162;
        /// <summary>终结蓄力起点</summary>
        internal const int FinaleChargeStart = 184;
        /// <summary>远古光汇聚引爆</summary>
        internal const int FinaleHit = 232;
        /// <summary>玩家已掷出，锁定与运镜结束</summary>
        internal const int ReleaseEnd = 256;
        /// <summary>状态总时长（尾段为 boss 恢复拍）</summary>
        internal const int Duration = 288;
        /// <summary>扑空碎阵收尾时长</summary>
        internal const int WhiffTail = 34;

        /// <summary>锁身判定半径（与收拢环视觉终点精确对齐）</summary>
        internal const float SealRadius = 170f;
        /// <summary>收拢环起始半径</summary>
        internal const float SealStartRadius = 420f;
        /// <summary>吊升高度</summary>
        internal const float LiftHeight = 110f;
        /// <summary>献祭环半径（boss与分身环列）</summary>
        internal const float RingRadius = 340f;

        /// <summary>命中后冷却（45s）</summary>
        internal const int CooldownTicks = 2700;
        /// <summary>扑空冷却（15s）</summary>
        internal const int WhiffCooldownTicks = 900;
        /// <summary>献祭印记时长（30s）</summary>
        internal const int BrandDuration = 1800;
        #endregion

        //扑空分支的本地收尾计时（各端自走）
        private int whiffTimer;

        #region 触发裁决（服务端，分身AI调用）
        /// <summary>
        /// 服务端登记一次"打错分身"：首次烙印，印记在身且闸门放行则触发献祭投技；
        /// 返回 true=投技已触发（调用方跳过电火花反击）
        /// </summary>
        internal static bool RegisterMirrorMistake(CultistBossAI bossOverride, int attackerIdx) {
            if (VaultUtils.isClient || bossOverride?.Context == null || bossOverride.Machine == null) {
                return false;
            }
            CultistStateContext ctx = bossOverride.Context;
            if (attackerIdx < 0 || attackerIdx >= Main.maxPlayers) {
                return false;
            }
            Player attacker = Main.player[attackerIdx];
            if (!attacker.Alives()) {
                return false;
            }

            if (ctx.BrandTimers[attackerIdx] <= 0) {
                //首次失误：烙上献祭印记（印记弹幕=全端可见的警示）
                ctx.BrandTimers[attackerIdx] = BrandDuration;
                RespawnBrandProj(ctx, attackerIdx, attacker.Center);
                return false;
            }

            //已烙印：再次失误，校验投技闸门
            if (!CanTrigger(bossOverride, attacker)) {
                //刷新印记：视觉弹幕同步重生（timeLeft 不入同步包，只能杀旧生新）
                ctx.BrandTimers[attackerIdx] = BrandDuration;
                RespawnBrandProj(ctx, attackerIdx, attacker.Center);
                return false;
            }

            //触发：锁阵中心=失误瞬间位置快照（不追踪，给足逃逸窗口）
            ctx.GrabTargetIndex = attackerIdx;
            ctx.GrabResult = 0;
            ctx.RitualCenter = attacker.Center;
            ctx.BrandTimers[attackerIdx] = 0;
            bossOverride.Machine.ChangeState(new CultistSacrificeGrabState());
            ctx.Npc.netUpdate = true;
            return true;
        }

        /// <summary>服务端：杀掉该玩家的旧印记弹幕并重生（Kill 与生成各自入同步）</summary>
        private static void RespawnBrandProj(CultistStateContext ctx, int playerIdx, Vector2 pos) {
            int brandType = ModContent.ProjectileType<CultistSacrificeBrand>();
            foreach (var p in Main.ActiveProjectiles) {
                if (p.type == brandType && (int)p.ai[0] == playerIdx && (int)p.ai[1] == ctx.Npc.whoAmI) {
                    p.Kill();
                }
            }
            Projectile.NewProjectile(ctx.Npc.GetSource_FromAI(), pos, Vector2.Zero,
                brandType, 0, 0f, Main.myPlayer, playerIdx, ctx.Npc.whoAmI);
        }

        /// <summary>投技闸门：冷却/状态/硬直/时停/演出/距离</summary>
        private static bool CanTrigger(CultistBossAI bossOverride, Player attacker) {
            CultistStateContext ctx = bossOverride.Context;
            NPC npc = ctx.Npc;
            if (ctx.GrabCooldown > 0) {
                return false;
            }
            //只在轮盘博弈期触发（本身排除 Intro/转阶段/大招/死亡/撤离）
            if (bossOverride.Machine.CurrentState is not CultistMirrorBlinkState) {
                return false;
            }
            //玩家刚赢下硬直窗口，不抢戏
            if (ctx.StaggerTimer > 0) {
                return false;
            }
            //世界时停期间不触发
            if (TimeFreezeSystem.IsFrozen(npc)) {
                return false;
            }
            //单机端（权威端=本地端）有演出在播不触发
            if (!Main.dedServ && CutsceneDirector.CurrentClip != null) {
                return false;
            }
            if (npc.Distance(attacker.Center) > 2400f) {
                return false;
            }
            return true;
        }
        #endregion

        #region 位形（受害者/弹幕/运镜共用的确定性公式）
        /// <summary>阵心当前位置：快照点+吊升曲线</summary>
        internal static Vector2 SealCenter(CultistStateContext context, int t) {
            float lift = t <= SnapEnd
                ? 0f
                : MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((t - SnapEnd) / (float)(LiftEnd - SnapEnd), 0f, 1f));
            return context.RitualCenter - new Vector2(0f, LiftHeight * lift);
        }

        /// <summary>献祭环上某席位的位置（slot 0=顶位真身）</summary>
        internal static Vector2 RingSlotPos(CultistStateContext context, int t, int slot, int slotCount) {
            float angle = -MathHelper.PiOver2 + MathHelper.TwoPi * slot / Math.Max(slotCount, 1) + t * 0.0045f;
            return SealCenter(context, t) + angle.ToRotationVector2() * RingRadius;
        }

        /// <summary>献祭环席位总数（真身+分身名册），各端同源</summary>
        internal static int RingSlotCount(CultistStateContext context)
            => Math.Max(context.Clones.Count + 1, 2);

        /// <summary>收拢环当前半径（视觉与判定同源）</summary>
        internal static float CloseRadius(int t) {
            float p = MathHelper.Clamp(t / (float)SealCloseEnd, 0f, 1f);
            //前松后紧的收拢：读秒感
            return MathHelper.Lerp(SealStartRadius, SealRadius, p * p * (3f - 2f * p));
        }
        #endregion

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            whiffTimer = 0;
            NPC npc = context.Npc;
            //演出期免伤：这是玩家失误换来的惩罚拍，不做输出窗口
            npc.dontTakeDamage = true;
            npc.alpha = 0;
            //刷新分身名册，环位数各端一致
            context.RefreshClones();
            if (!VaultUtils.isClient) {
                //公平阀：清掉本 boss 存量敌对弹幕，连段节拍不被污染
                CultistBossAI.ClearHostileProjectiles();
                context.GrabResult = 0;
                //锁阵演出弹幕：全端可见的世界空间视觉载体
                Projectile.NewProjectile(npc.GetSource_FromAI(), context.RitualCenter, Vector2.Zero,
                    ModContent.ProjectileType<CultistSacrificeSeal>(), 0, 0f, Main.myPlayer, npc.whoAmI);
                npc.netUpdate = true;
            }
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;
            int t = (int)Timer;

            context.SkipDefaultHover = true;
            context.ElementAura = 1f;
            //演出期免伤每帧重申（防狂暴沿边等外部清除竞态）
            npc.dontTakeDamage = true;
            //比大仪式更重的帷幕：处刑舞台
            CultistScreenFX.DeclareVeil(SealCenter(context, t), 0.5f, context.Element);

            Player victim = ValidVictim(context);

            //——服务端异常出口：目标死亡/离场/被传送——
            if (!VaultUtils.isClient) {
                bool victimGone = victim == null;
                bool victimEscapedWorld = !victimGone && context.GrabResult == 1
                    && victim.Center.Distance(SealCenter(context, t)) > 1600f;
                if ((victimGone || victimEscapedWorld) && context.GrabResult != 2) {
                    //断投：按扑空收场（碎阵+半冷却）
                    context.GrabResult = 2;
                    npc.netUpdate = true;
                }
            }

            //——扑空/断投分支：碎阵短收尾——
            if (context.GrabResult == 2) {
                whiffTimer++;
                context.CastPose = CultistPose.Stand;
                context.CastGlow = Math.Max(0.6f - whiffTimer * 0.02f, 0f);
                HoldRingPosition(context, npc, t);
                if (whiffTimer >= WhiffTail && !VaultUtils.isClient) {
                    return new CultistWeaveState();
                }
                return null;
            }

            //——收拢 telegraph 0..47：boss 就位嘶吼，锁阵读秒——
            if (t < SealCloseEnd) {
                UpdateTelegraph(context, npc, victim, t);
                return null;
            }

            //——判定帧 t=48：服务端裁决抓取——
            if (t == SealCloseEnd && !VaultUtils.isClient && context.GrabResult == 0) {
                bool caught = victim != null
                    && victim.Center.Distance(context.RitualCenter) <= SealRadius;
                context.GrabResult = caught ? 1 : 2;
                npc.netUpdate = true;
                if (!caught) {
                    return null;
                }
            }

            //收拢已到底但裁决尚未同步到本端：白热定格拍（掩盖同步抖动；
            //镜像长期不达时由 ai[2] 状态跟随兜底收场）
            if (context.GrabResult == 0) {
                context.CastPose = CultistPose.CastForward;
                context.CastGlow = 1f;
                HoldRingPosition(context, npc, t);
                return null;
            }

            //——锁身演出主线（GrabResult==1）——
            UpdatePerformance(context, npc, t);

            //超时保底
            if (t >= Duration && !VaultUtils.isClient) {
                return new CultistWeaveState();
            }
            return null;
        }

        /// <summary>校验投技目标有效性，无效返回 null</summary>
        private static Player ValidVictim(CultistStateContext context) {
            int idx = context.GrabTargetIndex;
            if (idx < 0 || idx >= Main.maxPlayers) {
                return null;
            }
            Player player = Main.player[idx];
            return player.Alives() ? player : null;
        }

        /// <summary>收拢期：boss 瞬移到环顶嘶吼压场</summary>
        private void UpdateTelegraph(CultistStateContext context, NPC npc, Player victim, int t) {
            //起手帧：boss 瞬移到锁阵上方环顶位
            if (t == 2) {
                Vector2 apex = RingSlotPos(context, t, 0, RingSlotCount(context));
                if (!VaultUtils.isClient) {
                    CultistBossAI.BlinkTo(context, apex);
                }
                else {
                    CultistRenderHelper.BlinkOut(npc.Center, context.Element);
                    CultistRenderHelper.BlinkIn(apex, context.Element);
                }
                if (!VaultUtils.isServer) {
                    //定罪钟鸣：锁阵启动的专属听觉信号
                    SoundEngine.PlaySound(SoundID.Item123 with { Volume = 1f, Pitch = -0.45f }, context.RitualCenter);
                    SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1.1f, Pitch = 0.15f }, npc.Center);
                }
            }

            HoldRingPosition(context, npc, t);
            FaceSeal(context, npc);
            context.CastPose = CultistPose.Scream;
            context.CastGlow = MathHelper.Clamp(t / 30f, 0f, 1f);

            //吟唱读秒：升调加速（密度过 72% 静默，尖啸前的吸气）
            if (!VaultUtils.isServer) {
                float p = t / (float)SealCloseEnd;
                int interval = (int)MathHelper.Lerp(14f, 7f, p);
                if (p < 0.72f && t % Math.Max(interval, 5) == 0) {
                    CultistRenderHelper.ChantVoice(context.RitualCenter, 0.8f, MathHelper.Lerp(-0.2f, 0.4f, p));
                }
                //第二层预警：半程低钟
                if (t == SealCloseEnd / 2) {
                    SoundEngine.PlaySound(SoundID.Item119 with { Volume = 0.9f, Pitch = -0.3f }, context.RitualCenter);
                }
            }
        }

        /// <summary>锁身演出：吊升→两拍幻龙掠影→远古光汇聚引爆→恢复</summary>
        private void UpdatePerformance(CultistStateContext context, NPC npc, int t) {
            HoldRingPosition(context, npc, t);
            FaceSeal(context, npc);

            //锁身瞬间（各端按本地时间轴同演）
            if (t == SealCloseEnd + 1) {
                CultistScreenFX.PushFlash(0.5f, 14);
                CultistScreenFX.Punch(context.RitualCenter, 9f, 14, "CultistSacrificeSnap");
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Shatter with { Volume = 1f, Pitch = -0.2f }, context.RitualCenter);
                    SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1.1f, Pitch = -0.3f }, npc.Center);
                }
                //服务端：分身瞬移列席献祭环
                if (!VaultUtils.isClient) {
                    ArrangeCloneRing(context, t);
                }
            }

            //姿态节拍
            if (t < LiftEnd) {
                //吊升段：上举施法
                context.CastPose = CultistPose.CastUp;
                context.CastGlow = 0.8f;
            }
            else if (t < FinaleChargeStart) {
                //掠影连段：拍前摇前施法，拍后余韵回落
                bool beat1Windup = t >= Beat1Hit - 26 && t <= Beat1Hit;
                bool beat2Windup = t >= Beat2Hit - 26 && t <= Beat2Hit;
                context.CastPose = beat1Windup || beat2Windup ? CultistPose.CastForward : CultistPose.CastUp;
                context.CastGlow = beat1Windup || beat2Windup ? 1f : 0.6f;
            }
            else if (t < FinaleHit) {
                //终结蓄力：嘶吼到底
                context.CastPose = CultistPose.Scream;
                context.CastGlow = MathHelper.Clamp((t - FinaleChargeStart) / (float)(FinaleHit - FinaleChargeStart), 0f, 1f);
            }
            else {
                //释放恢复：喘息
                context.CastPose = CultistPose.Stand;
                context.CastGlow = Math.Max(1f - (t - FinaleHit) * 0.03f, 0f);
            }

            //拍点音画（世界空间部分由锁阵弹幕承担，这里只做屏幕级）
            if (t == Beat1Hit || t == Beat2Hit) {
                CultistScreenFX.PushFlash(0.25f, 10);
                CultistScreenFX.Punch(SealCenter(context, t), 6f, 10, "CultistSacrificeBeat");
            }
            //终结引爆：本场投技唯一的大震
            if (t == FinaleHit) {
                CultistScreenFX.PushFlash(0.9f, 26);
                CultistScreenFX.Punch(SealCenter(context, t), 12f, 20, "CultistSacrificeFinale");
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.1f, Pitch = -0.35f }, SealCenter(context, t));
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f, Pitch = -0.4f }, SealCenter(context, t));
                }
            }
        }

        /// <summary>boss 弹簧保持环顶位</summary>
        private static void HoldRingPosition(CultistStateContext context, NPC npc, int t) {
            Vector2 goal = RingSlotPos(context, t, 0, RingSlotCount(context));
            Vector2 desired = (goal - npc.Center) * 0.1f;
            if (desired.Length() > 16f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 16f;
            }
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.16f);
            npc.rotation = npc.velocity.X * 0.012f;
        }

        /// <summary>面向阵心</summary>
        private static void FaceSeal(CultistStateContext context, NPC npc) {
            int sign = Math.Sign(context.RitualCenter.X - npc.Center.X);
            if (sign != 0) {
                npc.direction = npc.spriteDirection = sign;
            }
        }

        /// <summary>服务端：把分身瞬移列席献祭环（slot 1 起，顶位留给真身）</summary>
        private static void ArrangeCloneRing(CultistStateContext context, int t) {
            context.RefreshClones();
            int slotCount = RingSlotCount(context);
            for (int i = 0; i < context.Clones.Count; i++) {
                NPC clone = context.Clones[i];
                if (!clone.Alives()) {
                    continue;
                }
                clone.Center = RingSlotPos(context, t, i + 1, slotCount);
                clone.velocity = Vector2.Zero;
                clone.ai[0] = i;
                clone.netUpdate = true;
            }
        }

        public override void OnExit(CultistStateContext context) {
            base.OnExit(context);
            NPC npc = context.Npc;
            npc.dontTakeDamage = false;
            npc.alpha = 0;
            if (!VaultUtils.isClient) {
                //冷却按结果分级：命中足额 45s，扑空/断投短冷却 15s
                context.GrabCooldown = context.GrabResult == 1 ? CooldownTicks : WhiffCooldownTicks;
                if (context.GrabTargetIndex >= 0 && context.GrabTargetIndex < Main.maxPlayers) {
                    context.BrandTimers[context.GrabTargetIndex] = 0;
                }
                context.GrabTargetIndex = -1;
                context.GrabResult = 0;
                npc.netUpdate = true;
            }
        }
    }
}

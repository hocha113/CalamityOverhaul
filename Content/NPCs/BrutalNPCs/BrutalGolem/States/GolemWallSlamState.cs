using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States
{
    /// <summary>壁咚研磨投技（二阶段）：慢蓄超级直拳 → 命中钉墙 → 眼激光横扫 + 胸口束点烙 → 拳沿面研磨收尾
    /// 抓取本体在 GolemFistGrabState，本状态负责出拳编排与连段火控</summary>
    [InnoVault.StateMachines.VaultState((int)GolemStateIndex.WallSlam, typeof(GolemStateContext))]
    internal class GolemWallSlamState : GolemStateBase
    {
        public override string StateName => "WallSlam";
        public override GolemStateIndex StateIndex => GolemStateIndex.WallSlam;

        private enum Step : int
        {
            /// <summary>拳蓄力（预警线已画）</summary>
            Windup = 0,
            /// <summary>拳在途</summary>
            Flight = 1,
            /// <summary>抓取连段（节拍以拳入 Grab 起算）</summary>
            Combo = 2,
            /// <summary>恢复拍（命中与落空共用）</summary>
            Recover = 3,
        }

        /// <summary>眼激光横扫生成拍（连段计时）</summary>
        private const int RakeSpawnBeat = 44;
        /// <summary>胸口束点烙生成拍</summary>
        private const int BrandSpawnBeat = 86;

        private Step step;
        private int stepTimer;
        private int comboTimer;
        private int fistIndex;

        public override void OnEnter(GolemStateContext context) {
            base.OnEnter(context);
            step = Step.Windup;
            stepTimer = 0;
            comboTimer = 0;
            fistIndex = -1;

            NPC npc = context.Npc;
            //尝试即入冷却：命中与落空都要付出机会成本
            context.LastGrabTick = Main.GameUpdateCount;

            if (!VaultUtils.isClient) {
                DispatchSuperPunch(context);
            }
            if (!VaultUtils.isServer) {
                //低吼宣告：这一拳与连拳不同
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 0.8f }, npc.Center);
            }
        }

        public override IGolemState OnUpdate(GolemStateContext context) {
            NPC npc = context.Npc;
            GroundBrake(npc);
            npc.noTileCollide = false;

            switch (step) {
                case Step.Windup: {
                    //蹲伏蓄势，宝石随拳同步充能
                    context.FrameMode = 1;
                    float t = MathHelper.Clamp(stepTimer / (float)GolemDirector.GrabWindup, 0f, 1f);
                    context.SetChargeState(1, t * 0.6f);
                    context.VeinGlow = Math.Max(context.VeinGlow, 0.3f + t * 0.3f);

                    GolemFistStateIndex fistState = FistState(context);
                    if (fistState is GolemFistStateIndex.Punch) {
                        step = Step.Flight;
                        stepTimer = 0;
                        break;
                    }
                    //中途加入恢复：拳已在抓取则直跳连段
                    if (fistState == GolemFistStateIndex.Grab) {
                        step = Step.Combo;
                        stepTimer = 0;
                        comboTimer = 0;
                        break;
                    }
                    //拳丢失/指令未接上：放弃
                    if (++stepTimer > GolemDirector.GrabWindup + 40 || fistState == GolemFistStateIndex.Invalid) {
                        step = Step.Recover;
                        stepTimer = 0;
                    }
                    break;
                }
                case Step.Flight: {
                    context.FrameMode = 0;
                    context.SetChargeState(1, 0.6f);

                    GolemFistStateIndex fistState = FistState(context);
                    if (fistState == GolemFistStateIndex.Grab) {
                        step = Step.Combo;
                        stepTimer = 0;
                        comboTimer = 0;
                        break;
                    }
                    //落空回收（Return/Anchor）或拳丢失：进恢复拍
                    if (fistState is GolemFistStateIndex.Return or GolemFistStateIndex.Anchor
                        or GolemFistStateIndex.Invalid || ++stepTimer > 160) {
                        step = Step.Recover;
                        stepTimer = 0;
                    }
                    break;
                }
                case Step.Combo: {
                    context.FrameMode = 0;
                    UpdateCombo(context);

                    //拳离开抓取（正常释放或异常断投）→ 恢复
                    if (FistState(context) != GolemFistStateIndex.Grab || ++stepTimer > 240) {
                        step = Step.Recover;
                        stepTimer = 0;
                    }
                    break;
                }
                case Step.Recover: {
                    context.FrameMode = 0;
                    context.ResetChargeState();
                    if (++stepTimer >= Tempo(context, 46) && !VaultUtils.isClient) {
                        return new GolemConnectorState();
                    }
                    break;
                }
            }

            Timer++;
            //全局保底
            if (Timer > 520 && !VaultUtils.isClient) {
                return new GolemConnectorState();
            }
            return null;
        }

        /// <summary>连段火控：眼激光横扫与胸口束点烙都对准钉压点（服务端生成，各端表现）</summary>
        private void UpdateCombo(GolemStateContext context) {
            NPC npc = context.Npc;
            comboTimer++;

            //胸口蓄能表现：束点烙前的汇聚与静默
            if (comboTimer >= BrandSpawnBeat - 26 && comboTimer <= BrandSpawnBeat + 30) {
                float t = MathHelper.Clamp((comboTimer - (BrandSpawnBeat - 26)) / 40f, 0f, 1f);
                context.SetChargeState(1, t);
                context.VeinGlow = Math.Max(context.VeinGlow, t);
            }

            if (VaultUtils.isClient) {
                return;
            }

            NPC fist = GolemFacts.FindGrabbingFist(context.Limbs);
            GolemFistAI fistOverride = fist != null ? GolemFacts.FindOverride<GolemFistAI>(fist) : null;
            if (fistOverride == null) {
                return;
            }
            Vector2 pin = new(fistOverride.ai[GolemAiSlots.FistPinX], fistOverride.ai[GolemAiSlots.FistPinY]);
            if (pin.LengthSquared() < 1f) {
                return;
            }

            //拍一：头部眼激光横扫过身
            if (comboTimer == RakeSpawnBeat) {
                Vector2 muzzle = RakeMuzzle(context, pin);
                float angle = (pin - muzzle).ToRotation();
                GolemGrabRay.Fire(npc, muzzle, angle, telegraphFrames: 26, sweepHalfSpan: 0.32f,
                    damage: ScaleDamage(context, GolemDirector.GrabRakeDamage));
            }

            //拍二：胸口宝石束点烙
            if (comboTimer == BrandSpawnBeat) {
                Vector2 gem = npc.Center + new Vector2(0f, -6f);
                float angle = (pin - gem).ToRotation();
                GolemGrabRay.Fire(npc, gem, angle, telegraphFrames: 30, sweepHalfSpan: 0f,
                    damage: ScaleDamage(context, GolemDirector.GrabBrandDamage));
                npc.netUpdate = true;
            }
        }

        /// <summary>横扫发射口：优先分离飞头眼位，缺席时退回躯干头锚</summary>
        private static Vector2 RakeMuzzle(GolemStateContext context, Vector2 pin) {
            GolemLimbStatus limbs = context.Limbs;
            if (limbs.FreeHeadAlive) {
                NPC head = Main.npc[limbs.FreeHeadIndex];
                return head.Center + new Vector2(0f, 4f);
            }
            //降级：从躯干头位射出，几何仍指向钉点
            return GolemFacts.HeadAnchor(context.Npc) + new Vector2(0f, -20f);
        }

        /// <summary>下达超级直拳（服务端）：同侧拳、锁定预读点、画预警线</summary>
        private void DispatchSuperPunch(GolemStateContext context) {
            GolemLimbStatus limbs = context.Limbs;
            int sign = Math.Sign(context.Target.Center.X - context.Npc.Center.X);
            if (sign == 0) {
                sign = 1;
            }
            fistIndex = sign < 0 ? limbs.LeftFistIndex : limbs.RightFistIndex;
            if (fistIndex < 0) {
                fistIndex = sign < 0 ? limbs.RightFistIndex : limbs.LeftFistIndex;
            }
            if (fistIndex < 0) {
                //无拳可用：直接进恢复，选择器下次会跳过本状态
                step = Step.Recover;
                return;
            }

            //锁定预读点：出拳线在蓄力起点即固定，预警线即真实弹道
            Vector2 aim = context.Target.Center + context.Target.velocity * 10f;
            GolemBodyAI.CommandFist(fistIndex, GolemFistCommand.SuperPunch, aim,
                GolemDirector.GrabWindup, GolemDirector.GrabPunchSpeed, bounce: 0);

            NPC fist = Main.npc[fistIndex];
            Vector2 anchor = GolemFacts.FistAnchor(context.Npc, fist.type == NPCID.GolemFistLeft ? -1 : 1);
            GolemTelegraph.SpawnLine(context.Npc, anchor, (aim - anchor).ToRotation(),
                GolemDirector.GrabWindup + 6);
        }

        /// <summary>读所遣拳的当前状态，拳失效返回 Invalid；客户端/中途加入按指令类型自解析</summary>
        private GolemFistStateIndex FistState(GolemStateContext context) {
            if (fistIndex < 0 || fistIndex >= Main.maxNPCs) {
                TryResolveFist(context);
            }
            if (fistIndex < 0 || fistIndex >= Main.maxNPCs) {
                return GolemFistStateIndex.Invalid;
            }
            NPC fist = Main.npc[fistIndex];
            if (!fist.active || fist.type != NPCID.GolemFistLeft && fist.type != NPCID.GolemFistRight) {
                return GolemFistStateIndex.Invalid;
            }
            return (GolemFistStateIndex)(int)fist.ai[GolemAiSlots.PartStateSlot];
        }

        /// <summary>客户端不经手指令下达，按"持超级直拳指令且已动身"的拳自解析</summary>
        private void TryResolveFist(GolemStateContext context) {
            GolemLimbStatus limbs = context.Limbs;
            Span<int> candidates = [limbs.LeftFistIndex, limbs.RightFistIndex];
            foreach (int index in candidates) {
                if (index < 0 || index >= Main.maxNPCs) {
                    continue;
                }
                NPC fist = Main.npc[index];
                if (!fist.active) {
                    continue;
                }
                //排除滞留旧指令的锚定拳，只认在途/抓取中的
                GolemFistStateIndex state = (GolemFistStateIndex)(int)fist.ai[GolemAiSlots.PartStateSlot];
                if (state is not GolemFistStateIndex.Windup and not GolemFistStateIndex.Punch
                    and not GolemFistStateIndex.Grab) {
                    continue;
                }
                GolemFistAI fistOverride = GolemFacts.FindOverride<GolemFistAI>(fist);
                if (fistOverride == null
                    || (int)fistOverride.ai[GolemAiSlots.FistCmdKind] != (int)GolemFistCommand.SuperPunch) {
                    continue;
                }
                fistIndex = index;
                return;
            }
        }

        /// <summary>触发阀：冷却、距离、阶段、时停、双拳与目标有效性（服务端选择器调用）</summary>
        internal static bool GrabReady(GolemStateContext context) {
            if (context.Limbs.FistCount == 0 || !context.Target.Alives() || context.Target.shimmering) {
                return false;
            }
            if (context.LastGrabTick != 0
                && Main.GameUpdateCount - context.LastGrabTick < (uint)GolemDirector.GrabCooldown) {
                return false;
            }
            //世界时停/演出冻结期间不出投技
            if (TimeFreezeSystem.IsAnyGlobalFreezeActive) {
                return false;
            }
            NPC npc = context.Npc;
            float dist = npc.Distance(context.Target.Center);
            if (dist < GolemDirector.GrabMinRange || dist > GolemDirector.GrabMaxRange) {
                return false;
            }
            return Math.Abs(context.Target.Center.Y - npc.Center.Y) <= GolemDirector.GrabMaxHeightDiff;
        }
    }
}

using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 终章步进剧本：加速长枪墙×3 → 停顿A → 智能长枪×2 → 停顿B → 反向日舞×3 → 终末警告 → 终末日舞（缩圈）。
    /// 浪高不断抬升，最后收束成"一个圈"。步进计数在 Counter，服务端权威（客户端只跟姿态/表现）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.Finale, typeof(EmpressStateContext))]
    internal class EmpressFinaleState : EmpressStateBase
    {
        public override string StateName => "EmpressFinale";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.Finale;

        private enum Step
        {
            FastWalls0, FastWalls1, FastWalls2,
            PauseA,
            SmartLances0, SmartLances1,
            PauseB,
            ReverseDance0, ReverseDance1, ReverseDance2,
            FinalWarning,
            FinalDance,
        }

        /// <summary>缩圈：起止半径与用时（20 秒）</summary>
        internal const float ArenaStart = 3000f;
        internal const float ArenaEnd = 700f;
        internal const int ShrinkFrames = 1200;

        private static readonly int[] FastWallDurations = [150, 110, 80];
        private static readonly int[] FastWallIntervals = [40, 25, 16];
        private const int PauseADuration = 120;
        private const int SmartDuration = 170;
        private const int PauseBDuration = 150;
        private const int ReverseLead = 60;
        private const int ReverseDuration = 275;
        private const int WarningDuration = 200;

        private Step step;
        private int stepTimer;
        private int shrinkTimer;
        private EmpressStateContext Context;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            Context = context;
            step = Step.FastWalls0;
            stepTimer = 0;
            shrinkTimer = 0;
            context.FinaleKillable = false;
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;
            stepTimer++;

            if (!target.Alives()) {
                npc.velocity *= 0.95f;
                return null;
            }

            switch (step) {
                case Step.FastWalls0:
                case Step.FastWalls1:
                case Step.FastWalls2:
                    FastWalls(context, npc, target, (int)step);
                    break;
                case Step.PauseA:
                    Pause(context, npc, target, PauseADuration, 0);
                    break;
                case Step.SmartLances0:
                case Step.SmartLances1:
                    SmartLances(context, npc, target);
                    break;
                case Step.PauseB:
                    Pause(context, npc, target, PauseBDuration, 1);
                    break;
                case Step.ReverseDance0:
                case Step.ReverseDance1:
                case Step.ReverseDance2:
                    ReverseDance(context, npc, target);
                    break;
                case Step.FinalWarning:
                    FinalWarning(context, npc, target);
                    break;
                case Step.FinalDance:
                    FinalDance(context, npc, target);
                    break;
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);
            //终章不自然结束：死亡/离场由主控层面强制切换
            return null;
        }

        private void Advance() {
            step = (Step)Math.Min((int)step + 1, (int)Step.FinalDance);
            stepTimer = 0;
            Context.Npc.netUpdate = true;
        }

        #region 步 0~2 加速长枪墙
        /// <summary>墙的间隔越拧越紧：40 → 25 → 16；她只在朝向玩家时额外前冲</summary>
        private void FastWalls(EmpressStateContext context, NPC npc, Player target, int idx) {
            context.Pose = EmpressPose.CastBoth;
            context.PoseTimer = 40f;
            context.ArenaRadiusRequest = 2600f;

            Vector2 dest = target.Center + new Vector2(target.Center.X > npc.Center.X ? -420f : 420f, -220f);
            npc.velocity = npc.velocity * 0.7f + (dest - npc.Center) * 0.045f;
            float toward = Math.Max(Vector2.Dot(npc.velocity.SafeNormalize(Vector2.Zero), npc.DirectionTo(target.Center)), 0f);
            npc.position += npc.velocity * (0.5f + Math.Min(npc.Distance(target.Center) / 400f, 2f)) * toward * 0.4f;

            int interval = FastWallIntervals[idx];
            if (stepTimer % interval == 0 && !VaultUtils.isClient) {
                int wallIdx = stepTimer / interval + idx * 5;
                Vector2 dir = (wallIdx % 4) switch {
                    0 => Vector2.UnitX,
                    1 => -Vector2.UnitX,
                    2 => new Vector2(-0.7071f, -0.7071f),
                    _ => new Vector2(0.7071f, -0.7071f),
                };
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
                int n = 10;
                int gap = (wallIdx * 3 + 1) % n;
                for (int i = 0; i < n; i++) {
                    if (i == gap) {
                        continue;
                    }
                    Vector2 pos = target.Center - dir * 1300f + perp * (EmpressLanceWallState.LaneSpacing * (i - n / 2f + 0.5f));
                    EmpressCast.Lance(npc, pos, dir.ToRotation(), context.LanceDamage, EmpressLanceMode.Fast);
                }
                PlayLocal(SoundID.Item162 with { Volume = 0.6f, Pitch = -0.2f + idx * 0.15f }, npc.Center);
            }

            if (stepTimer >= FastWallDurations[idx]) {
                Advance();
            }
        }
        #endregion

        #region 停顿
        /// <summary>停顿：飞到玩家侧面 275，最后 30f 切到头顶 325；-90f 预光、-30f 闪；末尾一跃入招</summary>
        private void Pause(EmpressStateContext context, NPC npc, Player target, int duration, int style) {
            context.Pose = EmpressPose.Idle;
            context.PoseTimer = 0f;
            context.ArenaRadiusRequest = 2400f;
            int left = duration - stepTimer;
            bool above = left < 30;

            if (style == 1 && stepTimer < 60) {
                npc.velocity *= 0.96f;
            }
            else {
                int sign = Math.Sign(target.Center.X - npc.Center.X);
                Vector2 dest = target.Center + (above ? new Vector2(0f, -325f) : new Vector2(sign * -275f, 0f));
                npc.velocity = npc.velocity * 0.5f + (dest - npc.Center) * (above ? 0.07f : 0.02f);
            }

            if (!VaultUtils.isServer) {
                if (left == 90) {
                    EmpressScreenFX.PushPhaseGlow();
                }
                if (left == 30) {
                    EmpressScreenFX.PushPhaseFlash(0.4f);
                    PlayLocal(SoundID.Item165 with { Volume = 0.8f, Pitch = 0.2f }, npc.Center);
                }
            }

            if (stepTimer >= duration) {
                HopOut(npc);
                Advance();
            }
        }
        #endregion

        #region 步 4~5 智能长枪
        private void SmartLances(EmpressStateContext context, NPC npc, Player target) {
            context.Pose = EmpressPose.CastLeft;
            context.PoseTimer = MathHelper.Clamp(stepTimer % 45, 0f, 45f);
            context.ArenaRadiusRequest = 2400f;

            Vector2 dest = target.Center + new Vector2(target.Center.X > npc.Center.X ? -380f : 380f, -260f);
            npc.velocity = npc.velocity * 0.72f + (dest - npc.Center) * 0.04f;

            if (VaultUtils.isClient) {
                if (stepTimer == 1) {
                    PlayLocal(SoundID.Item165 with { Volume = 1.2f, Pitch = 0.5f }, npc.Center);
                }
                return;
            }

            if (stepTimer == 1) {
                PlayLocal(SoundID.Item165 with { Volume = 1.2f, Pitch = 0.5f }, npc.Center);
                //开门一记：12 发追踪光球扇（自左右各 6 发）
                for (int side = -1; side <= 1; side += 2) {
                    for (int j = 2; j <= 7; j++) {
                        Vector2 dir = (-MathHelper.PiOver2 + MathHelper.PiOver2 * (j / 7f) * side).ToRotationVector2();
                        EmpressCast.Bolt(npc, npc.Center + dir * 120f, npc.velocity * 0.3f + dir * (3f - j * 0.3f), context.BoltDamage, EmpressBoltMode.Chase, target.whoAmI);
                    }
                }
            }

            //三轮，每轮 5 枪围着预测点的扇位出生（不重叠，留缝）
            if (stepTimer % 45 == 20 && stepTimer < 155) {
                Vector2 aim = EmpressMotion.Intercept(npc.Center, target, 100f);
                float baseAngle = npc.AngleTo(aim);
                for (int i = -2; i <= 2; i++) {
                    float spawnAngle = baseAngle + MathHelper.Pi + i * 0.35f;
                    Vector2 pos = aim + spawnAngle.ToRotationVector2() * 900f;
                    EmpressCast.Lance(npc, pos, pos.AngleTo(aim) + i * 0.08f, context.LanceDamage, EmpressLanceMode.Smart);
                }
                PlayLocal(SoundID.Item162 with { Volume = 0.7f, Pitch = 0.2f }, npc.Center);
            }

            if (stepTimer >= SmartDuration) {
                Advance();
            }
        }
        #endregion

        #region 步 7~9 反向日舞
        /// <summary>前 60f 引子里光晕与震屏线性堆到爆；然后一根持续追踪束 + 每 69f 两圈反向自旋光球</summary>
        private void ReverseDance(EmpressStateContext context, NPC npc, Player target) {
            context.Pose = EmpressPose.Dance;
            context.PoseTimer = MathHelper.Clamp(stepTimer, 10f, 170f);
            context.ArenaRadiusRequest = 2400f;
            npc.velocity *= 0.85f;
            npc.velocity.Y += (target.Center.Y - 360f - npc.Center.Y) * 0.0012f;

            bool first = step == Step.ReverseDance0;
            if (first && stepTimer <= ReverseLead) {
                float k = stepTimer / (float)ReverseLead;
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.DeclareAmbient(0.5f + 0.5f * k);
                    if (stepTimer % 6 == 0) {
                        EmpressMotion.Shake(npc.Center, 0.5f + 3f * k, 6);
                    }
                }
                if (stepTimer == 1) {
                    PlayLocal(SoundID.NPCDeath58 with { Volume = 1f, Pitch = 0.2f }, npc.Center);
                    PlayLocal(SoundID.Item77 with { Volume = 1f, Pitch = -0.33f }, npc.Center);
                }
                return;
            }
            int t = first ? stepTimer - ReverseLead : stepTimer;
            if (!VaultUtils.isServer) {
                EmpressScreenFX.DeclareAmbient(0.8f);
            }

            if (!VaultUtils.isClient) {
                if (t == 8) {
                    float angle = npc.AngleTo(target.Center);
                    EmpressCast.Beam(npc, npc.Center + angle.ToRotationVector2() * 100f, angle, context.BeamDamage, EmpressBeamMode.Persistent, ReverseDuration - 90);
                }
                if (t % 69 == 0 && t > 0 && t < ReverseDuration) {
                    float phase = t / (float)ReverseDuration * MathHelper.TwoPi;
                    for (int s = -1; s <= 1; s += 2) {
                        for (int j = 0; j < 22; j++) {
                            Vector2 dir = (j * MathHelper.TwoPi / 22f + phase).ToRotationVector2();
                            EmpressCast.Bolt(npc, npc.Center, dir * 11f, context.BoltDamage, EmpressBoltMode.Spinning, 0.0009f * s);
                        }
                    }
                    PlayLocal(SoundID.Item164 with { Volume = 0.7f, Pitch = 0.3f }, npc.Center);
                }
            }

            if (t >= ReverseDuration) {
                Advance();
            }
        }
        #endregion

        #region 步 10 终末警告
        /// <summary>两根竖直屏障束从她身侧升起慢慢合拢；震屏 1.2(t/T)²</summary>
        private void FinalWarning(EmpressStateContext context, NPC npc, Player target) {
            context.Pose = EmpressPose.Dance;
            context.PoseTimer = 170f;
            context.ArenaRadiusRequest = ArenaStart;
            npc.velocity *= 0.85f;
            npc.velocity.Y += (target.Center.Y - 360f - npc.Center.Y) * 0.0012f;

            if (stepTimer == 8 && !VaultUtils.isClient) {
                EmpressCast.Beam(npc, npc.Center + new Vector2(-75f, 0f), MathHelper.PiOver2, context.BeamDamage, EmpressBeamMode.Barrier, 900);
                EmpressCast.Beam(npc, npc.Center + new Vector2(75f, 0f), MathHelper.PiOver2, context.BeamDamage, EmpressBeamMode.Barrier, 900);
            }
            float k = stepTimer / (float)WarningDuration;
            if (!VaultUtils.isServer) {
                EmpressScreenFX.DeclareAmbient(0.9f);
                if (stepTimer % 5 == 0) {
                    EmpressMotion.Shake(npc.Center, 1.2f * k * k * 6f, 5);
                }
            }
            if (stepTimer >= WarningDuration) {
                shrinkTimer = 0;
                Advance();
            }
        }
        #endregion

        #region 步 11 终末日舞（缩圈）
        /// <summary>竞技场 3000→700 收缩 20 秒，收满后血量地板解除；循环持续追踪束 + 自旋环直到死亡</summary>
        private void FinalDance(EmpressStateContext context, NPC npc, Player target) {
            context.Pose = EmpressPose.Dance;
            context.PoseTimer = 170f;
            shrinkTimer++;
            float k = MathHelper.Clamp(shrinkTimer / (float)ShrinkFrames, 0f, 1f);
            context.ArenaRadiusRequest = MathHelper.Lerp(ArenaStart, ArenaEnd, k);
            context.ArenaFollowSpeed = 6f;
            if (k >= 1f && !context.FinaleKillable) {
                context.FinaleKillable = true;
                EmpressOfLightAI.SayPhase3(6);
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.PushPhaseFlash(0.5f);
                }
            }

            //缩圈期间她几乎不动，圆心跟她，所以她也就是圈心
            Vector2 dest = target.Center + new Vector2(0f, -300f);
            npc.velocity = Vector2.Lerp(npc.velocity, (dest - npc.Center) * 0.012f, 0.1f);

            if (!VaultUtils.isServer) {
                EmpressScreenFX.DeclareAmbient(0.7f + 0.3f * k);
            }

            if (VaultUtils.isClient) {
                return;
            }
            int t = stepTimer;
            if (t % 90 == 30) {
                float angle = npc.AngleTo(target.Center);
                EmpressCast.Beam(npc, npc.Center + angle.ToRotationVector2() * 100f, angle, context.BeamDamage, EmpressBeamMode.Persistent, 70);
            }
            if (t % 69 == 0) {
                int count = k >= 1f ? 16 : 22;
                float phase = t / 275f * MathHelper.TwoPi;
                for (int j = 0; j < count; j++) {
                    Vector2 dir = (j * MathHelper.TwoPi / count + phase).ToRotationVector2();
                    EmpressCast.Bolt(npc, npc.Center, dir * 10f, context.BoltDamage, EmpressBoltMode.Spinning, 0.0009f * (t / 69 % 2 == 0 ? 1 : -1));
                }
                PlayLocal(SoundID.Item164 with { Volume = 0.7f, Pitch = 0.35f }, npc.Center);
            }
        }
        #endregion
    }
}

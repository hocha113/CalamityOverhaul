using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.States
{
    /// <summary>
    /// A10 灵液门冲（P2 起，地形无关；平台/挖空场地的破土替身）：
    /// 蛇前方开进门、玩家侧开出口门（出口门生成即锁位锁向 = 预告实体，≥PortalOpenLeadFrames）
    /// → 高速钻入进门、全链渐隐吞没 → 门内滞留吊拍（双门脉动）→ 整链自出口门爆冲而出
    /// （堆叠后由跟链逐节拉出 = 鱼贯而出）→ 硬刹 → 出口门绕玩家 120° 轮转进下一循环。
    /// 公平口径：出口门可见 ≥42 帧且不再移动改向、爆冲向 = 门面朝向（锁定即承诺）、
    /// 伤害窗速度门控；隐身滞留期免伤（打不见的东西不公平，双向豁免）。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.PortalRush, typeof(FssStateContext))]
    internal class FssPortalRushState : FssStateBase
    {
        public override string StateName => "PortalRush";
        public override FssStateIndex StateIndex => FssStateIndex.PortalRush;

        private enum Phase { OpenGates, DiveIn, Inside, Burst, Brake }

        private Phase phase;
        private int phaseTimer;
        /// <summary>本循环自开门起的总帧数（出口门预告下限用）</summary>
        private int repTimer;
        private Vector2 entryPoint;
        private Vector2 exitPoint;
        private float exitFacing;
        private bool hiding;
        private int entryGateId = -1;
        private int exitGateId = -1;

        public override void OnEnter(FssStateContext ctx) {
            base.OnEnter(ctx);
            phase = Phase.OpenGates;
            phaseTimer = 0;
            repTimer = 0;
            hiding = false;
        }

        public override void OnExit(FssStateContext ctx) {
            //任何退出路径都收门（死亡/转阶段打断不留孤门）
            KillGate(ref entryGateId);
            KillGate(ref exitGateId);
        }

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;
            ctx.PortalPhase = true;

            switch (phase) {
                case Phase.OpenGates:
                    SetupGates(ctx, npc);
                    phase = Phase.DiveIn;
                    phaseTimer = 0;
                    hiding = false;
                    break;

                case Phase.DiveIn:
                    UpdateDiveIn(ctx, npc);
                    break;

                case Phase.Inside:
                    UpdateInside(ctx, npc);
                    break;

                case Phase.Burst:
                    ctx.Mode = FssMoveMode.Direct;
                    ctx.LegCommand = FssLegCommand.Tuck;
                    //复利加速：出门越冲越快（鱼贯拉出的链条被越扯越直）
                    npc.velocity *= 1.012f;
                    if (npc.velocity.Length() > FssDirector.SkimContactSpeed) {
                        npc.damage = npc.defDamage;
                    }
                    //撕咬意图：出门爆冲中玩家贴嘴即鳌足急伸合围
                    DeclareSnatchIfClose(ctx, npc, FssDirector.SkimContactSpeed);
                    if (phaseTimer >= FssDirector.PortalBurstFlightFrames) {
                        //收出口门（进入收拢窗）
                        KillGate(ref exitGateId);
                        phase = Phase.Brake;
                        phaseTimer = 0;
                    }
                    break;

                case Phase.Brake: {
                    ctx.Mode = FssMoveMode.Direct;
                    ctx.LegCommand = FssLegCommand.Flail;
                    npc.velocity *= 0.7f;
                    if (phaseTimer >= FssDirector.PortalBrakeFrames) {
                        Counter++;
                        if (Counter >= FssDirector.PortalReps(ctx.Phase) || ctx.Owner.TargetInvalid()) {
                            return EndAttack(ctx);
                        }
                        phase = Phase.OpenGates;
                        phaseTimer = 0;
                        repTimer = 0;
                    }
                    break;
                }
            }

            phaseTimer++;
            repTimer++;
            Timer++;

            //超时保险
            int repLen = FssDirector.PortalDiveMaxFrames + FssDirector.PortalOpenLeadFrames
                + FssDirector.PortalBurstFlightFrames + FssDirector.PortalBrakeFrames + 30;
            if (Timer > FssDirector.PortalReps(3) * repLen + 80) {
                npc.velocity *= 0.8f;
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>
        /// 开门：进门在蛇前方、出口门绕玩家轮转（各端同式推算；门实体只在权威端生成，
        /// 位置以实体同步为准，本地推算只喂转向与表现）
        /// </summary>
        private void SetupGates(FssStateContext ctx, NPC npc) {
            Vector2 forward = npc.velocity.SafeNormalize(
                (ctx.Target.Center - npc.Center).SafeNormalize(Vector2.UnitX));
            entryPoint = npc.Center + forward * FssDirector.PortalEntryOffset;

            //出口角：入招基角（whoAmI 确定性去相关）+ 循环 120° 轮转
            float baseAng = npc.whoAmI * 0.7f - MathHelper.PiOver2;
            float exitAng = baseAng + Counter * MathHelper.TwoPi / 3f;
            exitPoint = ctx.Target.Center + exitAng.ToRotationVector2() * FssDirector.PortalExitRadius;
            exitFacing = (ctx.Target.Center - exitPoint).ToRotation();

            if (!VaultUtils.isClient) {
                int type = ModContent.ProjectileType<FssIchorGate>();
                entryGateId = Projectile.NewProjectile(npc.GetSource_FromAI(), entryPoint, Vector2.Zero,
                    type, 0, 0f, Main.myPlayer, forward.ToRotation(), 0f);
                exitGateId = Projectile.NewProjectile(npc.GetSource_FromAI(), exitPoint, Vector2.Zero,
                    type, 0, 0f, Main.myPlayer, exitFacing, 1f);
            }
        }

        /// <summary>钻入进门：全速扑门 → 门口吞没（全链渐隐）→ 权威端整链搬运到出口</summary>
        private void UpdateDiveIn(FssStateContext ctx, NPC npc) {
            ctx.LegCommand = FssLegCommand.Tuck;

            if (!hiding) {
                ctx.Mode = FssMoveMode.Steer;
                ctx.MoveTarget = entryPoint;
                ctx.MoveSpeed = 30f;
                ctx.TurnSpeed = 3.2f;
                ctx.AccelRate = 0.13f;
                ctx.Slither = 0.3f;
                if (Vector2.Distance(npc.Center, entryPoint) < 70f
                    || phaseTimer > FssDirector.PortalDiveMaxFrames) {
                    hiding = true;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item86 with { Volume = 0.8f, Pitch = -0.3f, MaxInstances = 3 }, entryPoint);
                        FssVfx.IchorBurst(entryPoint, 1.2f);
                    }
                }
                return;
            }

            //吞没段：免伤（打不见的东西不公平）+ 全链快速渐隐
            npc.dontTakeDamage = true;
            ctx.Mode = FssMoveMode.Hold;
            ctx.PortalHiding = true;
            npc.alpha = Math.Min(npc.alpha + 40, 255);

            if (npc.alpha >= 250) {
                //整链搬运到出口（堆叠，爆冲时由跟链逐节拉出）；客户端等实体同步
                if (!VaultUtils.isClient) {
                    npc.Center = exitPoint;
                    npc.velocity = Vector2.Zero;
                    npc.netUpdate = true;
                    foreach (var seg in ctx.Segments) {
                        if (!seg.active) {
                            continue;
                        }
                        seg.Center = exitPoint;
                        seg.velocity = Vector2.Zero;
                        seg.netUpdate = true;
                    }
                    KillGate(ref entryGateId);
                }
                npc.rotation = exitFacing + FssHead.FacingRot;
                phase = Phase.Inside;
                phaseTimer = 0;
            }
        }

        /// <summary>门内滞留：吊一拍（双门脉动 + 微震），满足出口门预告下限后爆冲</summary>
        private void UpdateInside(FssStateContext ctx, NPC npc) {
            ctx.Mode = FssMoveMode.Hold;
            ctx.LegCommand = FssLegCommand.Tuck;
            ctx.PortalHiding = true;
            npc.dontTakeDamage = true;
            npc.alpha = 255;
            npc.velocity = Vector2.Zero;
            npc.rotation = exitFacing + FssHead.FacingRot;

            if (phaseTimer == 2 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.7f, Pitch = -0.6f, MaxInstances = 3 }, exitPoint);
                FssVfx.Shake(exitPoint, 2.5f, 1200f);
            }

            //爆冲门槛：滞留拍走完 且 出口门已可见满预告下限
            if (phaseTimer >= FssDirector.PortalInsideFrames
                && repTimer >= FssDirector.PortalOpenLeadFrames) {
                npc.alpha = 0;
                npc.velocity = exitFacing.ToRotationVector2()
                    * FssDirector.PortalBurstSpeed * ctx.RampSpeedScale;
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
                ctx.PulseWhip(11f);
                if (!Main.dedServ) {
                    FssVfx.Roar(exitPoint, -0.5f, 1.1f);
                    FssVfx.IchorBurst(exitPoint, 1.8f, exitFacing.ToRotationVector2());
                    FssVfx.Shake(exitPoint, 6f, 1500f);
                    SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.8f, Pitch = 0.1f, MaxInstances = 3 }, exitPoint);
                }
                phase = Phase.Burst;
                phaseTimer = 0;
            }
        }

        /// <summary>收门：进收拢窗（权威端；门自带 14 帧收拢演出）</summary>
        private static void KillGate(ref int gateId) {
            if (VaultUtils.isClient || gateId < 0 || gateId >= Main.maxProjectiles) {
                gateId = -1;
                return;
            }
            Projectile gate = Main.projectile[gateId];
            if (gate.active && gate.ModProjectile is FssIchorGate && gate.timeLeft > 16) {
                gate.timeLeft = 16;
                gate.netUpdate = true;
            }
            gateId = -1;
        }
    }
}

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
    /// A6 环卷瀑洗（P1 起，地形无关）：蠕虫腾空围玩家画大圈，绕圈中头部向心
    /// 呕吐灵液管流——圈内形成随蟒身转动的辐条雨，痰真正打向玩家所在的圈心带。
    /// P2 起收束时沿切线甩出一记短冲刺作标点，随后俯冲回地。
    /// 公平口径：喷 CoilFireFrames / 歇 CoilRestFrames 的占空循环（歇拍断流 +
    /// 吸气音 = 声明逃生拍，可切向绕行或出圈）、圈心只慢跟不追踪（圈几何稳定）、
    /// 喷向走提前角时间表不锁玩家、向心滴 14px/f 自环径到圈心约 33 帧反应窗。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.CoilCascade, typeof(FssStateContext))]
    internal class FssCoilCascadeState : FssStateBase
    {
        public override string StateName => "CoilCascade";
        public override FssStateIndex StateIndex => FssStateIndex.CoilCascade;

        private enum Phase { Entry, Circle, ExitDash, ExitDive }

        private Phase phase;
        private int phaseTimer;
        /// <summary>圈心（慢速跟随玩家）</summary>
        private Vector2 center;
        /// <summary>当前环位角</summary>
        private float angle;
        /// <summary>已转过的累计弧度</summary>
        private float sweptTotal;
        /// <summary>转向（±1，入招时按相对位取定）</summary>
        private float spinDir = 1f;
        private int dropIndex;
        private Vector2 exitDashDir;
        private bool exitLocked;

        public override void OnEnter(FssStateContext ctx) {
            base.OnEnter(ctx);
            phase = Phase.Entry;
            phaseTimer = 0;
            sweptTotal = 0f;
            dropIndex = 0;
            exitLocked = false;
            center = ctx.Target.Center;
            Vector2 rel = ctx.Npc.Center - center;
            angle = rel.ToRotation();
            //转向取"继续当前侧绕行"的方向：头在左侧顺时针、右侧逆时针（连续入圈不折返）
            spinDir = rel.X >= 0f ? 1f : -1f;
        }

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;

            //圈心慢跟（不追踪只慢跟：圈几何对玩家稳定可读）
            center = Vector2.Lerp(center, ctx.Target.Center, FssDirector.CoilCenterLerp);

            switch (phase) {
                case Phase.Entry: {
                    //赶往入圈点（当前角位的环上点）
                    ctx.Mode = FssMoveMode.Steer;
                    ctx.MoveTarget = center + angle.ToRotationVector2() * FssDirector.CoilRadius;
                    ctx.MoveSpeed = 27f;
                    ctx.TurnSpeed = 3f;
                    ctx.AccelRate = 0.12f;
                    ctx.Slither = 0.5f;
                    ctx.LegCommand = FssLegCommand.Flail;
                    if (Vector2.Distance(npc.Center, ctx.MoveTarget) < 110f
                        || phaseTimer > FssDirector.CoilEntryFrames) {
                        phase = Phase.Circle;
                        phaseTimer = 0;
                        if (!Main.dedServ) {
                            FssVfx.Roar(npc.Center, -0.55f, 0.85f);
                        }
                    }
                    break;
                }

                case Phase.Circle:
                    UpdateCircle(ctx, npc);
                    if (sweptTotal >= FssDirector.CoilLaps(ctx.Phase) * MathHelper.TwoPi) {
                        if (ctx.Phase >= 2) {
                            phase = Phase.ExitDash;
                            phaseTimer = 0;
                            exitLocked = false;
                        }
                        else {
                            phase = Phase.ExitDive;
                            phaseTimer = 0;
                        }
                    }
                    break;

                case Phase.ExitDash: {
                    //切线收束冲刺：预亮 8 帧锁切向（预告即承诺），一帧全速甩出
                    ctx.Mode = FssMoveMode.Direct;
                    ctx.LegCommand = FssLegCommand.Tuck;
                    if (!exitLocked) {
                        exitDashDir = (angle + MathHelper.PiOver2 * spinDir).ToRotationVector2();
                        ctx.CystGlow = 1f;
                        if (phaseTimer >= 8) {
                            exitLocked = true;
                            npc.velocity = exitDashDir * FssDirector.CoilExitDashSpeed * ctx.RampSpeedScale;
                            if (!VaultUtils.isClient) {
                                npc.netUpdate = true;
                            }
                            ctx.PulseWhip(10f);
                            if (!Main.dedServ) {
                                FssVfx.Roar(npc.Center, -0.7f, 0.8f);
                                FssVfx.Shake(npc.Center, 4.5f, 1300f);
                            }
                        }
                        else {
                            npc.velocity *= 0.86f;
                            npc.rotation = npc.rotation.AngleLerp(exitDashDir.ToRotation() + FssHead.FacingRot, 0.35f);
                        }
                    }
                    else {
                        //伤害窗=可见冲势
                        if (npc.velocity.Length() > FssDirector.SkimContactSpeed) {
                            npc.damage = npc.defDamage;
                        }
                        if (phaseTimer >= 8 + FssDirector.CoilExitFlightFrames) {
                            phase = Phase.ExitDive;
                            phaseTimer = 0;
                        }
                    }
                    break;
                }

                case Phase.ExitDive: {
                    //俯冲回地收招（平台/悬空场地下 46 帧兜底直接收）
                    ctx.Mode = FssMoveMode.Direct;
                    ctx.LegCommand = FssLegCommand.Flail;
                    npc.velocity.X *= 0.96f;
                    npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y + 0.7f, -8f, 22f);
                    float groundY = FssVfx.FindGroundY(npc.Center - new Vector2(0f, 60f));
                    if (npc.Center.Y >= groundY - FssDirector.CrawlRideHeight - 30f || phaseTimer > 46) {
                        npc.velocity.Y *= 0.3f;
                        return EndAttack(ctx);
                    }
                    break;
                }
            }

            phaseTimer++;
            Timer++;

            //超时保险：入圈 + 最大圈数 + 收束 + 缓冲
            int budget = FssDirector.CoilEntryFrames
                + (int)(FssDirector.CoilLaps(3) * MathHelper.TwoPi / FssDirector.CoilOmega(1))
                + 8 + FssDirector.CoilExitFlightFrames + 46 + 60;
            if (Timer > budget) {
                npc.velocity *= 0.8f;
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>绕圈 + 向心管流占空循环</summary>
        private void UpdateCircle(FssStateContext ctx, NPC npc) {
            float omega = FssDirector.CoilOmega(ctx.Phase);
            angle += omega * spinDir;
            sweptTotal += omega;

            //环上追点：目标点略超前于角位（蛇追着自己的轨道跑）
            float aheadAng = angle + omega * spinDir * 10f;
            ctx.Mode = FssMoveMode.Steer;
            ctx.MoveTarget = center + aheadAng.ToRotationVector2() * FssDirector.CoilRadius;
            ctx.MoveSpeed = omega * FssDirector.CoilRadius * 1.25f;
            ctx.TurnSpeed = 3.6f;
            ctx.AccelRate = 0.14f;
            ctx.Slither = 0.25f;
            ctx.LegCommand = FssLegCommand.Flail;

            //占空循环：喷窗吐向心管流，歇窗断流吸气（声明逃生拍）
            int duty = FssDirector.CoilFireFrames + FssDirector.CoilRestFrames;
            int cycleT = phaseTimer % duty;
            bool firing = cycleT < FssDirector.CoilFireFrames;

            if (firing) {
                //喷向：向心 + 沿转向提前角（辐条追转速的时间表，不锁玩家）
                Vector2 mouth = MouthPos(npc);
                Vector2 inward = (center - npc.Center).SafeNormalize(Vector2.UnitX);
                Vector2 aim = inward.RotatedBy(FssDirector.CoilLeadAngle * spinDir);
                ctx.AimAngle = aim.ToRotation();
                ctx.CystGlow = Math.Max(ctx.CystGlow, 0.85f);
                ctx.SwallowSuction = Math.Max(ctx.SwallowSuction, 0.4f);

                if (!Main.dedServ) {
                    if (cycleT % 9 == 0) {
                        SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.5f, Pitch = -0.35f, MaxInstances = 5 }, mouth);
                    }
                    if (Main.rand.NextBool(2)) {
                        FssVfx.IchorBurst(mouth, 0.35f, aim);
                    }
                }
                if (cycleT % 12 == 0) {
                    ctx.PulseWhip(3.5f);
                }

                //痰滴链（权威端）：向心射流，少量留池
                if (!VaultUtils.isClient && cycleT % FssDirector.CoilDropGap == 0) {
                    dropIndex++;
                    int damage = FssDirector.ScaleProjectileDamage(npc, FssDirector.CascadeDamage);
                    float poolFlag = dropIndex % FssDirector.CoilPoolEvery == 0 ? 1f : 0f;
                    Vector2 vel = aim * FssDirector.CoilDropSpeed * ctx.RampSpeedScale
                        + npc.velocity * 0.15f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), mouth, vel,
                        ModContent.ProjectileType<FssCascadeDrop>(), damage, 0.4f, Main.myPlayer, poolFlag);
                }
            }
            else if (cycleT == FssDirector.CoilFireFrames && !Main.dedServ) {
                //断流吸气（逃生拍的听觉边界）
                SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.55f, Pitch = -0.5f, MaxInstances = 3 }, npc.Center);
            }
        }
    }
}

using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>
    /// 回环沙瀑（P2 起）：破空而起，在玩家侧上方画一个完整的正圆，环身按节拍泻沙成
    /// 下落幕帘，画满一圈后沿环滑行找切点，切向对准玩家即甩出俯冲（离心出手，物理自然）。
    /// 与天游的差异：天游是漫游点射，这招是一个几何图形承诺 + 区域幕帘 + 爆点俯冲。
    /// 公平阀：环心入环帧锁定不追玩家（离开环下即安全）；泻沙非追踪、节拍疏密即逃生缝；
    /// 俯冲预告 14 帧亮头吼声、锁点即承诺；切点对准即早退出手，不磨自己的时钟。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.LoopCascade, typeof(BssStateContext))]
    internal class BssLoopCascadeState : BssStateBase
    {
        public override string StateName => "LoopCascade";
        public override BssStateIndex StateIndex => BssStateIndex.LoopCascade;

        private enum LoopPhase
        {
            Crouch,   //蹬地蓄势
            Ascend,   //腾空去入环点
            Loop,     //画环泻沙
            Align,    //沿环滑行找切点（泻沙停 = 出手前静默）
            Telegraph,//俯冲预告
            Dive,     //离心俯冲
        }

        private const int CrouchFrames = 10;

        private LoopPhase phase;
        /// <summary>环心（入环帧锁定，整招不追玩家）</summary>
        private Vector2 ringCenter;
        private float loopAngle;
        private float loopSign = 1f;
        /// <summary>已扫过的弧度（画满 2π 收环）</summary>
        private float sweptAngle;
        /// <summary>俯冲锁点（预告起始帧锁定，不追瞄）</summary>
        private Vector2 diveLock;
        private int cascadeTimer;
        private bool landed;
        private float landTime;
        private float prevY;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            phase = LoopPhase.Crouch;
            sweptAngle = 0f;
            cascadeTimer = 0;
            landed = false;
            prevY = ctx.Npc.Center.Y;
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case LoopPhase.Crouch:
                    UpdateCrouch(ctx, npc);
                    break;
                case LoopPhase.Ascend:
                    UpdateAscend(ctx, npc);
                    break;
                case LoopPhase.Loop:
                    UpdateLoop(ctx, npc);
                    break;
                case LoopPhase.Align:
                    UpdateAlign(ctx, npc);
                    break;
                case LoopPhase.Telegraph:
                    UpdateTelegraph(ctx, npc);
                    break;
                case LoopPhase.Dive: {
                    IBssState next = UpdateDive(ctx, npc);
                    if (next != null) {
                        return next;
                    }
                    break;
                }
            }

            prevY = npc.Center.Y;

            //超时保险兜底
            if (Counter++ > 60 * 10) {
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(LoopPhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>蹬地蓄势：前身压低（弹簧先压才有劲）</summary>
        private void UpdateCrouch(BssStateContext ctx, NPC npc) {
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlSpeed = 4f;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.FrontRaise = 0.3f;

            Timer++;
            if (Timer >= CrouchFrames) {
                //破空 + 锁环心（入环帧的玩家位置定环，此后不追）
                float side = Math.Sign(npc.Center.X - ctx.Target.Center.X);
                if (side == 0f) {
                    side = 1f;
                }
                ringCenter = ctx.Target.Center
                    + new Vector2(side * BssDirector.LoopCenterSide, -BssDirector.LoopCenterLift);
                if (!VaultUtils.isClient) {
                    float dir = FacingToTarget(ctx, 0f);
                    npc.velocity = new Vector2(dir * 9f, -25f);
                    npc.netUpdate = true;
                }
                ctx.PulseWhip(10f);
                if (!Main.dedServ) {
                    BssVfx.SandBurst(npc.Bottom, 1.1f);
                    BssVfx.Roar(npc.Center, -0.2f, 0.8f);
                }
                SwitchPhase(LoopPhase.Ascend);
            }
        }

        /// <summary>腾空：Steer 到环周入口，提前到位即早退入环</summary>
        private void UpdateAscend(BssStateContext ctx, NPC npc) {
            Vector2 entryPoint = ringCenter
                + (npc.Center - ringCenter).SafeNormalize(-Vector2.UnitY) * BssDirector.LoopRadius;
            ctx.Mode = BssMoveMode.Steer;
            ctx.MoveTarget = entryPoint;
            ctx.MoveSpeed = 26f;
            ctx.TurnSpeed = 3.2f;
            ctx.AccelRate = 0.13f;
            ctx.Slither = 0.25f;
            ctx.LegCommand = BssLegCommand.Flail;

            Timer++;
            if (Vector2.Distance(npc.Center, entryPoint) < 110f
                || Timer >= BssDirector.LoopEntryFrames) {
                loopAngle = (npc.Center - ringCenter).ToRotation();
                //环向取速度延续侧（切向连续 = 入环不打折）
                Vector2 radial = npc.Center - ringCenter;
                float cross = radial.X * npc.velocity.Y - radial.Y * npc.velocity.X;
                loopSign = cross >= 0f ? 1f : -1f;
                SwitchPhase(LoopPhase.Loop);
            }
        }

        /// <summary>画环泻沙：追圆周行进点，链条自然成环；环身按节拍垂落沙球幕帘</summary>
        private void UpdateLoop(BssStateContext ctx, NPC npc) {
            float omega = MathHelper.TwoPi / BssDirector.LoopLapFrames;
            loopAngle += omega * loopSign;
            sweptAngle += omega;
            SteerOnRing(ctx, npc, omega);

            //环身即墙：高速段开伤害窗（环在天上，地面玩家吃不到）
            if (npc.velocity.Length() > 12f) {
                npc.damage = npc.defDamage;
            }

            //泻沙：微量继承切向速度 + 重力主导 = 从环上垂落（非追踪）
            if (++cascadeTimer >= BssDirector.LoopCascadeGap) {
                cascadeTimer = 0;
                if (!Main.dedServ) {
                    BssVfx.SandTrickle(npc.Center, 1.4f);
                }
                if (!VaultUtils.isClient) {
                    int damage = BssDirector.ScaleProjectileDamage(npc, BssDirector.SandGlobDamage);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                        npc.velocity * 0.22f + new Vector2(0f, 1.5f),
                        ModContent.ProjectileType<BssSandGlob>(), damage, 0.5f, Main.myPlayer);
                }
            }

            Timer++;
            if (sweptAngle >= MathHelper.TwoPi) {
                SwitchPhase(LoopPhase.Align);
            }
        }

        /// <summary>沿环找切点：泻沙已停（出手前静默），切向对准玩家即早退锁点</summary>
        private void UpdateAlign(BssStateContext ctx, NPC npc) {
            float omega = MathHelper.TwoPi / BssDirector.LoopLapFrames;
            loopAngle += omega * loopSign;
            SteerOnRing(ctx, npc, omega);

            if (npc.velocity.Length() > 12f) {
                npc.damage = npc.defDamage;
            }

            Vector2 tangent = (loopAngle + loopSign * MathHelper.PiOver2).ToRotationVector2();
            Vector2 toPlayer = (PredictTarget(ctx, 14f) - npc.Center).SafeNormalize(Vector2.UnitY);
            float error = MathF.Acos(MathHelper.Clamp(Vector2.Dot(tangent, toPlayer), -1f, 1f));

            Timer++;
            if (error < 0.16f || Timer >= BssDirector.LoopAlignFrames) {
                diveLock = PredictTarget(ctx, 14f);
                if (!Main.dedServ) {
                    BssVfx.Roar(npc.Center, -0.5f, 1f);
                }
                SwitchPhase(LoopPhase.Telegraph);
            }
        }

        /// <summary>俯冲预告：转速减半（吸气拍）+ 亮头，锁点不再更新</summary>
        private void UpdateTelegraph(BssStateContext ctx, NPC npc) {
            float omega = MathHelper.TwoPi / BssDirector.LoopLapFrames * 0.5f;
            loopAngle += omega * loopSign;
            SteerOnRing(ctx, npc, omega);
            ctx.BloomGlow = Math.Max(ctx.BloomGlow, 1f);

            Timer++;
            if (Timer >= BssDirector.LoopDiveTelegraph) {
                if (!VaultUtils.isClient) {
                    npc.velocity = (diveLock - npc.Center).SafeNormalize(Vector2.UnitY)
                        * BssDirector.LoopDiveSpeed;
                    npc.netUpdate = true;
                }
                ctx.PulseWhip(12f);
                if (!Main.dedServ) {
                    BssVfx.Shake(npc.Center, 5f, 1300f);
                }
                SwitchPhase(LoopPhase.Dive);
            }
        }

        /// <summary>离心俯冲：直线承诺，砸地喷发转爬行收招</summary>
        private IBssState UpdateDive(BssStateContext ctx, NPC npc) {
            ctx.Mode = BssMoveMode.Direct;
            ctx.LegCommand = BssLegCommand.Tuck;
            if (npc.velocity.Length() > BssDirector.LungeContactSpeed) {
                npc.damage = npc.defDamage;
            }

            float groundY = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 300f), 900f);
            if (!landed && prevY < groundY && npc.Center.Y >= groundY - 10f) {
                landed = true;
                landTime = Timer;
                ctx.PulseWhip(9f);
                if (!Main.dedServ) {
                    BssVfx.SandBurst(new Vector2(npc.Center.X, groundY), 1.7f);
                    BssVfx.Roar(npc.Center, -0.45f, 1f);
                    BssVfx.Shake(npc.Center, 7f);
                }
                BssVfx.BreachEruption(npc, new Vector2(npc.Center.X, groundY), 7);
            }

            Timer++;
            if (landed && Timer - landTime > 10f) {
                npc.velocity *= 0.5f;
                //P3 落地即回马掠冲：砸地喷发接贴地爆冲，一口气两记
                if (ctx.Phase >= 3) {
                    ctx.QueuedChainState = (int)BssStateIndex.SandDash;
                }
                return EndAttack(ctx);
            }
            //俯冲落空（斜坡/深谷）：飞满一段自然收招
            if (Timer > 70f) {
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>环上驱动共件：追圆周行进点</summary>
        private void SteerOnRing(BssStateContext ctx, NPC npc, float omega) {
            Vector2 ringPoint = ringCenter + loopAngle.ToRotationVector2() * BssDirector.LoopRadius;
            ctx.Mode = BssMoveMode.Steer;
            ctx.MoveTarget = ringPoint;
            ctx.MoveSpeed = omega * BssDirector.LoopRadius * 1.35f + 6f;
            ctx.TurnSpeed = 3.6f;
            ctx.AccelRate = 0.17f;
            ctx.LegCommand = BssLegCommand.Flail;
        }
    }
}

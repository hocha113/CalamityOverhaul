using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>
    /// 破土突袭：潜沙 → 沙丘隆起预告（实体，生成即锁点 = 预告即承诺）→ 直线爆冲跃出 → 回潜循环。
    /// 公平阀：出土直线不再改向；伤害窗 = 速度门槛（可见冲势才咬人）；每循环重新预告。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.BurrowLunge, typeof(BssStateContext))]
    internal class BssBurrowLungeState : BssStateBase
    {
        public override string StateName => "BurrowLunge";
        public override BssStateIndex StateIndex => BssStateIndex.BurrowLunge;

        private const int DiveFrame = 8;
        private const int OmenFrame = 26;
        private int LungeFrame => OmenFrame + BssDirector.BreachTelegraphFrames;

        /// <summary>锁定的破土点（权威端裁决，钻行与出土只认它）</summary>
        private Vector2 lockPoint;
        /// <summary>预告帧锁定的玩家原位（斜刺式的穿刺目标，出土不再追瞄）</summary>
        private Vector2 lockTarget;
        private bool lockDone;
        /// <summary>本循环破土式：0 直上顶袭 / 1 侧翼斜刺 / 2 过顶长跃（权威端掷骰）</summary>
        private int breachStyle;
        /// <summary>已完成的突袭循环</summary>
        private int cycles;
        /// <summary>收尾段起始帧（-1 未进入）</summary>
        private float finishFrom = -1f;

        private float prevY;
        private bool breachFxDone;
        private bool diveFxDone;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            cycles = 0;
            lockDone = false;
            breachStyle = 0;
            finishFrom = -1f;
            breachFxDone = false;
            diveFxDone = false;
            prevY = ctx.Npc.Center.Y;
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;
            int maxCycles = BssDirector.LungeCycles(ctx.Phase);

            //收尾段：钻回玩家侧沙面，交还爬行
            if (finishFrom >= 0f) {
                return UpdateFinish(ctx, npc, t);
            }

            if (t < DiveFrame) {
                //前摇：带着冲势下探（腿收拢是入土信号，不急停）
                ctx.Mode = BssMoveMode.Crawl;
                ctx.CrawlSpeed = 9f;
                ctx.CrawlDirX = FacingToTarget(ctx);
                if (t > 3) {
                    ctx.LegCommand = BssLegCommand.Tuck;
                    ctx.LegAlpha = MathHelper.Clamp(1f - (t - 3) / 5f, 0f, 1f);
                }
            }
            else if (t == DiveFrame) {
                //入土：一帧定初速扎下去
                if (!VaultUtils.isClient) {
                    float dir = Math.Sign(ctx.Target.Center.X - npc.Center.X);
                    if (dir == 0f) {
                        dir = 1f;
                    }
                    npc.velocity = new Vector2(dir * 6f, 17f);
                    npc.netUpdate = true;
                }
                ctx.Mode = BssMoveMode.Direct;
                ctx.LegAlpha = 0f;
                ctx.LegCommand = BssLegCommand.Tuck;
            }
            else if (t < OmenFrame) {
                //地下接近：奔玩家脚下深处
                ctx.LegAlpha = 0f;
                ctx.LegCommand = BssLegCommand.Tuck;
                ctx.Mode = BssMoveMode.Steer;
                ctx.MoveTarget = ctx.Target.Center + new Vector2(0f, 320f);
                ctx.MoveSpeed = BssDirector.LungeDigSpeed;
                ctx.TurnSpeed = 2.6f;
                ctx.AccelRate = 0.12f;
            }
            else if (t == OmenFrame) {
                //锁点 + 预告实体（生成位置即承诺，出土不再追瞄）
                ctx.LegAlpha = 0f;
                ctx.LegCommand = BssLegCommand.Tuck;
                if (!VaultUtils.isClient && ctx.Target.Alives()) {
                    //三式破土掷骰：0 直上顶袭 / 1 侧翼斜刺 / 2 过顶长跃（动作经位置同步落到各端）
                    breachStyle = Main.rand.Next(3);
                    Vector2 predicted = ctx.Target.Center + ctx.Target.velocity * 16f;
                    lockTarget = predicted;
                    float breachX = predicted.X;
                    if (breachStyle == 1) {
                        //斜刺：从玩家侧翼破土，隆起点在侧面 = 方向预告
                        float side = Math.Sign(npc.Center.X - ctx.Target.Center.X);
                        if (side == 0f) {
                            side = 1f;
                        }
                        breachX = predicted.X + side * 300f;
                    }
                    float groundY = BssVfx.FindGroundY(new Vector2(breachX, ctx.Target.Center.Y - 240f));
                    lockPoint = new Vector2(breachX, groundY);
                    lockDone = true;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), lockPoint - new Vector2(0f, 4f),
                        Vector2.Zero, ModContent.ProjectileType<BssBreachOmen>(), 0, 0f, Main.myPlayer,
                        BssDirector.BreachTelegraphFrames);
                    npc.netUpdate = true;
                }
                ctx.Mode = BssMoveMode.Hold;
            }
            else if (t < LungeFrame) {
                //预告期：地下就位到锁点正下方蓄势
                ctx.LegAlpha = 0f;
                ctx.LegCommand = BssLegCommand.Tuck;
                ctx.Mode = BssMoveMode.Steer;
                ctx.MoveTarget = (lockDone ? lockPoint : ctx.Target.Center) + new Vector2(0f, 300f);
                ctx.MoveSpeed = 15f;
                ctx.TurnSpeed = 3f;
                ctx.AccelRate = 0.14f;
                //临出土收油蓄势（吸气拍）
                if (t > LungeFrame - 10) {
                    npc.velocity *= 0.8f;
                }
            }
            else if (t == LungeFrame) {
                //出土爆冲：一帧定初速，按三式给向，全部直线承诺不追瞄
                if (!VaultUtils.isClient) {
                    Vector2 vel;
                    switch (breachStyle) {
                        case 1: {
                            //侧翼斜刺：斜穿预告帧锁定的玩家原位
                            Vector2 aim = (lockTarget + new Vector2(0f, -50f) - npc.Center)
                                .SafeNormalize(-Vector2.UnitY);
                            if (aim.Y > -0.35f) {
                                aim.Y = -0.35f;
                                aim = aim.SafeNormalize(-Vector2.UnitY);
                            }
                            vel = aim * 36f;
                            break;
                        }
                        case 2: {
                            //过顶长跃：冲天带横向惯性，从头顶跃过砸向另一侧
                            float side = Math.Sign(lockTarget.X - npc.Center.X);
                            if (side == 0f) {
                                side = 1f;
                            }
                            vel = new Vector2(side * 11f, -BssDirector.BreachLaunchSpeed);
                            break;
                        }
                        default: {
                            //直上顶袭
                            float dx = MathHelper.Clamp((lockPoint.X - npc.Center.X) * 0.02f, -4f, 4f);
                            vel = new Vector2(dx, -BssDirector.BreachLaunchSpeed);
                            break;
                        }
                    }
                    npc.velocity = vel;
                    npc.netUpdate = true;
                }
                ctx.Mode = BssMoveMode.Direct;
                ctx.LegCommand = BssLegCommand.Flail;
                ctx.LegAlpha = 0.85f;
            }
            else {
                //腾空段：抛物，速度门槛开伤害窗（可见冲势=咬合窗）
                ctx.Mode = BssMoveMode.Direct;
                ctx.LegCommand = BssLegCommand.Flail;
                ctx.LegAlpha = 0.85f;
                npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y + BssDirector.LungeGravity, -30f, 19f);

                //回潜检测：下坠中扎回地表以下
                if (npc.velocity.Y > 2f) {
                    float groundY = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 340f), 1000f);
                    if (npc.Center.Y > groundY + 40f) {
                        cycles++;
                        breachFxDone = false;
                        if (cycles >= maxCycles) {
                            finishFrom = Timer;
                        }
                        else {
                            //早退计时器：直接跳回预告拍，不磨蹭
                            Timer = OmenFrame - 1;
                            lockDone = false;
                        }
                    }
                }
            }

            //伤害窗：只在腾空高速段咬人
            float speed = npc.velocity.Length();
            if (t > LungeFrame && speed > BssDirector.LungeContactSpeed) {
                npc.damage = npc.defDamage;
            }

            UpdateCrossFx(ctx, npc, t);
            Timer++;

            //超时保险
            if (Timer > 60 * 9) {
                ctx.AttackCooldown = BssDirector.AttackCooldown(ctx.Phase);
                return new BssHubState();
            }
            return null;
        }

        /// <summary>收尾：钻到玩家近侧浅层，抬头出面立刻交还压迫（不远遁、不磨蹭）</summary>
        private IBssState UpdateFinish(BssStateContext ctx, NPC npc, int t) {
            ctx.LegAlpha = MathHelper.Clamp(ctx.LegAlpha + 0.06f, 0f, 1f);
            ctx.LegCommand = BssLegCommand.Tuck;
            ctx.Mode = BssMoveMode.Steer;
            float side = Math.Sign(npc.Center.X - ctx.Target.Center.X);
            if (side == 0f) {
                side = 1f;
            }
            float exitX = ctx.Target.Center.X + side * 250f;
            float groundY = BssVfx.FindGroundY(new Vector2(exitX, ctx.Target.Center.Y - 240f));
            ctx.MoveTarget = new Vector2(exitX, groundY - BssDirector.CrawlRideHeight);
            ctx.MoveSpeed = 22f;
            ctx.TurnSpeed = 3f;
            ctx.AccelRate = 0.12f;

            UpdateCrossFx(ctx, npc, t);
            Timer++;

            bool arrived = Vector2.Distance(npc.Center, ctx.MoveTarget) < 90f;
            if (arrived || Timer - finishFrom > 80f) {
                npc.velocity *= 0.5f;
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>
        /// 穿面检测：各端本地按位置捕捉破土/入土瞬间。
        /// 表现（沙爆/吼/震/鞭波）走 !dedServ；破土喷发沙弹扇由权威端在同一瞬间生成。
        /// </summary>
        private void UpdateCrossFx(BssStateContext ctx, NPC npc, int t) {
            float groundY = BssVfx.FindGroundY(npc.Center - new Vector2(0f, 340f), 1000f);

            //入土
            if (!diveFxDone && t >= DiveFrame && prevY < groundY && npc.Center.Y >= groundY - 10f) {
                diveFxDone = true;
                ctx.PulseWhip(8f);
                if (!Main.dedServ) {
                    BssVfx.SandBurst(new Vector2(npc.Center.X, groundY), 1.2f);
                    BssVfx.Shake(npc.Center, 3.5f);
                }
            }
            //出土
            if (!breachFxDone && npc.velocity.Y < -8f && prevY > groundY && npc.Center.Y <= groundY + 24f) {
                breachFxDone = true;
                diveFxDone = false;
                ctx.PulseWhip(12f);
                if (!Main.dedServ) {
                    BssVfx.SandBurst(new Vector2(npc.Center.X, groundY), 1.7f);
                    BssVfx.Roar(npc.Center, -0.45f, 1f);
                    BssVfx.Shake(npc.Center, 8f);
                }
                //破土喷发沙弹扇（P1 减量到 5 颗）
                BssVfx.BreachEruption(npc, new Vector2(npc.Center.X, groundY),
                    ctx.Phase >= 2 ? BssDirector.BreachEruptGlobs : 5);
            }
            prevY = npc.Center.Y;
        }
    }
}

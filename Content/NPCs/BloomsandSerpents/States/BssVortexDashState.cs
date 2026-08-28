using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>
    /// 沙爆漩涡冲刺（P2 起）：脱离玩家到侧上锚点 → 绕自身锚点高速盘旋收紧、涡心搓出
    /// 沙爆漩涡（实体蓄力 VortexSpinFrames 帧）→ 锁向塌缩（涡缩小、粒子静默、蛇急刹再瞄）→
    /// 一帧 VortexDashSpeed 弃涡爆冲 → 出手 VortexDetonateDelay 帧后漩涡在身后自爆放沙球环。
    /// 公平阀：漩涡蓄力全程可见（锚点在玩家侧上 500px 屏内）；冲刺锁向提前 DashLockLead
    /// 帧死向（预告即承诺）；后爆沙球从固定点径向出、重力弧线；冲刺与沙雨两个威胁
    /// 时间错开（先躲冲刺再看落点）。盘旋期蛇是盘紧的大靶子 = 玩家的输出窗口。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.VortexDash, typeof(BssStateContext))]
    internal class BssVortexDashState : BssStateBase
    {
        public override string StateName => "VortexDash";
        public override BssStateIndex StateIndex => BssStateIndex.VortexDash;

        private enum VortexPhase
        {
            Entry,    //脱离玩家去锚点
            Spin,     //盘旋搓涡（漩涡蓄力）
            Collapse, //锁向塌缩（急刹再瞄，涡静默变小）
            Flight,   //弃涡爆冲
            Brake,    //硬刹
        }

        private const int BrakeFrames = 10;

        private VortexPhase phase;
        /// <summary>涡心锚点（进入时算，漩涡实体出现后各端对齐到实体位置）</summary>
        private Vector2 anchor;
        private float orbitAngle;
        private float orbitSign = 1f;
        /// <summary>锁定射向（锁向帧后不再更新 = 预告即承诺）</summary>
        private Vector2 lockedDir = Vector2.UnitX;

        public override void OnEnter(BssStateContext ctx) {
            base.OnEnter(ctx);
            phase = VortexPhase.Entry;

            //锚点：玩家侧上方（取玩家与地面较高者再上抬，飞天/站地都在屏内）
            Player target = ctx.Target;
            float side = Math.Sign(ctx.Npc.Center.X - target.Center.X);
            if (side == 0f) {
                side = 1f;
            }
            float anchorX = target.Center.X + side * BssDirector.VortexAnchorSide;
            float groundY = BssVfx.FindGroundY(new Vector2(anchorX, target.Center.Y - 240f));
            anchor = new Vector2(anchorX,
                Math.Min(target.Center.Y, groundY) - BssDirector.VortexAnchorLift);
            orbitSign = ctx.Npc.Center.X < anchor.X ? 1f : -1f;

            if (!Main.dedServ) {
                BssVfx.Roar(ctx.Npc.Center, -0.15f, 0.75f);
            }
        }

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;

            switch (phase) {
                case VortexPhase.Entry:
                    UpdateEntry(ctx, npc);
                    break;
                case VortexPhase.Spin:
                    UpdateSpin(ctx, npc);
                    break;
                case VortexPhase.Collapse:
                    UpdateCollapse(ctx, npc);
                    break;
                case VortexPhase.Flight:
                    UpdateFlight(ctx, npc);
                    break;
                case VortexPhase.Brake:
                    ctx.Mode = BssMoveMode.Direct;
                    ctx.LegCommand = BssLegCommand.March;
                    npc.velocity *= 0.66f;
                    if (npc.velocity.Length() > BssDirector.DashContactSpeed) {
                        npc.damage = npc.defDamage;
                    }
                    Timer++;
                    if (Timer >= BrakeFrames) {
                        npc.velocity *= 0.6f;
                        return EndAttack(ctx);
                    }
                    break;
            }

            //超时保险兜底（漩涡弹幕的孤儿保险会跟着状态退出消散）
            if (Counter++ > 60 * 8) {
                return EndAttack(ctx);
            }
            return null;
        }

        private void SwitchPhase(VortexPhase next) {
            phase = next;
            Timer = 0;
        }

        /// <summary>就位：Steer 到锚点圆周入口，提前到位即早退入盘</summary>
        private void UpdateEntry(BssStateContext ctx, NPC npc) {
            Vector2 entryPoint = anchor
                + (npc.Center - anchor).SafeNormalize(Vector2.UnitX) * BssDirector.VortexRadiusStart;
            ctx.Mode = BssMoveMode.Steer;
            ctx.MoveTarget = entryPoint;
            ctx.MoveSpeed = 24f;
            ctx.TurnSpeed = 3f;
            ctx.AccelRate = 0.12f;
            ctx.Slither = 0.3f;
            ctx.LegCommand = BssLegCommand.Flail;

            Timer++;
            if (Vector2.Distance(npc.Center, entryPoint) < 110f
                || Timer >= BssDirector.VortexEntryFrames) {
                orbitAngle = (npc.Center - anchor).ToRotation();
                //漩涡实体：生成位置即涡心承诺（各端此后对齐到实体）
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), anchor, Vector2.Zero,
                        ModContent.ProjectileType<BssSandVortexProj>(), 0, 0f, Main.myPlayer,
                        BssDirector.VortexSpinFrames, npc.whoAmI, ctx.Phase);
                }
                SwitchPhase(VortexPhase.Spin);
            }
        }

        /// <summary>盘旋搓涡：追绕锚点旋转的圆点，半径收紧角速递增（越搓越快）</summary>
        private void UpdateSpin(BssStateContext ctx, NPC npc) {
            //各端把锚点对齐到实际漩涡实体（联机硬同步）
            AlignAnchorToVortex(npc);

            float progress = MathHelper.Clamp(Timer / BssDirector.VortexSpinFrames, 0f, 1f);
            float omega = MathHelper.Lerp(BssDirector.VortexOmegaStart, BssDirector.VortexOmegaEnd, progress);
            float radius = MathHelper.Lerp(BssDirector.VortexRadiusStart, BssDirector.VortexRadiusEnd, progress);
            orbitAngle += omega * orbitSign;
            Vector2 orbitPoint = anchor + orbitAngle.ToRotationVector2() * radius;

            ctx.Mode = BssMoveMode.Steer;
            ctx.MoveTarget = orbitPoint;
            ctx.MoveSpeed = omega * radius * 1.35f + 6f;
            //转向上限须覆盖末段角速 0.19：5.6/20×0.72≈0.20（低了头会锁不住紧圈外甩）
            ctx.TurnSpeed = 5.6f;
            ctx.AccelRate = 0.2f;
            ctx.LegCommand = BssLegCommand.Flail;
            ctx.Compression = Math.Min(ctx.Compression,
                MathHelper.Lerp(1f, 0.88f, MathHelper.Clamp(progress * 2f, 0f, 1f)));

            //环身即墙：高速段开伤害窗（锚点远离玩家 500px，主动贴上来才吃）
            if (npc.velocity.Length() > 12f) {
                npc.damage = npc.defDamage;
            }
            //末段亮花：出手前的身体预告
            if (progress > 0.75f) {
                ctx.BloomGlow = Math.Max(ctx.BloomGlow, (progress - 0.75f) * 4f);
            }

            Timer++;
            if (Timer >= BssDirector.VortexSpinFrames) {
                if (!Main.dedServ) {
                    BssVfx.Roar(npc.Center, -0.55f, 1f);
                }
                SwitchPhase(VortexPhase.Collapse);
            }
        }

        /// <summary>锁向塌缩：脱轨急刹再瞄，末段死向；涡在同拍静默变小（各自吸气）</summary>
        private void UpdateCollapse(BssStateContext ctx, NPC npc) {
            int t = (int)Timer;
            float progress = MathHelper.Clamp(t / (float)BssDirector.VortexCollapseFrames, 0f, 1f);

            ctx.Mode = BssMoveMode.Direct;
            ctx.LegCommand = BssLegCommand.Tuck;
            npc.velocity *= 0.8f;

            //锁向拍之前追瞄（全向自由 = 与贴地掠冲的身份区分），之后死向
            if (t <= BssDirector.VortexCollapseFrames - BssDirector.DashLockLead) {
                Vector2 predicted = PredictTarget(ctx, 12f);
                lockedDir = (predicted - npc.Center).SafeNormalize(Vector2.UnitX);
            }
            npc.rotation = npc.rotation.AngleLerp(lockedDir.ToRotation() + BssHead.FacingRot, 0.3f);
            ctx.BloomGlow = Math.Max(ctx.BloomGlow, 1f);
            ctx.Compression = Math.Min(ctx.Compression, 0.86f);

            //末段绷紧颤抖
            if (progress > 0.5f && !Main.dedServ) {
                npc.position += Main.rand.NextVector2Circular(1.5f, 1.5f);
            }

            Timer++;
            if (t >= BssDirector.VortexCollapseFrames) {
                Launch(ctx, npc);
                SwitchPhase(VortexPhase.Flight);
            }
        }

        /// <summary>弃涡爆冲：一帧定速 50px/f，力量在出手帧；漩涡按自己的定时器在身后爆</summary>
        private void Launch(BssStateContext ctx, NPC npc) {
            if (!VaultUtils.isClient) {
                npc.velocity = lockedDir * BssDirector.VortexDashSpeed;
                npc.netUpdate = true;
            }
            ctx.PulseWhip(12f);
            if (!Main.dedServ) {
                BssVfx.SandBurst(npc.Center, 1.4f);
                BssVfx.Roar(npc.Center, -0.35f, 1f);
                BssVfx.Shake(npc.Center, 7f, 1300f);
            }
        }

        /// <summary>爆冲飞行：直线承诺不转向，速度门槛开伤害窗</summary>
        private void UpdateFlight(BssStateContext ctx, NPC npc) {
            ctx.Mode = BssMoveMode.Direct;
            ctx.LegCommand = BssLegCommand.Tuck;

            if (npc.velocity.Length() > BssDirector.DashContactSpeed) {
                npc.damage = npc.defDamage;
            }

            //冲刺尾流（各端本地）
            if (!Main.dedServ && Main.GameUpdateCount % 2 == 0) {
                Dust d = Dust.NewDustPerfect(npc.Center - npc.velocity * 0.4f
                    + Main.rand.NextVector2Circular(14f, 14f), Terraria.ID.DustID.Sand,
                    -npc.velocity * 0.08f, 110, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = true;
            }

            Timer++;
            if (Timer >= BssDirector.VortexFlightFrames) {
                SwitchPhase(VortexPhase.Brake);
            }
        }

        /// <summary>找到自家漩涡实体并把锚点对齐过去（各端硬同步涡心）</summary>
        private void AlignAnchorToVortex(NPC npc) {
            int type = ModContent.ProjectileType<BssSandVortexProj>();
            foreach (var p in Main.ActiveProjectiles) {
                if (p.type == type && (int)p.ai[1] == npc.whoAmI) {
                    anchor = p.Center;
                    return;
                }
            }
        }
    }
}

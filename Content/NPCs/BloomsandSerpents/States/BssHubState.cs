using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>
    /// 巡曳 hub：在玩家两侧折返点之间中速爬行（整条蛇在屏内爬来爬去，八腿步态与
    /// 鳌足探路就是这段的看点），喘息拍走完按手写轮换表出招。
    /// 轮换表各端一致、只有权威端的返回被采纳。核心动作是喷沙与带预警线的冲刺/扑击，
    /// 体节发射器与刺球作区域变化。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BssStateIndex.Hub, typeof(BssStateContext))]
    internal class BssHubState : BssStateBase
    {
        public override string StateName => "Hub";
        public override BssStateIndex StateIndex => BssStateIndex.Hub;

        public override IBssState OnUpdate(BssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            float dist = Vector2.Distance(npc.Center, ctx.Target.Center);
            UpdatePatrol(ctx, npc);

            //爬行掠沙的底噪
            if (!Main.dedServ && Main.rand.NextBool(9) && Math.Abs(npc.velocity.X) > 3f) {
                BssVfx.SandTrickle(npc.Bottom + new Vector2(Main.rand.NextFloat(-30f, 30f), 0f), 0.8f);
            }

            //骚扰甩刺：巡曳中红花节也在咬（预亮 = 预告，慢刺低量 = 底噪不是主菜）
            UpdateHarass(ctx, npc, t);

            Timer++;

            //追击阀：拉到 ChaseValveDistance 才插入钻地连接件，且单发限流——用过一次
            //必须走一轮轮换才能再用，防机动战里追击无限复读
            if (t > BssDirector.ConnectorFrames && !ctx.Owner.TargetInvalid()
                && dist > BssDirector.ChaseValveDistance && !ctx.ChaseValveUsed) {
                ctx.ChaseValveUsed = true;
                ctx.LastPickedState = (int)BssStateIndex.BurrowLunge;
                return new BssBurrowLungeState();
            }

            if (t > BssDirector.ConnectorFrames && ctx.AttackCooldown <= 0
                && !ctx.Owner.TargetInvalid()) {
                ctx.ChaseValveUsed = false;
                ctx.AttackIndex++;
                IBssState pick = PickAttack(ctx);
                ctx.LastPickedState = pick is BssStateBase picked ? (int)picked.StateIndex : -1;
                return pick;
            }
            return null;
        }

        /// <summary>
        /// 屏内巡曳：折返点 = 玩家 ± PatrolOffset，爬到即换侧（跨过玩家脚下再折回来），
        /// 接近折返点先减速再转身（转身是腿架的高光帧，不许瞬间掉头）。
        /// 玩家拉远则放弃折返直线追赶；巡曳侧跨 hub 持久，收招回来不重置方向。
        /// </summary>
        private static void UpdatePatrol(BssStateContext ctx, NPC npc) {
            float dx = ctx.Target.Center.X - npc.Center.X;
            ctx.Mode = BssMoveMode.Crawl;
            ctx.LegCommand = BssLegCommand.March;

            if (Math.Abs(dx) > BssDirector.PatrolChaseDistance) {
                //拉远：直线追赶（追赶速仍在步行档）
                ctx.CrawlDirX = Math.Sign(dx);
                ctx.CrawlSpeed = BssDirector.CrawlChaseSpeed;
                ctx.PatrolSide = Math.Sign(dx);
                ctx.PatrolLegTimer = 0;
                return;
            }

            if (ctx.PatrolSide == 0) {
                ctx.PatrolSide = dx >= 0f ? 1 : -1;
            }
            float targetX = ctx.Target.Center.X + ctx.PatrolSide * BssDirector.PatrolOffset;
            float toTarget = targetX - npc.Center.X;
            ctx.PatrolLegTimer++;
            if (Math.Abs(toTarget) < BssDirector.PatrolArriveBand || ctx.PatrolLegTimer > BssDirector.PatrolLegMaxFrames) {
                ctx.PatrolSide = -ctx.PatrolSide;
                ctx.PatrolLegTimer = 0;
                targetX = ctx.Target.Center.X + ctx.PatrolSide * BssDirector.PatrolOffset;
                toTarget = targetX - npc.Center.X;
            }

            float dir = Math.Sign(toTarget);
            ctx.CrawlDirX = dir != 0f ? dir : (ctx.CrawlDirX != 0f ? ctx.CrawlDirX : 1f);
            float ease = MathHelper.Clamp(Math.Abs(toTarget) / BssDirector.PatrolSlowBand, 0f, 1f);
            ctx.CrawlSpeed = MathHelper.Lerp(BssDirector.CrawlTurnSpeed, BssDirector.CrawlCruiseSpeed, ease);
        }

        /// <summary>
        /// 骚扰甩刺：周期性从离玩家最近的露地红花节甩 HarassNeedles 枚钉刺。
        /// 公平口径：射前 HarassGlowLead 帧全花预亮 + 出手音；单源低量慢刺，
        /// 是"这条虫永远在咬"的底噪，不承担杀伤主力。
        /// </summary>
        private static void UpdateHarass(BssStateContext ctx, NPC npc, int t) {
            int gap = BssDirector.HarassGap(ctx.Phase);
            int cycle = t % gap;

            //预亮拍
            if (cycle >= gap - BssDirector.HarassGlowLead) {
                ctx.BloomGlow = Math.Max(ctx.BloomGlow, 0.7f);
            }
            if (cycle != gap - 1 || ctx.Segments.Count == 0 || !ctx.Target.Alives()) {
                return;
            }

            //出手拍：找离玩家最近的露地红花节
            NPC muzzle = null;
            float best = float.MaxValue;
            int bodyType = ModContent.NPCType<BssBody>();
            foreach (var seg in ctx.Segments) {
                if (!seg.Alives() || seg.type != bodyType
                    || !BssStateContext.IsFlowerOrdinal((int)seg.ai[0])
                    || !BssVfx.IsAboveGround(seg.Center)) {
                    continue;
                }
                float d = seg.DistanceSQ(ctx.Target.Center);
                if (d < best) {
                    best = d;
                    muzzle = seg;
                }
            }
            if (muzzle == null) {
                return;
            }

            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.45f, Pitch = 0.35f, MaxInstances = 3 }, muzzle.Center);
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustPerfect(muzzle.Center, DustID.JunglePlants,
                        Main.rand.NextVector2Circular(2f, 2f), 100, default, 0.9f);
                    d.noGravity = true;
                }
            }
            if (!VaultUtils.isClient) {
                int damage = BssDirector.ScaleProjectileDamage(npc, BssDirector.NeedleDamage);
                int type = ModContent.ProjectileType<Projectiles.BssNeedleProj>();
                Vector2 aim = (ctx.Target.Center - muzzle.Center).SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < BssDirector.HarassNeedles; i++) {
                    Vector2 vel = aim.RotatedBy(MathHelper.Lerp(-0.12f, 0.12f, BssDirector.HarassNeedles > 1
                        ? i / (float)(BssDirector.HarassNeedles - 1) : 0.5f)) * BssDirector.NeedleSpeed;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle.Center, vel, type, damage, 0.4f, Main.myPlayer);
                }
            }
        }

        /// <summary>
        /// 手写轮换表：喷沙是每隔几手就回来的主菜，掠冲/扑击/合击/沙鳍是压力招，
        /// 体节发射器（涟漪/花瓣/花刃）、刺球、沙泉、沙浪、沙雨、龙卷、流沙作区域变化，
        /// 钻地只做追击连接件与替补。表按"压迫 → 区域 → 喘息"的波形手排，同类不相邻。
        /// 高飞替补：贴地招（掠冲/沙泉/沙浪/沙鳍/流沙/合击/龙卷/花刃）对空无效，
        /// 换扑击（抛物上天）、扬沙（高弧沙雨）或钻地。
        /// 钻地连发闸：上一手已是钻地类（突袭/沙鳍/流沙）就换成替补，不让玩家连看两次沙里的空场。
        /// 近钳远喷：合击只在玩家进 SnapTriggerRange 时出，否则退回喷沙。
        /// P3 连段：扑击落地直接接掠冲；涟漪直连花瓣；沙浪出土直接接扬沙（地上一浪、天上一雨）。
        /// P2 解锁：龙卷、花刃、流沙、花瓣、沙泉。P3 解锁：盘身刺阵（怒放宣言后首招也是它）。
        /// </summary>
        private static IBssState PickAttack(BssStateContext ctx) {
            ctx.QueuedChainState = -1;

            bool air = ctx.Target.Alives()
                && BssVfx.FindGroundY(ctx.Target.Center) - ctx.Target.Center.Y > 430f;
            bool close = ctx.Target.Alives()
                && Vector2.Distance(ctx.Npc.Center, ctx.Target.Center) <= BssDirector.SnapTriggerRange;
            bool lastUnderground = ctx.LastPickedState is (int)BssStateIndex.BurrowLunge
                or (int)BssStateIndex.FinHunt or (int)BssStateIndex.Quicksand;

            IBssState Burrow(Func<IBssState> alt) => lastUnderground ? alt() : new BssBurrowLungeState();
            IBssState Dash() => air ? new BssPounceState() : new BssSandDashState();
            IBssState Geyser() => air ? Burrow(() => new BssPounceState()) : new BssGeyserMarchState();
            IBssState Fling() => new BssClawFlingState();
            IBssState Surge() => air ? Fling() : new BssSandSurgeState();
            IBssState Fin() => air ? new BssPounceState() : lastUnderground ? Dash() : new BssFinHuntState();
            IBssState Snap() => close && !air ? new BssPincerSnapState() : new BssSandSpitState();
            IBssState Devil() => air ? Fling() : new BssDustDevilState();
            IBssState Gale() => air ? Fling() : new BssWindBladeState();
            IBssState Quick() => air ? new BssPounceState() : lastUnderground ? Geyser() : new BssQuicksandState();

            if (ctx.Phase >= 3) {
                switch (ctx.AttackIndex % 20) {
                    case 1:
                        return new BssSandSpitState();
                    case 2:
                        //扑击落地直接接掠冲：落地即起跑的连段压迫
                        ctx.QueuedChainState = (int)BssStateIndex.SandDash;
                        return new BssPounceState();
                    case 3:
                        return new BssCoilRingState();
                    case 4:
                        return Gale();
                    case 5:
                        return Snap();
                    case 6:
                        return new BssSandSpitState();
                    case 7:
                        //沙浪出土直接接扬沙：地上一浪、天上一雨
                        if (!air) {
                            ctx.QueuedChainState = (int)BssStateIndex.ClawFling;
                        }
                        return Surge();
                    case 8:
                        return Devil();
                    case 9:
                        return Fin();
                    case 10:
                        return new BssSandSpitState();
                    case 11:
                        ctx.QueuedChainState = (int)BssStateIndex.PetalShake;
                        return new BssNeedleRippleState();
                    case 12:
                        return Quick();
                    case 13:
                        return Dash();
                    case 14:
                        return new BssCoilRingState();
                    case 15:
                        return new BssCactusBallState();
                    case 16:
                        return Fling();
                    case 17:
                        return new BssSandSpitState();
                    case 18:
                        return Geyser();
                    case 19:
                        return Snap();
                    default:
                        ctx.QueuedChainState = (int)BssStateIndex.SandDash;
                        return new BssPounceState();
                }
            }

            if (ctx.Phase >= 2) {
                switch (ctx.AttackIndex % 18) {
                    case 1:
                        return new BssSandSpitState();
                    case 2:
                        return new BssPounceState();
                    case 3:
                        return Gale();
                    case 4:
                        return Dash();
                    case 5:
                        return new BssSandSpitState();
                    case 6:
                        return Fin();
                    case 7:
                        return Devil();
                    case 8:
                        return Snap();
                    case 9:
                        return Surge();
                    case 10:
                        return new BssSandSpitState();
                    case 11:
                        return Geyser();
                    case 12:
                        return new BssPounceState();
                    case 13:
                        return Quick();
                    case 14:
                        return Fling();
                    case 15:
                        return Dash();
                    case 16:
                        return new BssPetalShakeState();
                    case 17:
                        return new BssSandSpitState();
                    default:
                        return new BssNeedleRippleState();
                }
            }

            switch (ctx.AttackIndex % 14) {
                case 1:
                    return new BssSandSpitState();
                case 2:
                    return new BssPounceState();
                case 3:
                    return Fling();
                case 4:
                    return Fin();
                case 5:
                    return new BssSandSpitState();
                case 6:
                    return Surge();
                case 7:
                    return Dash();
                case 8:
                    return new BssCactusBallState();
                case 9:
                    return Snap();
                case 10:
                    return new BssSandSpitState();
                case 11:
                    return new BssNeedleRippleState();
                case 12:
                    return Dash();
                case 13:
                    return Fling();
                default:
                    return new BssPounceState();
            }
        }
    }
}

using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>
    /// 爬行巡曳 hub：蜈蚣步态贴地逼近，喘息拍走完按手写轮换表出招。
    /// 轮换表各端一致、只有权威端的返回被采纳。压力（突袭）与区域（沙/球/瓣）交替。
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
            bool targetAirborne = ctx.Target.Alives()
                && BssVfx.FindGroundY(ctx.Target.Center) - ctx.Target.Center.Y > 430f;

            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = dist > 500f ? BssDirector.CrawlChaseSpeed : BssDirector.CrawlCruiseSpeed;
            ctx.LegCommand = BssLegCommand.March;

            //爬行掠沙的底噪
            if (!Main.dedServ && Main.rand.NextBool(9) && Math.Abs(npc.velocity.X) > 4f) {
                BssVfx.SandTrickle(npc.Bottom + new Vector2(Main.rand.NextFloat(-30f, 30f), 0f), 0.8f);
            }

            //骚扰甩刺：巡曳中红花节也在咬（预亮 = 预告，慢刺低量 = 底噪不是主菜）
            UpdateHarass(ctx, npc, t);

            Timer++;

            //玩家拉远或高飞：不磨蹭，直接钻地鱼雷压上去（追击即攻击）
            if (t > BssDirector.ConnectorFrames && !ctx.Owner.TargetInvalid()
                && (dist > BssDirector.PursuitDistance || targetAirborne)) {
                ctx.AttackIndex++;
                return new BssBurrowLungeState();
            }

            if (t > BssDirector.ConnectorFrames && ctx.AttackCooldown <= 0
                && !ctx.Owner.TargetInvalid() && dist < BssDirector.EngageDistance) {
                ctx.AttackIndex++;
                return PickAttack(ctx);
            }
            return null;
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
        /// 手写轮换表：掠冲/突袭（压力）与沙团/刺球/涟漪/花瓣（区域）交替，强招押后阶段解锁。
        /// 掠冲是贴地压力主力；玩家高飞时换成破土突袭（掠冲贴地打不到天上）。
        /// P3 花瓣直接连击涟漪（收招帧即后招蓄力帧）。
        /// </summary>
        private static IBssState PickAttack(BssStateContext ctx) {
            ctx.QueuedChainState = -1;

            //目标长期高飞：贴地掠冲无解高空，换破土突袭压上去
            bool targetAirborne = ctx.Target.Alives()
                && BssVfx.FindGroundY(ctx.Target.Center) - ctx.Target.Center.Y > 430f;
            IBssState Dash() => targetAirborne ? new BssBurrowLungeState() : new BssSandDashState();

            if (ctx.Phase >= 3) {
                switch (ctx.AttackIndex % 8) {
                    case 1:
                        ctx.QueuedChainState = (int)BssStateIndex.NeedleRipple;
                        return new BssPetalShakeState();
                    case 2:
                        return new BssCoilOrbitState();
                    case 3:
                        return new BssCactusBallState();
                    case 4:
                        return Dash();
                    case 5:
                        return new BssSkyWeaveState();
                    case 6:
                        return new BssNeedleRippleState();
                    case 7:
                        return new BssBurrowLungeState();
                    default:
                        return Dash();
                }
            }

            if (ctx.Phase >= 2) {
                switch (ctx.AttackIndex % 8) {
                    case 1:
                        return new BssNeedleRippleState();
                    case 2:
                        return new BssSkyWeaveState();
                    case 3:
                        return new BssPetalShakeState();
                    case 4:
                        return new BssCoilOrbitState();
                    case 5:
                        return targetAirborne ? new BssBurrowLungeState() : new BssSandSpitState();
                    case 6:
                        return new BssCactusBallState();
                    case 7:
                        return new BssBurrowLungeState();
                    default:
                        return Dash();
                }
            }

            switch (ctx.AttackIndex % 6) {
                case 1:
                    return targetAirborne ? new BssBurrowLungeState() : new BssSandSpitState();
                case 2:
                    return new BssBurrowLungeState();
                case 3:
                    return new BssCoilOrbitState();
                case 4:
                    return new BssCactusBallState();
                case 5:
                    return Dash();
                default:
                    return Dash();
            }
        }
    }
}

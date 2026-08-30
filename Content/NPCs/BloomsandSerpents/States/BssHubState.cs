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

            //追击阀：拉到 ChaseValveDistance 才插入钻地连接件，且单发限流——用过一次
            //必须走一轮轮换才能再用。旧版 1400px 无限连发会让机动战整场复读钻地，
            //轮换表（含沙柱三招）永远轮不到（真机反馈 2026-08-31）。轮换招大半
            //自带远距/对空能力（突刺点名脚下、腾跃爆冲、突袭本身），不靠追击兜距离
            if (t > BssDirector.ConnectorFrames && !ctx.Owner.TargetInvalid()
                && dist > BssDirector.ChaseValveDistance && !ctx.ChaseValveUsed) {
                ctx.ChaseValveUsed = true;
                return new BssBurrowLungeState();
            }

            if (t > BssDirector.ConnectorFrames && ctx.AttackCooldown <= 0
                && !ctx.Owner.TargetInvalid()) {
                ctx.ChaseValveUsed = false;
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
        /// 手写轮换表：压力招（掠冲/突袭/漩涡/甩尾/腾跃）与区域招（沙团/刺球/涟漪/
        /// 花瓣/沙泉/沙瀑/沙柱）交替，强招押后阶段解锁：沙泉行军与沙柱突刺 P1 即有
        /// （立起砸地/跺地点名的腿架戏，突刺双槽保出场率），沙爆漩涡、回环沙瀑、
        /// 沙柱腾跃与沙柱爆震属沙暴身份 P2 解锁（转阶段收尾即漩涡首秀），
        /// 回马甩尾 P3 才上（自带掠冲回马枪连段）。
        /// 沙柱三招一律前置排位且相互毗邻：突刺柱只滞留 16s，爆震必须排在突刺
        /// 两招之内，排远了轮到时柱已沉、门槛永远不过（真机时序死穴 2026-08-31）。
        /// 高飞替补按槽位各配：贴地招换成突袭/天游/漩涡等对空招（沙柱突刺自带空中
        /// 凝沙变体，天然对空），天上也有全套变化。
        /// P3 连段：花瓣直连涟漪；沙柱突刺直连爆震（种柱即引爆的连段压迫，自带种柱保底）。
        /// </summary>
        private static IBssState PickAttack(BssStateContext ctx) {
            ctx.QueuedChainState = -1;

            //高飞判定：贴地招按槽位换对空替补（不再一律钻地）
            bool air = ctx.Target.Alives()
                && BssVfx.FindGroundY(ctx.Target.Center) - ctx.Target.Center.Y > 430f;
            IBssState Dash() => air ? new BssBurrowLungeState() : new BssSandDashState();

            if (ctx.Phase >= 3) {
                switch (ctx.AttackIndex % 12) {
                    case 1:
                        ctx.QueuedChainState = (int)BssStateIndex.NeedleRipple;
                        return new BssPetalShakeState();
                    case 2:
                        //种柱即引爆：突刺收招直接接爆震（爆震自带种柱保底，连段必成立）
                        ctx.QueuedChainState = (int)BssStateIndex.PillarBurst;
                        return new BssPillarSpikeState();
                    case 3:
                        return new BssVortexDashState();
                    case 4:
                        //甩尾要贴地擦身；高飞替补漩涡（终局旗舰加密是特性）
                        return air ? new BssVortexDashState() : (IBssState)new BssTailSweepState();
                    case 5:
                        return new BssPillarVaultState();
                    case 6:
                        return new BssLoopCascadeState();
                    case 7:
                        return air ? new BssBurrowLungeState() : (IBssState)new BssGeyserMarchState();
                    case 8:
                        return new BssCoilOrbitState();
                    case 9:
                        return new BssNeedleRippleState();
                    case 10:
                        return new BssCactusBallState();
                    case 11:
                        return new BssBurrowLungeState();
                    default:
                        return Dash();
                }
            }

            if (ctx.Phase >= 2) {
                switch (ctx.AttackIndex % 13) {
                    case 1:
                        return new BssVortexDashState();
                    case 2:
                        return new BssPillarSpikeState();
                    case 3:
                        return new BssNeedleRippleState();
                    case 4:
                        //爆震紧跟突刺两招内（突刺柱滞留 16s，排远了轮到时柱已沉、
                        //门槛永远不过——真机反馈爆震整场不放的时序死穴）；
                        //柱不够落到本槽原本的地面压制替补
                        return BssSandPillar.CountDetonatable() >= BssDirector.BurstMinPillars
                            ? new BssPillarBurstState()
                            : (air ? new BssBurrowLungeState() : (IBssState)new BssGeyserMarchState());
                    case 5:
                        return new BssPillarVaultState();
                    case 6:
                        return new BssPetalShakeState();
                    case 7:
                        return new BssLoopCascadeState();
                    case 8:
                        return air ? new BssSkyWeaveState() : (IBssState)new BssSandSpitState();
                    case 9:
                        return air ? new BssBurrowLungeState() : (IBssState)new BssGeyserMarchState();
                    case 10:
                        return new BssCoilOrbitState();
                    case 11:
                        return new BssSkyWeaveState();
                    case 12:
                        return new BssBurrowLungeState();
                    default:
                        return Dash();
                }
            }

            switch (ctx.AttackIndex % 8) {
                case 1:
                    return air ? new BssSkyWeaveState() : (IBssState)new BssSandSpitState();
                case 2:
                    return new BssPillarSpikeState();
                case 3:
                    return air ? new BssBurrowLungeState() : (IBssState)new BssGeyserMarchState();
                case 4:
                    return new BssCactusBallState();
                case 5:
                    return new BssCoilOrbitState();
                case 6:
                    return new BssPillarSpikeState();
                case 7:
                    return new BssBurrowLungeState();
                default:
                    return Dash();
            }
        }
    }
}

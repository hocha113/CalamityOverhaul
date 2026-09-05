using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.States
{
    /// <summary>
    /// 爬行巡曳 hub：蜈蚣步态贴地直线逼近玩家（拉远换追赶速），喘息拍走完按手写轮换表出招。
    /// 轮换表各端一致、只有权威端的返回被采纳。压力招（掠冲/扑击/天游/环猎/漩涡/甩尾/沙鳍/合击）
    /// 与区域招（喷沙/扬沙/刺球/涟漪/花瓣/沙泉/沙浪/龙卷/花刃/流沙/刺阵）交替。
    /// hub 只是换招的一口气，不是展示步态的巡游：折返巡曳（2026-09-06 一版）让整场读成贴地蠕动，已撤。
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

            //掉头死区放宽到约三个节距：1700px 的身体每次掉头都是整链叠回自己身上，
            //玩家在头顶附近小幅横移不该触发
            ctx.Mode = BssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx, BssDirector.SegmentGap * 3f * BssDirector.BodyScale);
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
        /// 手写轮换表：压力招与区域招严格交替（压迫 → 区域 → 喘息的波形），同类不相邻。
        /// 压力招分两种身体语言：贴地（掠冲/扑击/合击/沙鳍/甩尾/腾跃）与破空（天游/环猎/漩涡/回环），
        /// 空中招错开排位，一轮里蛇 P1 上天三次、P2 起四次——"沙漠游龙"的身份靠它们撑着。
        /// 区域招：喷沙/扬沙/刺球/涟漪/花瓣/沙泉/沙浪/龙卷/花刃/流沙/刺阵/沙柱突刺/沙柱爆震，每种一轮最多两手。
        /// 沙柱三招前置排位且相互毗邻：突刺柱只滞留 16 秒，爆震必须排在突刺两招之内，
        /// 腾跃紧跟其后借柱（排远了轮到时柱已沉、门槛永远不过——真机时序死穴 2026-08-31）。
        /// 钻地突袭只做追击连接件与对空替补，不进主轮换。
        /// 高飞替补：贴地招对空无效，按槽位各配替补（扑击抛物上天、扬沙高弧沙雨、天游/漩涡本身在天上、
        /// 钻地破土向上；沙柱突刺自带空中凝沙变体，天然对空）。
        /// 钻地连发闸：上一手已是钻地类（突袭/沙鳍/流沙）就换替补，不让玩家连看两次沙里的空场。
        /// 近钳远替：合击只在玩家进 SnapTriggerRange 时出，否则退到本槽位的压力替补
        /// （不许退成喷沙——上一版退成喷沙后 P1 轮换里 14 手有 5 手喷沙且两两相邻，整场读成复读）。
        /// P2 解锁：漩涡冲刺（转阶段收尾即首秀）、回环沙瀑、龙卷、花刃、流沙、花瓣、沙柱腾跃、沙柱爆震。
        /// P3 解锁：盘身刺阵（怒放宣言后首招）、回马甩尾（自带掠冲回马枪连段）。
        /// P3 连段：扑击落地直接接掠冲；沙柱突刺直连爆震（种柱即引爆，爆震自带种柱保底）；
        /// 涟漪直连花瓣；沙浪出土直接接扬沙（地上一浪、天上一雨）；回环俯冲落地接掠冲由状态自管。
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
            IBssState Fling() => new BssClawFlingState();
            IBssState Geyser() => air ? Burrow(Fling) : new BssGeyserMarchState();
            IBssState Surge() => air ? Fling() : new BssSandSurgeState();
            IBssState Fin() => air ? new BssPounceState() : lastUnderground ? Dash() : new BssFinHuntState();
            IBssState Snap(Func<IBssState> farAlt) => close && !air ? new BssPincerSnapState() : farAlt();
            IBssState Devil() => air ? Fling() : new BssDustDevilState();
            IBssState Gale() => air ? Fling() : new BssWindBladeState();
            IBssState Quick() => air ? new BssPounceState() : lastUnderground ? Geyser() : new BssQuicksandState();
            IBssState Sweep() => air ? new BssVortexDashState() : new BssTailSweepState();
            //爆震门槛：场上可点名柱不足就落到本槽位的区域替补（真机时序死穴的保险）
            IBssState Burst(Func<IBssState> alt)
                => BssSandPillar.CountDetonatable() >= BssDirector.BurstMinPillars ? new BssPillarBurstState() : alt();

            if (ctx.Phase >= 3) {
                switch (ctx.AttackIndex % 22) {
                    case 1:
                        return new BssSandSpitState();
                    case 2:
                        //扑击落地直接接掠冲：落地即起跑的连段压迫
                        ctx.QueuedChainState = (int)BssStateIndex.SandDash;
                        return new BssPounceState();
                    case 3:
                        //种柱即引爆：突刺收招直接接爆震（爆震自带种柱保底，连段必成立）
                        ctx.QueuedChainState = (int)BssStateIndex.PillarBurst;
                        return new BssPillarSpikeState();
                    case 4:
                        return Sweep();
                    case 5:
                        return Gale();
                    case 6:
                        return new BssPillarVaultState();
                    case 7:
                        ctx.QueuedChainState = (int)BssStateIndex.PetalShake;
                        return new BssNeedleRippleState();
                    case 8:
                        return new BssLoopCascadeState();
                    case 9:
                        //沙浪出土直接接扬沙：地上一浪、天上一雨
                        if (!air) {
                            ctx.QueuedChainState = (int)BssStateIndex.ClawFling;
                        }
                        return Surge();
                    case 10:
                        return Snap(Dash);
                    case 11:
                        return new BssCactusBallState();
                    case 12:
                        return new BssVortexDashState();
                    case 13:
                        return Devil();
                    case 14:
                        return Fin();
                    case 15:
                        return Geyser();
                    case 16:
                        return new BssCoilOrbitState();
                    case 17:
                        return new BssSandSpitState();
                    case 18:
                        return Quick();
                    case 19:
                        return new BssCoilRingState();
                    case 20:
                        return new BssSkyWeaveState();
                    case 21:
                        return Fling();
                    default:
                        return Dash();
                }
            }

            if (ctx.Phase >= 2) {
                switch (ctx.AttackIndex % 20) {
                    case 1:
                        //沙暴身份的招牌：转阶段收尾即漩涡首秀（转阶段把序号归零）
                        return new BssVortexDashState();
                    case 2:
                        return new BssPillarSpikeState();
                    case 3:
                        return new BssPounceState();
                    case 4:
                        //爆震紧跟突刺两招内（柱滞留 16 秒）；柱不够落到花刃
                        return Burst(Gale);
                    case 5:
                        return new BssPillarVaultState();
                    case 6:
                        return Devil();
                    case 7:
                        return new BssLoopCascadeState();
                    case 8:
                        return new BssPetalShakeState();
                    case 9:
                        return Fin();
                    case 10:
                        return Geyser();
                    case 11:
                        return new BssSkyWeaveState();
                    case 12:
                        return Surge();
                    case 13:
                        return Snap(() => new BssVortexDashState());
                    case 14:
                        return new BssNeedleRippleState();
                    case 15:
                        return new BssCoilOrbitState();
                    case 16:
                        return Gale();
                    case 17:
                        return Quick();
                    case 18:
                        return new BssSandSpitState();
                    case 19:
                        return Dash();
                    default:
                        return Fling();
                }
            }

            switch (ctx.AttackIndex % 16) {
                case 1:
                    return new BssSandSpitState();
                case 2:
                    return new BssPounceState();
                case 3:
                    //沙柱突刺 P1 即有：跺地点名的腿架戏 + 全场沸腾，柱滞留给后面的招当场地
                    return new BssPillarSpikeState();
                case 4:
                    return new BssCoilOrbitState();
                case 5:
                    return Fling();
                case 6:
                    return Dash();
                case 7:
                    return new BssCactusBallState();
                case 8:
                    return new BssSkyWeaveState();
                case 9:
                    return Geyser();
                case 10:
                    return Fin();
                case 11:
                    //突刺双槽保出场率
                    return new BssPillarSpikeState();
                case 12:
                    return Snap(() => new BssPounceState());
                case 13:
                    return new BssNeedleRippleState();
                case 14:
                    return Dash();
                case 15:
                    return Surge();
                default:
                    return new BssSkyWeaveState();
            }
        }
    }
}

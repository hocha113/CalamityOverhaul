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
    /// 爬行巡曳 hub：变异蜈蚣步态贴地逼近，喘息拍走完按手写轮换表出招。
    /// 巡曳中囊肿节周期滴射骚扰（预亮再射的慢滴底噪，"这条虫永远在渗"）。
    /// 轮换表各端一致、只有权威端的返回被采纳。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.Hub, typeof(FssStateContext))]
    internal class FssHubState : FssStateBase
    {
        public override string StateName => "Hub";
        public override FssStateIndex StateIndex => FssStateIndex.Hub;

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            float dist = Vector2.Distance(npc.Center, ctx.Target.Center);

            ctx.Mode = FssMoveMode.Crawl;
            ctx.CrawlDirX = FacingToTarget(ctx);
            ctx.CrawlSpeed = dist > 520f ? FssDirector.CrawlChaseSpeed : FssDirector.CrawlCruiseSpeed;
            ctx.LegCommand = FssLegCommand.March;

            //爬行渗漏的底噪
            if (!Main.dedServ && Main.rand.NextBool(9) && Math.Abs(npc.velocity.X) > 4f) {
                FssVfx.FesterTrickle(npc.Bottom + new Vector2(Main.rand.NextFloat(-34f, 34f), 0f), 0.8f);
            }

            //骚扰滴射：巡曳中囊肿节也在渗
            UpdateHarass(ctx, npc);

            Timer++;

            //追击阀：拉到 ChaseValveDistance 才插入连接件（P2 门冲 / P1 钻沙突袭），
            //且单发限流——用过一次必须走一手轮换才许再追。旧版 1500px 无限连发会让
            //机动战整场复读门冲/钻地，轮换表（含鳌足新招）永远轮不到（移植 BSS 口径）
            if (t > FssDirector.ConnectorFrames && !ctx.Owner.TargetInvalid()
                && dist > FssDirector.ChaseValveDistance && !ctx.ChaseValveUsed) {
                ctx.ChaseValveUsed = true;
                if (ctx.Phase >= 2) {
                    ctx.LastPickedState = (int)FssStateIndex.PortalRush;
                    return new FssPortalRushState();
                }
                ctx.LastPickedState = (int)FssStateIndex.BreachFount;
                return new FssBreachFountState();
            }

            if (t > FssDirector.ConnectorFrames && ctx.AttackCooldown <= 0
                && !ctx.Owner.TargetInvalid()) {
                IFssState pick = PickAttack(ctx);
                if (pick != null) {
                    ctx.ChaseValveUsed = false;
                    ctx.AttackIndex++;
                    ctx.LastPickedState = pick is FssStateBase picked ? (int)picked.StateIndex : -1;
                    return pick;
                }
            }
            return null;
        }

        /// <summary>
        /// 骚扰滴射：周期性从离玩家最近的露地囊肿节滴出慢速灵液珠（雨滴模式不留池）。
        /// 时钟读 ctx.HarassClock（跨 hub 进出持久累积）：hub 每次重建 Timer 归零、
        /// 且 hub 常只活几帧，按状态内 Timer 取模永远凑不满周期。
        /// 公平口径：射前 HarassGlowLead 帧囊肿预亮 + 出手音；单源低量慢滴，
        /// 是压迫底噪不是杀伤主力。
        /// </summary>
        private static void UpdateHarass(FssStateContext ctx, NPC npc) {
            int gap = FssDirector.HarassGap(ctx.Phase);
            ctx.HarassClock++;

            //预亮拍
            if (ctx.HarassClock >= gap - FssDirector.HarassGlowLead) {
                ctx.CystGlow = Math.Max(ctx.CystGlow, 0.7f);
            }
            if (ctx.HarassClock < gap || ctx.Segments.Count == 0 || !ctx.Target.Alives()) {
                return;
            }
            ctx.HarassClock = 0;

            //出手拍：找离玩家最近的露地未瘪囊肿节
            NPC muzzle = null;
            float best = float.MaxValue;
            int bodyType = ModContent.NPCType<FssBody>();
            foreach (var seg in ctx.Segments) {
                int ordinal = (int)seg.ai[0];
                if (!seg.Alives() || seg.type != bodyType
                    || !FssStateContext.IsCystOrdinal(ordinal)
                    || (ordinal < ctx.CystSpent.Length && ctx.CystSpent[ordinal] > 0.5f)
                    || !FssVfx.IsAboveGround(seg.Center)) {
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
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.45f, Pitch = -0.05f, MaxInstances = 3 }, muzzle.Center);
                FssVfx.IchorBurst(muzzle.Center, 0.5f,
                    (ctx.Target.Center - muzzle.Center).SafeNormalize(Vector2.UnitX));
            }
            if (!VaultUtils.isClient) {
                int damage = FssDirector.ScaleProjectileDamage(npc, FssDirector.IchorGlobDamage);
                int type = ModContent.ProjectileType<FssIchorGlob>();
                Vector2 aim = (ctx.Target.Center - muzzle.Center).SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < FssDirector.HarassDrops; i++) {
                    Vector2 vel = aim.RotatedBy(MathHelper.Lerp(-0.1f, 0.1f, FssDirector.HarassDrops > 1
                        ? i / (float)(FssDirector.HarassDrops - 1) : 0.5f)) * 9.5f - new Vector2(0f, 1.5f);
                    //雨滴模式：骚扰滴不参与池经济（低烦度）
                    Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle.Center, vel, type,
                        (int)(damage * 0.75f), 0.3f, Main.myPlayer, 1f);
                }
            }
        }

        /// <summary>
        /// 手写轮换表：压力招（毒冲/突袭/掠航/门冲）与区域招（扫喷/黏疮/炮/环卷/
        /// 鳌足夯地）交替，强招押后阶段解锁：吞沙炮/门冲/掠航/长镰自刈属变异蔓延
        /// 身份 P2 起，满场引爆与裂躯交叉 P3 才上；环卷瀑洗与疮杵夯地 P1 即有
        /// （夯地播池 = 给满场引爆喂燃料的前菜）。
        /// 高飞/平台替补：贴地招按槽位换成环卷/门冲/裂躯，天上也有全套变化。
        /// 压力招连发闸：门冲/破土/毒冲上一手同招即换替补（含追击阀记账）——
        /// 黏疮布点与吞沙炮各保双语境出场，复读连接件不许挤占轮换（移植 BSS 口径）。
        /// </summary>
        private static IFssState PickAttack(FssStateContext ctx) {
            ctx.QueuedChainState = -1;

            //高飞判定：贴地招按槽位换对空替补
            bool air = ctx.Target.Alives()
                && FssVfx.FindGroundY(ctx.Target.Center) - ctx.Target.Center.Y > 430f;

            //压力招连发闸：门冲/破土/毒冲是复读惯犯——上一手（含追击阀）已是同招
            //就换成槽位替补，两记之间必然隔一手别的（移植 BSS 真机验证过的口径）
            IFssState Portal(Func<IFssState> alt)
                => ctx.LastPickedState == (int)FssStateIndex.PortalRush ? alt() : new FssPortalRushState();
            IFssState Breach(Func<IFssState> alt)
                => ctx.LastPickedState == (int)FssStateIndex.BreachFount ? alt() : new FssBreachFountState();
            IFssState Skim(Func<IFssState> alt)
                => ctx.LastPickedState == (int)FssStateIndex.VenomSkim ? alt() : new FssVenomSkimState();

            if (ctx.Phase >= 3) {
                switch (ctx.AttackIndex % 12) {
                    case 1:
                        return air ? Portal(() => new FssCoilCascadeState())
                            : (IFssState)new FssSwallowMortarState();
                    case 2:
                        //长镰自刈 P3 常驻（割囊肿的资源戏；充能不足状态自落替补）
                        return new FssClawReapState();
                    case 3:
                        //满场引爆吃地面池；高空/平台观众改看裂躯交叉
                        return air ? new FssSunderCrossState() : (IFssState)new FssFieldDetonateState();
                    case 4:
                        return new FssCoilCascadeState();
                    case 5:
                        //P3 夯击升级为泉列版（普通夯地退居 P1/P2）
                        return air ? Portal(() => new FssSunderCrossState())
                            : (IFssState)new FssClawQuakeState();
                    case 6:
                        return new FssFesterRippleState();
                    case 7:
                        return new FssSunderCrossState();
                    case 8:
                        return air ? new FssCoilCascadeState() : (IFssState)new FssIchorSpitState();
                    case 9:
                        return air ? Portal(() => new FssCoilCascadeState())
                            : Breach(() => new FssFesterRippleState());
                    case 10:
                        return new FssStickyCystState();
                    case 11:
                        return air ? Portal(() => new FssSunderCrossState())
                            : Skim(() => new FssIchorSpitState());
                    default:
                        return new FssSunderCrossState();
                }
            }

            if (ctx.Phase >= 2) {
                switch (ctx.AttackIndex % 11) {
                    case 1:
                        return air ? Portal(() => new FssCoilCascadeState())
                            : (IFssState)new FssIchorSpitState();
                    case 2:
                        //疮杵夯地：播种脓池的前菜（给满场引爆喂燃料）
                        return new FssClawSlamState();
                    case 3:
                        return air ? Portal(() => new FssCoilCascadeState())
                            : (IFssState)new FssSwallowMortarState();
                    case 4:
                        return air ? new FssCoilCascadeState() : Skim(() => new FssIchorSpitState());
                    case 5:
                        //长镰自刈 P2 首秀
                        return new FssClawReapState();
                    case 6:
                        //夯地泉列 P2 首秀（冲击柱双向行军）
                        return new FssClawQuakeState();
                    case 7:
                        return air ? Portal(() => new FssCoilCascadeState())
                            : (IFssState)new FssStickyCystState();
                    case 8:
                        return new FssFesterRippleState();
                    case 9:
                        return Breach(() => new FssFesterRippleState());
                    case 10:
                        //环卷专属槽（泉列顶掉旧环卷槽后还回来：基础招不许从地面轮换里消失）
                        return new FssCoilCascadeState();
                    default:
                        return air ? new FssCoilCascadeState() : Skim(() => new FssIchorSpitState());
                }
            }

            switch (ctx.AttackIndex % 9) {
                case 1:
                    return air ? new FssCoilCascadeState() : (IFssState)new FssIchorSpitState();
                case 2:
                    return air ? Breach(() => new FssCoilCascadeState())
                        : (IFssState)new FssStickyCystState();
                case 3:
                    //疮杵夯地 P1 即有（腿架 + 播池的重击戏）
                    return new FssClawSlamState();
                case 4:
                    return Breach(() => new FssCoilCascadeState());
                case 5:
                    return air ? new FssCoilCascadeState() : Skim(() => new FssIchorSpitState());
                case 6:
                    return air ? Breach(() => new FssCoilCascadeState())
                        : (IFssState)new FssIchorSpitState();
                case 7:
                    return air ? new FssCoilCascadeState() : Skim(() => new FssStickyCystState());
                case 8:
                    return new FssClawSlamState();
                default:
                    return air ? Breach(() => new FssCoilCascadeState())
                        : Skim(() => new FssIchorSpitState());
            }
        }
    }
}

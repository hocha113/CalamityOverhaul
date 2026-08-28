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

            //追击阀：拉出交战圈时接管（追击即攻击，不消耗轮换序号）。
            //P2 起走门冲（地形无关的贴近手段），P1 保持钻沙突袭
            if (t > FssDirector.ConnectorFrames && !ctx.Owner.TargetInvalid()
                && dist > FssDirector.EngageDistance) {
                return ctx.Phase >= 2 ? new FssPortalRushState() : (IFssState)new FssBreachFountState();
            }

            if (t > FssDirector.ConnectorFrames && ctx.AttackCooldown <= 0
                && !ctx.Owner.TargetInvalid()) {
                IFssState pick = PickAttack(ctx);
                if (pick != null) {
                    ctx.AttackIndex++;
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
        /// 手写轮换表：压力招（毒冲/突袭/掠航/门冲）与区域招（扫喷/黏疮/炮/环卷）交替，
        /// 强招押后阶段解锁：吞沙炮/门冲/掠航属变异蔓延身份 P2 起，
        /// 满场引爆与裂躯交叉 P3 才上；环卷瀑洗 P1 即有（地形无关的基础对空）。
        /// 高飞/平台替补：贴地招按槽位换成环卷/门冲/破土（破土自地下向上突袭，
        /// 对平台上方玩家仍有效），天上也有全套变化而非单招循环。
        /// </summary>
        private static IFssState PickAttack(FssStateContext ctx) {
            ctx.QueuedChainState = -1;

            //高飞判定：贴地招按槽位换对空替补
            bool air = ctx.Target.Alives()
                && FssVfx.FindGroundY(ctx.Target.Center) - ctx.Target.Center.Y > 430f;

            if (ctx.Phase >= 3) {
                switch (ctx.AttackIndex % 10) {
                    case 1:
                        return air ? new FssPortalRushState() : (IFssState)new FssSwallowMortarState();
                    case 2:
                        return air ? new FssPortalRushState() : (IFssState)new FssVenomSkimState();
                    case 3:
                        //满场引爆吃地面池；高空/平台观众改看裂躯交叉
                        return air ? new FssSunderCrossState() : (IFssState)new FssFieldDetonateState();
                    case 4:
                        return new FssCoilCascadeState();
                    case 5:
                        return new FssFesterRippleState();
                    case 6:
                        return new FssSunderCrossState();
                    case 7:
                        return air ? new FssCoilCascadeState() : (IFssState)new FssIchorSpitState();
                    case 8:
                        return air ? new FssPortalRushState() : (IFssState)new FssBreachFountState();
                    case 9:
                        return air ? new FssPortalRushState() : (IFssState)new FssVenomSkimState();
                    default:
                        return new FssSunderCrossState();
                }
            }

            if (ctx.Phase >= 2) {
                switch (ctx.AttackIndex % 8) {
                    case 1:
                        return air ? new FssPortalRushState() : (IFssState)new FssIchorSpitState();
                    case 2:
                        return air ? new FssPortalRushState() : (IFssState)new FssSwallowMortarState();
                    case 3:
                        return air ? new FssCoilCascadeState() : (IFssState)new FssVenomSkimState();
                    case 4:
                        return new FssCoilCascadeState();
                    case 5:
                        return air ? new FssPortalRushState() : (IFssState)new FssStickyCystState();
                    case 6:
                        return new FssFesterRippleState();
                    case 7:
                        return new FssBreachFountState();
                    default:
                        return air ? new FssCoilCascadeState() : (IFssState)new FssVenomSkimState();
                }
            }

            switch (ctx.AttackIndex % 7) {
                case 1:
                    return air ? new FssCoilCascadeState() : (IFssState)new FssIchorSpitState();
                case 2:
                    return air ? new FssBreachFountState() : (IFssState)new FssStickyCystState();
                case 3:
                    return new FssBreachFountState();
                case 4:
                    return air ? new FssCoilCascadeState() : (IFssState)new FssVenomSkimState();
                case 5:
                    return air ? new FssBreachFountState() : (IFssState)new FssIchorSpitState();
                case 6:
                    return air ? new FssCoilCascadeState() : (IFssState)new FssVenomSkimState();
                default:
                    return air ? new FssBreachFountState() : (IFssState)new FssVenomSkimState();
            }
        }
    }
}

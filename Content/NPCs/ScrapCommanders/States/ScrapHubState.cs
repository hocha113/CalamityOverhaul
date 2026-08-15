using CalamityOverhaul.Content.NPCs.ScrapCommanders.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.States
{
    /// <summary>
    /// 悬停选招 hub：贴着目标一侧上方缓浮，垂链泄压喘息，
    /// 冷却走完按轮换表出招。选招裁决只在权威端生效
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.Hub, typeof(ScrapStateContext))]
    internal class ScrapHubState : ScrapStateBase
    {
        public override string StateName => "Hub";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.Hub;

        private bool ventPlayed;

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            Player target = ctx.Target;
            int t = (int)Timer;

            //悬停锚点：保持当前侧，呼吸浮动
            float side = MathF.Sign(npc.Center.X - target.Center.X);
            if (side == 0f) {
                side = 1f;
            }
            Vector2 anchor = target.Center + new Vector2(side * 300f, -190f);
            anchor.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.6f + ctx.Owner.Seed) * 9f;
            anchor.X += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.1f + ctx.Owner.Seed * 2f) * 6f;

            float dist = Vector2.Distance(npc.Center, target.Center);
            if (dist > ScrapDirector.LeashDistance) {
                //跟丢硬追，别让玩家风筝出一个屏
                GlideToward(ctx, target.Center, 0.09f, 22f, 0.14f);
            }
            else {
                GlideToward(ctx, anchor, 0.065f, 17f, 0.12f);
            }
            LeanByVelocity(npc);

            //闲臂骚扰层（P2 起）：隔轮在 connector 喘息里补一发慢速脉冲，屏幕不静止。
            //先挂 16 帧瞄准线+聚能再出膛（预警可读；开火拍 24 早于最短冷却 27，转场不吞弹）
            const int HarassAimStart = 8;
            const int HarassFire = 24;
            if (ctx.Phase >= 2 && ctx.AttackIndex % 2 == 1 && !ctx.Owner.TargetInvalid()
                && t >= HarassAimStart && t <= HarassFire) {
                Vector2 hAim = (target.Center - ctx.Owner.GetArmPos(ScrapCommander.ArmLaser))
                    .SafeNormalize(Vector2.UnitX);
                //镭射臂从垂链转向目标持位——臂的转向本身就是前摇信号
                ctx.Arms[ScrapCommander.ArmLaser] = new ArmDirective {
                    Mode = ArmMode.Hold,
                    Target = npc.Center + npc.velocity + new Vector2(MathF.Sign(hAim.X) * 122f, -4f),
                    Spring = 0.2f,
                    Damping = 0.78f,
                    UseRot = true,
                    WantRot = hAim.ToRotation() - MathHelper.PiOver2,
                    RotRate = 0.3f,
                };
                if (t == HarassAimStart) {
                    ctx.Owner.ChargeLaser(HarassFire - HarassAimStart);
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.3f, Pitch = 0.4f, MaxInstances = 2 },
                        ctx.Owner.GetArmPos(ScrapCommander.ArmLaser));
                }
                float aimAlpha = (t - HarassAimStart) / (float)(HarassFire - HarassAimStart) * 0.4f;
                ctx.AddTelegraph(ctx.Owner.GetArmPos(ScrapCommander.ArmLaser) + hAim * 24f,
                    hAim, 700f, aimAlpha, 0.45f);
                if (t == HarassFire) {
                    ctx.Owner.ImpulseArm(ScrapCommander.ArmLaser, -hAim * 3f);
                    SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.3f, Pitch = 0.3f, MaxInstances = 2 },
                        ctx.Owner.GetArmPos(ScrapCommander.ArmLaser));
                    if (!VaultUtils.isClient) {
                        int hDamage = ScrapDirector.ScaleProjectileDamage(npc, (24f, 20f));
                        Projectile.NewProjectile(npc.GetSource_FromAI(),
                            ctx.Owner.GetArmPos(ScrapCommander.ArmLaser) + hAim * 24f, hAim * 13f,
                            ModContent.ProjectileType<Projectiles.ScrapLaserPulse>(), hDamage, 1f, Main.myPlayer);
                    }
                }
            }

            //入场那口泄压：垂链喘息的听觉标记
            if (t == 6 && !ventPlayed) {
                ventPlayed = true;
                SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.3f, Pitch = -0.4f, MaxInstances = 2 }, npc.Center);
                if (!Main.dedServ) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        ctx.Owner.GetArmPos(ScrapCommander.ArmCannon) + new Vector2(0f, -8f),
                        new Vector2(0f, -0.6f), ScrapCommander.SmokeGray * 0.8f, 0.6f)?.Configure(36);
                }
            }

            //过载阶段的身体代价：每次回 hub 自落一块零件（服务端裁决）
            if (t == 4 && ctx.Phase >= 3 && ctx.AttackIndex > 0 && !VaultUtils.isClient) {
                int arm = Main.rand.Next(ScrapCommander.ArmCount);
                int damage = ScrapDirector.ScaleProjectileDamage(npc, ScrapDirector.GroundSawDamage);
                Projectile.NewProjectile(npc.GetSource_FromAI(), ctx.Owner.GetArmPos(arm),
                    new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -1f),
                    ModContent.ProjectileType<Projectiles.ScrapDebris>(), damage, 2f,
                    Main.myPlayer, -1f);
            }

            //常态目镜巡扫
            float cycle = (t + npc.whoAmI * 37) % 240;
            if (cycle < 42f) {
                ctx.EyeScan = cycle / 42f;
            }

            Timer++;

            //出招裁决：轮换表各端一致，只有权威端的返回被采纳
            if (t > ScrapDirector.ConnectorFrames && ctx.AttackCooldown <= 0
                && !ctx.Owner.TargetInvalid() && dist < ScrapDirector.EngageDistance) {
                ctx.AttackIndex++;
                return PickAttack(ctx, npc);
            }
            return null;
        }

        /// <summary>
        /// 手写轮换表（压力/走位/压制交替）+ 连击队列，各端一致、只有权威端的返回被采纳：
        /// P1 六循环 锯(接迫击)→钳(接头锤)→迫击→十字旋→镭射扫削→头锤；
        /// P2 八循环插入军团/磁暴/协奏/瀑布/矩阵/总攻，过载后终局招隔轮压场；
        /// 军团满编时本体不再贴身肉搏（单打手规则）
        /// </summary>
        private static IScrapState PickAttack(ScrapStateContext ctx, NPC npc) {
            ctx.QueuedChainState = -1;

            if (ctx.Phase >= 2) {
                int probes = ScrapLegionProbe.CountFor(npc);
                IScrapState next;
                switch (ctx.AttackIndex % 8) {
                    case 1:
                        if (probes == 0) {
                            next = new ScrapLegionState();
                        }
                        else {
                            next = new ScrapSawLaunchState();
                            ctx.QueuedChainState = (int)ScrapStateIndex.Mortar;
                        }
                        break;
                    case 2:
                        next = new ScrapMagnetStormState();
                        break;
                    case 3:
                        next = new ScrapViceSnatchState();
                        ctx.QueuedChainState = (int)ScrapStateIndex.HeadSwing;
                        break;
                    case 4:
                        next = probes > 0 ? new ScrapAllOutCommandState() : new ScrapLaserSweepState();
                        break;
                    case 5:
                        next = new ScrapSawCannonComboState();
                        break;
                    case 6:
                        next = new ScrapWaterfallState();
                        break;
                    case 7:
                        next = probes > 0 ? new ScrapLaserMatrixState() : new ScrapCrossSpinState();
                        break;
                    default:
                        //过载后：终局招隔轮压场
                        next = ctx.Phase >= 3 && (ctx.AttackIndex / 8) % 2 == 1
                            ? new ScrapFusedFrenzyState()
                            : new ScrapHeadSwingState();
                        break;
                }
                //单打手规则：军团满编时本体不再贴身肉搏，换成远程压制
                if (probes >= 2 && next is ScrapSawLaunchState or ScrapViceSnatchState
                    or ScrapHeadSwingState or ScrapCrossSpinState) {
                    ctx.QueuedChainState = -1;
                    next = new ScrapLaserSweepState();
                }
                return next;
            }

            switch (ctx.AttackIndex % 6) {
                case 1: {
                    ctx.QueuedChainState = (int)ScrapStateIndex.Mortar;
                    return new ScrapSawLaunchState();
                }
                case 2: {
                    ctx.QueuedChainState = (int)ScrapStateIndex.HeadSwing;
                    return new ScrapViceSnatchState();
                }
                case 3:
                    return new ScrapMortarState();
                case 4:
                    return new ScrapCrossSpinState();
                case 5:
                    return new ScrapLaserSweepState();
                default:
                    return new ScrapHeadSwingState();
            }
        }
    }
}

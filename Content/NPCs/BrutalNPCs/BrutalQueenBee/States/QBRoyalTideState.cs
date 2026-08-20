using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.States
{
    /// <summary>
    /// 低血大招·蜂潮终曲(≤25%一次)：全群聚成蜂潮长矛，女王领矛三段贯穿冲锋，<br/>
    /// 沿途两侧撒毒刺；终段全矛甩鞭放镖，随后力竭喘息(奖励窗口)<br/>
    /// npc.ai[0]=本段航向角 npc.ai[1]=起点X npc.ai[3]=起点Y(服务端掷骰)
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenBeeStateIndex.RoyalTide, typeof(QueenBeeStateContext))]
    internal class QBRoyalTideState : QueenBeeStateBase
    {
        public override string StateName => "RoyalTide";
        public override QueenBeeStateIndex StateIndex => QueenBeeStateIndex.RoyalTide;

        #region 节奏常量
        private const int GatherTime = 80;   //聚矛蓄力(补员与升调轰鸣同步进行)
        private const int TelegraphTime = 42;
        private const int ChargeTime = 32;
        private const int LoopTime = 34;
        private const int LegTime = TelegraphTime + ChargeTime + LoopTime;
        private const int FinaleTime = 66;   //甩鞭+力竭(奖励输出窗口)
        /// <summary>公平阀：冲锋沿途两侧撒刺的间隔帧，成对垂直出射留出棋盘状穿越缝</summary>
        private const int SideSpitInterval = 6;
        //公平阀：每段航向在段首一次掷定(ai[0])后整段锁死不再跟踪，预警线全程42帧；
        //回环拍女王与矛体接触伤关闭
        #endregion

        private int ChargeCount(QueenBeeStateContext context) => context.IsDeathMode ? 4 : 3;

        public override void OnEnter(QueenBeeStateContext context) {
            base.OnEnter(context);
            QueenBeeMotion.RoarBurst(context.Npc.Center, 1.35f);
        }

        public override IQueenBeeState OnUpdate(QueenBeeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //幕一 聚矛：全群归矛，急速补员到满编
            if (Timer <= GatherTime) {
                Vector2 gatherPos = player.Center + new Vector2(0f, -420f);
                QueenBeeMotion.SpringHover(npc, gatherPos, 0.013f, 0.1f, 22f);
                FaceTarget(npc, player.Center);

                Vector2 aim = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                context.Swarm.Declare(SwarmFormation.Lance, npc.Center, aim);
                context.Swarm.PushSnap(2.3f);
                float p = Timer / (float)GatherTime;
                context.Swarm.PushSignal(0.5f + p * 0.5f);
                context.SetChargeState(4, p);
                QueenBeeMotion.ChargeGatherFX(npc.Center, p, 150f);

                if (!VaultUtils.isClient && Timer % 10 == 0) {
                    context.Swarm.ServerTopUp(SwarmDirector.MaxBees - 4, 3);
                }
                //升调轰鸣，末14帧静默
                if (p < 0.72f && Timer % 20 == 0) {
                    QueenBeeMotion.WingHum(npc.Center, 0.4f + p * 0.35f, -0.3f + p * 0.9f);
                }
                return null;
            }

            //幕二 三段贯穿冲锋
            int legTimer = Timer - GatherTime - 1;
            int leg = legTimer / LegTime;
            int legT = legTimer % LegTime;

            if (leg < ChargeCount(context)) {
                UpdateChargeLeg(context, npc, player, legT);
                return null;
            }

            //幕三 甩鞭+力竭
            int finaleT = legTimer - ChargeCount(context) * LegTime;
            if (finaleT == 0) {
                //全矛甩鞭：所有蜂沿末段航向放镖越过女王
                Vector2 dir = npc.ai[0].ToRotationVector2();
                context.Swarm.LaunchDarts(0, SwarmDirector.MaxBees - 1, dir, 34f, 4);
                QueenBeeMotion.AmberBoom(npc.Center, dir, 1.3f);
                QueenBeeMotion.Shake(npc.Center, 7f, 14);
                QueenBeeMotion.BrakeHard(npc, 0.4f);
            }

            //力竭喘息：奖励输出窗口，缓慢飘摇
            npc.velocity *= 0.94f;
            npc.velocity.Y += (float)Math.Sin(Timer * 0.1f) * 0.04f;
            FaceTarget(npc, player.Center);
            DisableContactDamage(npc);
            context.Swarm.PushSignal(0.2f);
            if (finaleT % 22 == 10) {
                QueenBeeMotion.WingHum(npc.Center, 0.3f, -0.7f);
            }

            if (finaleT >= FinaleTime) {
                return new QBRepositionState();
            }
            return null;
        }

        /// <summary>单段冲锋：预警线→就位反向蓄势→领矛贯穿→宽弧回环</summary>
        private void UpdateChargeLeg(QueenBeeStateContext context, NPC npc, Player player, int legT) {
            //段首掷航线
            if (legT == 0) {
                if (!VaultUtils.isClient) {
                    Vector2 predicted = QueenBeeMotion.PredictTarget(player, npc.Center, 42f, 0.75f);
                    Vector2 dir = (predicted - npc.Center).SafeNormalize(Vector2.UnitY);
                    Vector2 start = predicted - dir * 880f;
                    npc.ai[0] = dir.ToRotation();
                    npc.ai[1] = start.X;
                    npc.ai[3] = start.Y;
                    npc.netUpdate = true;

                    Projectile.NewProjectile(npc.GetSource_FromAI(), start, dir,
                        ModContent.ProjectileType<QueenBeeTelegraphLine>(), 0, 0f, Main.myPlayer,
                        -1, -1, QueenBeeTelegraphLine.PackParams(0, TelegraphTime));
                }
                SoundEngine.PlaySound(SoundID.Zombie125 with { Volume = 0.7f, Pitch = -0.3f, MaxInstances = 2 }, player.Center);
            }

            Vector2 chargeDir = npc.ai[0].ToRotationVector2();
            Vector2 chargeStart = new Vector2(npc.ai[1], npc.ai[3]);

            //预警拍：矛头就位，末段反向吸气
            if (legT < TelegraphTime) {
                float t = legT / (float)TelegraphTime;
                Vector2 target = legT < TelegraphTime - 12
                    ? chargeStart
                    : chargeStart - chargeDir * (float)Math.Pow((legT - (TelegraphTime - 12)) / 12f, 8f) * 110f;
                QueenBeeMotion.SpringHover(npc, target, 0.045f, 0.16f, 48f);
                context.SetChargeState(4, t);
                context.UseChargePose = legT > TelegraphTime - 16;
                FaceTarget(npc, chargeStart + chargeDir * 200f);

                context.Swarm.Declare(SwarmFormation.Lance, npc.Center, chargeDir);
                context.Swarm.PushSnap(2.6f);
                context.Swarm.PushSignal(0.95f);
                return;
            }

            //冲锋发射帧
            if (legT == TelegraphTime) {
                float speed = 42f + (context.IsDeathMode ? 3f : 0f);
                QueenBeeMotion.DashLaunch(npc, chargeDir, speed, 1.35f);
                QueenBeeMotion.Shake(npc.Center, 6f, 12);
            }

            //冲锋拍：矛体全速咬合，两侧撒刺
            if (legT < TelegraphTime + ChargeTime) {
                context.UseChargePose = true;
                context.PushAfterimage(1f);
                EnableContactDamageIfFast(npc, 24f);
                FaceByVelocity(npc);

                context.Swarm.Declare(SwarmFormation.Lance, npc.Center, chargeDir);
                context.Swarm.PushSnap(2.8f);
                context.Swarm.PushSignal(1f);

                if (!VaultUtils.isClient && legT % SideSpitInterval == 0) {
                    Vector2 perp = chargeDir.RotatedBy(MathHelper.PiOver2);
                    for (int s = -1; s <= 1; s += 2) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                            perp * s * 3.8f + chargeDir * 2.5f,
                            ModContent.ProjectileType<BrutalBeeStinger>(), BrutalBeeStinger.BaseDamage,
                            0f, Main.myPlayer, 1f);
                    }
                }
                return;
            }

            //回环拍：宽弧转向下一段起手，不造成伤害
            DisableContactDamage(npc);
            float curSpeed = Math.Max(npc.velocity.Length() * 0.955f, 17f);
            QueenBeeMotion.CurveChase(npc, player.Center + new Vector2(0f, -300f), curSpeed, 0.052f);
            FaceByVelocity(npc);
            context.Swarm.Declare(SwarmFormation.Lance, npc.Center,
                npc.velocity.SafeNormalize(Vector2.UnitX));
            context.Swarm.PushSnap(2.2f);
            context.Swarm.PushSignal(0.8f);
        }

        public override void OnExit(QueenBeeStateContext context) {
            base.OnExit(context);
            DisableContactDamage(context.Npc);
        }
    }
}

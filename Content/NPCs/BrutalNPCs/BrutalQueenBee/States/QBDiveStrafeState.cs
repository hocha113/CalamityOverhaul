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
    /// 俯冲扫射：多段折线航线——琥珀预警线→末段反向蓄势→一帧置位俯冲，沿途两侧撒重力毒刺幕<br/>
    /// npc.ai[0]=本段航向角 npc.ai[1]=航线中心X npc.ai[3]=航线中心Y(服务端掷骰)
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenBeeStateIndex.DiveStrafe, typeof(QueenBeeStateContext))]
    internal class QBDiveStrafeState : QueenBeeStateBase
    {
        public override string StateName => "DiveStrafe";
        public override QueenBeeStateIndex StateIndex => QueenBeeStateIndex.DiveStrafe;

        #region 节奏常量
        private const int TelegraphTime = 38;
        private const int DiveTime = 32;
        private const int BrakeTime = 14;
        private const int SegmentTime = TelegraphTime + DiveTime + BrakeTime;
        #endregion

        private bool boomFired;

        private int SegmentCount(QueenBeeStateContext context) =>
            context.IsPhase2 || context.IsDeathMode ? 3 : 2;

        public override IQueenBeeState OnUpdate(QueenBeeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            int segT = Timer % SegmentTime;
            int segment = Timer / SegmentTime;
            Timer++;

            if (segment >= SegmentCount(context)) {
                return new QBRepositionState();
            }

            //段首：服务端掷航线(折线拐角靠左右交替偏角)
            if (segT == 0) {
                boomFired = false;
                if (!VaultUtils.isClient) {
                    int side = segment % 2 == 0 ? 1 : -1;
                    float tilt = side * Main.rand.NextFloat(0.32f, 0.62f);
                    Vector2 dir = Vector2.UnitY.RotatedBy(tilt);
                    //偶数段自上而下，奇数段自下而上回穿
                    if (segment % 2 == 1) {
                        dir = -dir;
                    }
                    Vector2 lineCenter = QueenBeeMotion.PredictTarget(player, npc.Center, 34f, 0.65f);
                    npc.ai[0] = dir.ToRotation();
                    npc.ai[1] = lineCenter.X;
                    npc.ai[3] = lineCenter.Y;
                    npc.netUpdate = true;

                    Projectile.NewProjectile(npc.GetSource_FromAI(), lineCenter - dir * 2100f, dir,
                        ModContent.ProjectileType<QueenBeeTelegraphLine>(), 0, 0f, Main.myPlayer,
                        -1, -1, QueenBeeTelegraphLine.PackParams(0, TelegraphTime));
                }
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.55f, Pitch = -0.35f, MaxInstances = 3 }, player.Center);
            }

            Vector2 diveDir = npc.ai[0].ToRotationVector2();
            Vector2 center = new Vector2(npc.ai[1], npc.ai[3]);

            //预警拍：赶到航线起点，末段反向蓄势
            if (segT < TelegraphTime) {
                Vector2 startPos = center - diveDir * 820f;
                float t = segT / (float)TelegraphTime;

                if (segT < TelegraphTime - 10) {
                    QueenBeeMotion.SpringHover(npc, startPos, 0.03f, 0.12f, 44f);
                }
                else {
                    //反向吸气：pow(t,8)末帧猛拉
                    float reel = (float)Math.Pow((segT - (TelegraphTime - 10)) / 10f, 8f);
                    QueenBeeMotion.SpringHover(npc, startPos - diveDir * (60f + reel * 90f), 0.06f, 0.2f, 46f);
                    context.UseChargePose = true;
                }
                context.SetChargeState(1, t);
                FaceTarget(npc, center);
                //蜂群拉开高位光环，让出舞台
                context.Swarm.Declare(SwarmFormation.Halo, player.Center + new Vector2(0f, -420f), Vector2.UnitX, 1.25f);
                context.Swarm.PushRibbon(0.3f);
                return null;
            }

            //俯冲发射帧
            if (segT == TelegraphTime) {
                float speed = 34f + context.EnrageScale * 2f + (context.IsDeathMode ? 4f : 0f);
                QueenBeeMotion.DashLaunch(npc, diveDir, speed, 1.15f);
                QueenBeeMotion.RoarBurst(npc.Center, 0.7f);
            }

            //俯冲拍
            if (segT < TelegraphTime + DiveTime) {
                context.UseChargePose = true;
                context.PushAfterimage(1f);
                EnableContactDamageIfFast(npc, 20f);
                FaceByVelocity(npc);
                context.Swarm.Declare(SwarmFormation.Halo, player.Center + new Vector2(0f, -420f), Vector2.UnitX, 1.25f);

                //两侧毒刺幕(重力坠落)：每5帧一对
                if (!VaultUtils.isClient && segT % 5 == 0) {
                    Vector2 perp = diveDir.RotatedBy(MathHelper.PiOver2);
                    float curtainSpeed = 3.4f + context.EnrageScale * 0.5f;
                    for (int s = -1; s <= 1; s += 2) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                            perp * s * curtainSpeed + diveDir * 2f,
                            ModContent.ProjectileType<BrutalBeeStinger>(), BrutalBeeStinger.BaseDamage,
                            0f, Main.myPlayer, 1f);
                    }
                }

                //穿过航线中心的冲击拍
                if (!boomFired && Vector2.Dot(npc.Center - center, diveDir) > 0f) {
                    boomFired = true;
                    QueenBeeMotion.Shake(center, 5f, 10);
                    QueenBeeMotion.HoneyBurst(npc.Center, 1f, 6, false);
                }

                //远远越过后提前进入刹车
                if (Vector2.Dot(npc.Center - center, diveDir) > 950f) {
                    Timer = segment * SegmentTime + TelegraphTime + DiveTime;
                }
                return null;
            }

            //刹车拍
            QueenBeeMotion.BrakeHard(npc, 0.74f);
            DisableContactDamage(npc);
            FaceTarget(npc, player.Center);
            return null;
        }

        public override void OnExit(QueenBeeStateContext context) {
            base.OnExit(context);
            DisableContactDamage(context.Npc);
        }
    }
}

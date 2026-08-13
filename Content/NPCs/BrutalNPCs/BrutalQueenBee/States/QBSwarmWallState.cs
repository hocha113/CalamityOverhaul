using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.States
{
    /// <summary>
    /// 蜂墙横扫(二阶段)：蜂群在一侧拼出带两道缝的活体墙→定格亮缝→横扫过场；<br/>
    /// 女王在对侧放慢速毒刺把玩家往墙里推<br/>
    /// npc.ai[0]=墙side npc.ai[1]=缝位打包(gapA+gapB*100) npc.ai[3]=扫掠锚Y
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenBeeStateIndex.SwarmWall, typeof(QueenBeeStateContext))]
    internal class QBSwarmWallState : QueenBeeStateBase
    {
        public override string StateName => "SwarmWall";
        public override QueenBeeStateIndex StateIndex => QueenBeeStateIndex.SwarmWall;

        #region 节奏常量
        private const int FormTime = 56;    //拼墙
        private const int HoldTime = 40;    //定格亮缝
        private const int SweepTime = 118;  //横扫
        private const float SweepSpeed = 13f;
        private const float WallDistance = 640f;
        #endregion

        private float sweepStartX;
        private float wallY;

        public override void OnEnter(QueenBeeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            wallY = context.Target.Center.Y;
            if (!VaultUtils.isClient) {
                int side = Main.rand.NextBool() ? 1 : -1;
                //两道缝：上三分之一与下三分之一附近掷骰
                int gapA = Main.rand.Next(3, 7);
                int gapB = Main.rand.Next(10, 15);
                npc.ai[0] = side;
                npc.ai[1] = gapA + gapB * 100;
                npc.netUpdate = true;
            }
        }

        public override IQueenBeeState OnUpdate(QueenBeeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int side = npc.ai[0] >= 0f ? 1 : -1;
            float gapPack = npc.ai[1];

            Timer++;

            //墙Y软跟踪玩家(慢速，可用垂直走位甩开)
            wallY = MathHelper.Lerp(wallY, player.Center.Y, 0.018f);

            //女王站对侧压制位
            Vector2 queenPos = player.Center + new Vector2(-side * 460f, -180f);
            QueenBeeMotion.SpringHover(npc, queenPos, 0.015f, 0.1f, 26f);
            FaceTarget(npc, player.Center);

            //拼墙拍
            if (Timer <= FormTime) {
                Vector2 anchor = new Vector2(player.Center.X + side * WallDistance, wallY);
                context.Swarm.Declare(SwarmFormation.Wall, anchor, new Vector2(-side, 0f), 1f, gapPack);
                context.Swarm.PushRibbon(0.35f + Timer / (float)FormTime * 0.4f);
                if (Timer == 1) {
                    context.Swarm.PushSnap(2.1f);
                    QueenBeeMotion.WingHum(player.Center, 0.5f, -0.3f);
                }
                return null;
            }

            //定格拍：亮缝警告
            if (Timer <= FormTime + HoldTime) {
                Vector2 anchor = new Vector2(player.Center.X + side * WallDistance, wallY);
                context.Swarm.Declare(SwarmFormation.Wall, anchor, new Vector2(-side, 0f), 1f, gapPack);
                context.Swarm.PushRibbon(0.95f);

                if (Timer == FormTime + 8) {
                    SoundEngine.PlaySound(SoundID.Zombie125 with { Volume = 0.6f, Pitch = 0.3f }, npc.Center);
                }
                //定格末帧记录扫掠起点
                if (Timer == FormTime + HoldTime) {
                    sweepStartX = player.Center.X + side * WallDistance;
                }
                return null;
            }

            //横扫拍
            int sweepT = Timer - FormTime - HoldTime;
            if (sweepT <= SweepTime) {
                //起点兜底(客户端可能错过定格末帧)
                if (sweepStartX == 0f) {
                    sweepStartX = player.Center.X + side * WallDistance;
                }
                float x = sweepStartX - side * SweepSpeed * sweepT;
                Vector2 anchor = new Vector2(x, wallY);
                context.Swarm.Declare(SwarmFormation.Wall, anchor, new Vector2(-side, 0f), 1f, gapPack);
                context.Swarm.PushSnap(1.5f);
                context.Swarm.PushRibbon(0.85f);

                //女王对侧慢速毒刺推人
                if (sweepT % 32 == 10) {
                    Vector2 muzzle = npc.Center + new Vector2(0f, npc.height * 0.32f);
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.6f, Pitch = -0.2f }, muzzle);
                    if (!VaultUtils.isClient) {
                        Vector2 vel = (player.Center - muzzle).SafeNormalize(Vector2.UnitY) * 6f;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, vel,
                            ModContent.ProjectileType<BrutalBeeStinger>(), BrutalBeeStinger.BaseDamage, 0f, Main.myPlayer, 0f);
                    }
                }
                return null;
            }

            //死亡模式回马枪：反向再扫一趟(更快)
            if (context.IsDeathMode && Counter == 0) {
                Counter = 1;
                //重置到定格拍末，方向取反
                if (!VaultUtils.isClient) {
                    npc.ai[0] = -side;
                    npc.netUpdate = true;
                }
                sweepStartX = 0f;
                Timer = FormTime + HoldTime - 12;
                return null;
            }

            return new QBRepositionState();
        }
    }
}

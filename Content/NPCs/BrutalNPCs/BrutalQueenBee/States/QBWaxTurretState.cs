using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.States
{
    /// <summary>
    /// 蜂巢炮台布设(二阶段)：女王双段疾掠到玩家两翼定点吐出蜂巢炮台，<br/>
    /// 炮台在其后诸招中持续输出定点威胁；蜂群箭形护航
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenBeeStateIndex.WaxTurret, typeof(QueenBeeStateContext))]
    internal class QBWaxTurretState : QueenBeeStateBase
    {
        public override string StateName => "WaxTurret";
        public override QueenBeeStateIndex StateIndex => QueenBeeStateIndex.WaxTurret;

        #region 节奏常量
        private const int SwoopTime = 52;   //单段掠位
        private const int PlantPad = 14;    //布设停顿
        private const int RetreatTime = 24; //收势
        private const int SegTime = SwoopTime + PlantPad;
        //护航箭中段顺势甩镖帧：掠位途中不再是零威胁真空
        private const int EscortDartFrame = 30;
        //甩镖前黄描边预警窗(只标即将出手的两只)
        private const int DartWarnLead = 18;
        #endregion

        /// <summary>公平阀：场上同源炮台上限，超限不再布设，定点威胁总量有顶</summary>
        private const int TurretCap = 4;

        public override IQueenBeeState OnUpdate(QueenBeeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            int seg = Timer / SegTime;
            int segT = Timer % SegTime;
            Timer++;

            //两段掠位后收势退出
            if (seg >= 2) {
                QueenBeeMotion.SpringHover(npc, player.Center + new Vector2(0f, -330f), 0.016f, 0.1f, 26f);
                FaceTarget(npc, player.Center);
                context.Swarm.PushSignal(0.3f);
                if (Timer >= 2 * SegTime + RetreatTime) {
                    return new QBRepositionState();
                }
                return null;
            }

            int side = seg == 0 ? 1 : -1;
            Vector2 plantPos = player.Center + new Vector2(side * 430f, -150f);

            //疾掠拍
            if (segT < SwoopTime) {
                QueenBeeMotion.SpringHover(npc, plantPos, 0.028f, 0.11f, 38f);
                FaceByVelocity(npc);
                context.PushAfterimage(0.5f);
                //蜂群箭形护航沿速度向
                Vector2 vel = npc.velocity.LengthSquared() > 4f
                    ? npc.velocity.SafeNormalize(Vector2.UnitX)
                    : Vector2.UnitX * side;
                context.Swarm.Declare(SwarmFormation.Arrow, npc.Center - vel * 60f, vel);
                context.Swarm.PushSignal(0.5f);
                //出手前两只预警读秒
                int dartIn = EscortDartFrame - segT;
                if (dartIn > 0 && dartIn <= DartWarnLead) {
                    context.Swarm.WarnDarts(1, 2, 1f - dartIn / (float)DartWarnLead);
                }
                //护航箭中段顺势甩两镖(沿飞行向直射，不追踪)
                if (segT == EscortDartFrame) {
                    context.Swarm.LaunchDarts(1, 2, vel, 22f, 0);
                }
                return null;
            }

            //布设帧
            if (segT == SwoopTime) {
                QueenBeeMotion.BrakeHard(npc, 0.6f);
                npc.velocity -= Vector2.UnitY * 2f;
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.8f, Pitch = -0.5f }, npc.Center);
                QueenBeeMotion.HoneyBurst(npc.Center + new Vector2(0f, npc.height * 0.3f), 1.1f, 8);

                if (!VaultUtils.isClient && CountTurrets() < TurretCap) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        npc.Center + new Vector2(0f, npc.height * 0.4f), Vector2.Zero,
                        ModContent.ProjectileType<WaxHiveTurret>(), 0, 0f, Main.myPlayer,
                        context.IsDeathMode ? 1f : 0f);
                }
                return null;
            }

            //停顿拍
            QueenBeeMotion.BrakeHard(npc, 0.86f);
            FaceTarget(npc, player.Center);
            return null;
        }

        /// <summary>场上活跃炮台计数</summary>
        private static int CountTurrets() {
            int count = 0;
            int type = ModContent.ProjectileType<WaxHiveTurret>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == type) {
                    count++;
                }
            }
            return count;
        }
    }
}

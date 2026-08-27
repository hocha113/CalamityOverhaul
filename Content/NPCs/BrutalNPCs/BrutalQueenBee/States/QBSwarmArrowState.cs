using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.States
{
    /// <summary>
    /// 蜂群箭矢：编队拼出箭形跟踪→定格锁向(可读拍)→尖端先行分波掷镖→回巢重整<br/>
    /// npc.ai[0]=锁定弹道角(服务端在定格帧掷骰)
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenBeeStateIndex.SwarmArrow, typeof(QueenBeeStateContext))]
    internal class QBSwarmArrowState : QueenBeeStateBase
    {
        public override string StateName => "SwarmArrow";
        public override QueenBeeStateIndex StateIndex => QueenBeeStateIndex.SwarmArrow;

        #region 节奏常量
        private const int TrackTime = 34;   //拼箭跟踪(阵型成型本身即前摇，不额外加等待)
        private const int FreezeTime = 14;  //锁向定格
        private const int LaunchTime = 12;  //分波出镖
        private const int RecoverTime = 26; //回巢重整
        private const int CycleTime = TrackTime + FreezeTime + LaunchTime + RecoverTime;
        //冲刺预警提前量：跟踪拍末尾即起黄描边，与定格拍连成24帧读秒
        private const int DartWarnLead = 10;
        /// <summary>公平阀：定格帧后弹道锁死；镖仅出手初段微弧修正(≤该帧数×0.03rad/帧)，持续横移即可甩脱</summary>
        private const int MaxDartSteerFrames = 12;
        #endregion

        private int MaxVolleys(QueenBeeStateContext context) =>
            context.IsPhase2 || context.IsDeathMode ? 3 : 2;

        public override IQueenBeeState OnUpdate(QueenBeeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            int cycleT = Timer % CycleTime;
            int volley = Timer / CycleTime;
            Timer++;

            if (volley >= MaxVolleys(context)) {
                return new QBRepositionState();
            }

            //女王居后指挥位
            Vector2 aimLive = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
            Vector2 commandPos = player.Center - aimLive * 470f + new Vector2(0f, -120f);
            QueenBeeMotion.SpringHover(npc, commandPos, 0.013f, 0.095f, 24f);
            FaceTarget(npc, player.Center);

            //跟踪拍：箭阵在女王前方缓慢咬向玩家
            if (cycleT < TrackTime) {
                context.Swarm.Declare(SwarmFormation.Arrow, npc.Center + aimLive * 130f, aimLive);
                float build = cycleT / (float)TrackTime;
                context.Swarm.PushSignal(0.35f + build * 0.45f);
                if (cycleT == 0) {
                    context.Swarm.PushSnap(1.9f);
                    QueenBeeMotion.WingHum(npc.Center, 0.4f, -0.1f);
                }
                //末尾起黄描边预警(与定格拍连续读秒)
                if (cycleT >= TrackTime - DartWarnLead) {
                    context.Swarm.WarnDarts(0, SwarmDirector.MaxBees - 1,
                        (cycleT - (TrackTime - DartWarnLead)) / (float)(DartWarnLead + FreezeTime));
                }
                return null;
            }

            //定格帧：服务端锁弹道(带预判)
            if (cycleT == TrackTime) {
                if (!VaultUtils.isClient) {
                    Vector2 predicted = QueenBeeMotion.PredictTarget(player, npc.Center, 30f, 0.8f);
                    npc.ai[0] = (predicted - npc.Center).ToRotation();
                    npc.netUpdate = true;
                }
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.5f, Pitch = 0.7f }, npc.Center);
                QueenBeeMotion.Shake(npc.Center, 2.5f, 6);
            }

            Vector2 lockedAim = npc.ai[0].ToRotationVector2();

            //定格拍：箭阵僵住，辉光拉满(可读性阀门)
            if (cycleT < TrackTime + FreezeTime) {
                context.Swarm.Declare(SwarmFormation.Arrow, npc.Center + lockedAim * 130f, lockedAim);
                context.Swarm.PushSignal(1f);
                context.Swarm.WarnDarts(0, SwarmDirector.MaxBees - 1,
                    (cycleT - (TrackTime - DartWarnLead)) / (float)(DartWarnLead + FreezeTime));
                context.SetChargeState(4, (cycleT - TrackTime) / (float)FreezeTime);
                return null;
            }

            //出镖拍：尖端先行，成对分波
            if (cycleT < TrackTime + FreezeTime + LaunchTime) {
                context.Swarm.Declare(SwarmFormation.Arrow, npc.Center + lockedAim * 130f, lockedAim);
                context.Swarm.PushSignal(0.9f);
                int launchT = cycleT - TrackTime - FreezeTime;
                float dartSpeed = 30f + (context.IsDeathMode ? 4f : 0f) + context.EnrageScale * 1.5f;
                //未出手的波次维持满亮预警，出手即熄
                if (launchT < 3) {
                    context.Swarm.WarnDarts(1, SwarmDirector.MaxBees - 1, 1f);
                }
                else if (launchT < 6) {
                    context.Swarm.WarnDarts(5, SwarmDirector.MaxBees - 1, 1f);
                }
                else if (launchT < 9) {
                    context.Swarm.WarnDarts(9, SwarmDirector.MaxBees - 1, 1f);
                }
                //波次：尖(0)→前两对→中两对→尾
                if (launchT == 0) {
                    context.Swarm.LaunchDarts(0, 0, lockedAim, dartSpeed, MaxDartSteerFrames);
                    QueenBeeMotion.AmberBoom(npc.Center + lockedAim * 160f, lockedAim, 0.75f);
                }
                else if (launchT == 3) {
                    context.Swarm.LaunchDarts(1, 4, lockedAim, dartSpeed, 10);
                }
                else if (launchT == 6) {
                    context.Swarm.LaunchDarts(5, 8, lockedAim, dartSpeed, 8);
                }
                else if (launchT == 9) {
                    context.Swarm.LaunchDarts(9, SwarmDirector.MaxBees - 1, lockedAim, dartSpeed, 6);
                }
                return null;
            }

            //回巢拍：光环重整，女王补一记指挥毒刺(存在感)
            context.Swarm.PushSignal(0.3f);
            if (cycleT == TrackTime + FreezeTime + LaunchTime + 10) {
                Vector2 muzzle = npc.Center + new Vector2(0f, npc.height * 0.32f);
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.6f }, muzzle);
                if (!VaultUtils.isClient) {
                    Vector2 vel = (player.Center - muzzle).SafeNormalize(Vector2.UnitY) * 8f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, vel,
                        ModContent.ProjectileType<BrutalBeeStinger>(), BrutalBeeStinger.BaseDamage, 0f, Main.myPlayer, 0f);
                }
            }
            return null;
        }
    }
}

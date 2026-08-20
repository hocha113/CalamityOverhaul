using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.States
{
    /// <summary>
    /// 入场演出：蜜雾现身低鸣→仰身咆哮螺旋放蜂→光环阅兵式收拢→箭阵直指→浅掠警告俯冲<br/>
    /// 编队是主角：第一幕就把"这群蜂听她号令"立起来
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenBeeStateIndex.Intro, typeof(QueenBeeStateContext))]
    internal class QBIntroState : QueenBeeStateBase
    {
        public override string StateName => "Intro";
        public override QueenBeeStateIndex StateIndex => QueenBeeStateIndex.Intro;

        #region 节奏常量
        private const int HushEnd = 40;        //蜜雾低鸣
        private const int SummonEnd = 78;      //咆哮+螺旋放蜂
        private const int ParadeEnd = 118;     //光环阅兵脉冲
        private const int AimEnd = 148;        //箭阵直指压场
        private const int SwoopEnd = 176;      //浅掠警告
        #endregion

        private int Side(QueenBeeStateContext context) => context.Npc.whoAmI % 2 == 0 ? 1 : -1;

        public override void OnEnter(QueenBeeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            npc.dontTakeDamage = false;
            //现身蜜雾+低鸣
            QueenBeeMotion.HoneyBurst(npc.Center, 1.5f, 14, false);
            QueenBeeMotion.WingHum(npc.Center, 0.6f, -0.55f);
        }

        public override IQueenBeeState OnUpdate(QueenBeeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int side = Side(context);

            Timer++;

            //幕一 低鸣悬停，缓缓压向玩家上方
            if (Timer <= HushEnd) {
                Vector2 lurk = player.Center + new Vector2(side * 300f, -340f);
                QueenBeeMotion.SpringHover(npc, lurk, 0.01f, 0.09f, 14f);
                FaceTarget(npc, player.Center);
                if (Timer % 14 == 0) {
                    QueenBeeMotion.WingHum(npc.Center, 0.3f + Timer / (float)HushEnd * 0.25f, -0.5f + Timer / (float)HushEnd * 0.3f);
                }
                if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_HoneyMist>(npc.Center + Main.rand.NextVector2Circular(46f, 34f),
                        Main.rand.NextVector2Circular(0.5f, 0.3f), QueenBeeMotion.HoneyGold * 0.35f,
                        Main.rand.NextFloat(0.6f, 1f));
                }
                return null;
            }

            //幕二 仰身咆哮，腹部螺旋吐蜂
            if (Timer <= SummonEnd) {
                //仰身反向蓄势
                npc.velocity *= 0.9f;
                npc.velocity.Y -= 0.12f;
                FaceTarget(npc, player.Center);

                if (Timer == HushEnd + 1) {
                    QueenBeeMotion.RoarBurst(npc.Center, 1.15f);
                }

                //服务端螺旋放蜂：每4帧2只，共16只
                if (!VaultUtils.isClient && Timer % 4 == 0) {
                    for (int i = 0; i < 2; i++) {
                        float angle = (Timer - HushEnd) * 0.55f + i * MathHelper.Pi;
                        Vector2 spawnPos = npc.Center + new Vector2(0f, npc.height * 0.3f)
                            + angle.ToRotationVector2() * 26f;
                        NPC bee = context.Swarm.SpawnFormationBee(spawnPos);
                        if (bee != null) {
                            bee.velocity = angle.ToRotationVector2() * 5f;
                        }
                    }
                    SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 4 }, npc.Center);
                }

                //吐蜂蜜雾
                if (!VaultUtils.isServer && Timer % 3 == 0) {
                    PRTLoader.NewParticle<PRT_HoneyMist>(npc.Center + new Vector2(0f, npc.height * 0.32f),
                        Main.rand.NextVector2Circular(1.2f, 0.7f), QueenBeeMotion.HoneyGold * 0.4f,
                        Main.rand.NextFloat(0.5f, 0.9f));
                }

                //光环随蜂数自然成形
                context.Swarm.Declare(SwarmFormation.Halo, npc.Center, Vector2.UnitX, 1.1f);
                context.Swarm.PushSignal(0.4f);
                return null;
            }

            //幕三 阅兵式：光环外扩→猛地收拢，一次脉冲立军纪
            if (Timer <= ParadeEnd) {
                Vector2 holdPos = player.Center + new Vector2(side * 240f, -330f);
                QueenBeeMotion.SpringHover(npc, holdPos, 0.014f, 0.1f, 18f);
                FaceTarget(npc, player.Center);

                float t = (Timer - SummonEnd) / (float)(ParadeEnd - SummonEnd);
                //先鼓出(1.45)再急收(0.85)：pow拉出"停顿-收拢"的呼吸
                float spread = t < 0.55f
                    ? MathHelper.Lerp(1.1f, 1.45f, t / 0.55f)
                    : MathHelper.Lerp(1.45f, 0.85f, (float)Math.Pow((t - 0.55f) / 0.45f, 0.4f));
                context.Swarm.Declare(SwarmFormation.Halo, npc.Center, Vector2.UnitX, spread);
                context.Swarm.PushSignal(0.75f);

                //收拢帧：snap提速+咔哒声
                if (Timer == SummonEnd + (int)((ParadeEnd - SummonEnd) * 0.55f)) {
                    context.Swarm.PushSnap(2.6f);
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.6f, Pitch = 0.6f }, npc.Center);
                    QueenBeeMotion.Shake(npc.Center, 3f, 8);
                }
                return null;
            }

            //幕四 箭阵直指玩家，威压定格
            if (Timer <= AimEnd) {
                Vector2 holdPos = player.Center + new Vector2(side * 260f, -320f);
                QueenBeeMotion.SpringHover(npc, holdPos, 0.012f, 0.11f, 15f);
                FaceTarget(npc, player.Center);

                Vector2 aim = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                context.Swarm.Declare(SwarmFormation.Arrow, npc.Center + aim * 130f, aim);
                context.Swarm.PushSignal(1f);
                if (Timer == ParadeEnd + 1) {
                    context.Swarm.PushSnap(2.2f);
                    SoundEngine.PlaySound(SoundID.Zombie125 with { Volume = 0.55f, Pitch = 0.25f }, npc.Center);
                }
                return null;
            }

            //幕五 浅掠警告俯冲(无伤)，擦着玩家侧掠过
            if (Timer <= SwoopEnd) {
                if (Timer == AimEnd + 1) {
                    Vector2 dir = (player.Center + new Vector2(-side * 130f, 60f) - npc.Center).SafeNormalize(Vector2.UnitY);
                    QueenBeeMotion.DashLaunch(npc, dir, 26f, 0.9f);
                }
                context.UseChargePose = true;
                context.PushAfterimage(0.8f);
                //蜂群跟枪成矛尾
                Vector2 vel = npc.velocity.SafeNormalize(Vector2.UnitX);
                context.Swarm.Declare(SwarmFormation.Lance, npc.Center, vel);
                context.Swarm.PushSnap(2.2f);
                context.Swarm.PushSignal(0.9f);
                FaceByVelocity(npc);

                if (Timer > SwoopEnd - 10) {
                    QueenBeeMotion.BrakeHard(npc, 0.82f);
                }
                return null;
            }

            return new QBRepositionState();
        }

        public override void OnExit(QueenBeeStateContext context) {
            base.OnExit(context);
        }
    }
}

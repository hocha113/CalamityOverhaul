using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States
{
    /// <summary>
    /// 阶段转换演出(≤55%血)：踉跄→仰天长啸→暴风雪整体升级，独眼永燃血色。
    /// 全程双向免伤(公平阀)，清场旧弹幕
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)DeerclopsStateIndex.PhaseRoar, typeof(DeerclopsStateContext))]
    internal class DeerclopsPhaseRoarState : DeerclopsStateBase
    {
        public override string StateName => "PhaseRoar";
        public override DeerclopsStateIndex StateIndex => DeerclopsStateIndex.PhaseRoar;

        private const int StaggerEnd = 26;
        private const int RoarFrame = 62;
        private const int RoarHoldEnd = 130;
        private const int StateEnd = 196;

        public override void OnEnter(DeerclopsStateContext context) {
            base.OnEnter(context);
            //公平阀：清掉全部在场攻势，给玩家重整拍
            DeerclopsAI.ClearHostileProjectiles();
        }

        public override IDeerclopsState OnUpdate(DeerclopsStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            context.HaltMovement = true;
            npc.damage = 0;
            npc.dontTakeDamage = true;

            //幕一：踉跄僵直(挨了半管血的重量)
            if (Timer <= StaggerEnd) {
                context.AnimMode = DeerAnimMode.Crouch;
                if (Timer == 4) {
                    DeerclopsMotion.CameraPunch(npc.Bottom, 4f, 14, "DeerPhaseStagger", Vector2.UnitY);
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.DeerclopsHit with { Volume = 1.2f, Pitch = -0.4f }, npc.Center);
                    }
                }
                return null;
            }

            //幕二：起身仰啸蓄势，冷芒收束入眼
            if (Timer <= RoarFrame) {
                context.AnimMode = DeerAnimMode.Roar;
                context.AnimTimer = (Timer - StaggerEnd) * 2 / 3;
                float p = (Timer - StaggerEnd) / (float)(RoarFrame - StaggerEnd);
                context.EyeGlow = p;
                context.EyeHeat = p;

                if (!Main.dedServ && Timer % 2 == 0) {
                    Vector2 eye = npc.Center + new Vector2(npc.spriteDirection * 20f, -60f);
                    Vector2 spawn = eye + Main.rand.NextVector2Unit() * Main.rand.NextFloat(80f, 260f);
                    Dust dust = Dust.NewDustPerfect(spawn, DustID.Frost, (eye - spawn) * 0.09f, 110, default, Main.rand.NextFloat(0.9f, 1.5f));
                    dust.noGravity = true;
                }
                return null;
            }

            //咆哮帧：位标置起，暴雪跳档
            if (Timer == RoarFrame + 1) {
                if (!VaultUtils.isClient) {
                    DeerclopsAI.SetFlag(npc, DeerclopsAI.FlagPhase2);
                }
                DeerclopsMotion.CameraPunch(npc.Center, 10f, 26, "DeerPhaseRoar");
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DeerclopsScream with { Volume = 1.3f, Pitch = -0.1f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.DeerclopsScream with { Volume = 0.7f, Pitch = -0.55f }, npc.Center);
                    //雪墙爆开
                    for (int i = 0; i < 40; i++) {
                        Dust dust = Dust.NewDustPerfect(npc.Center, DustID.Snow,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(5f, 14f), 60, default, Main.rand.NextFloat(1.4f, 2.4f));
                        dust.noGravity = true;
                    }
                }
            }

            //幕三：长啸维持，风雪压境
            if (Timer <= RoarHoldEnd) {
                context.AnimMode = DeerAnimMode.Roar;
                context.AnimTimer = 32 + (Timer - RoarFrame) / 3;
                context.EyeGlow = 1f;
                context.EyeHeat = 1f;
                context.VeilTarget = 1f;
                if (Timer % 6 == 0) {
                    DeerclopsMotion.CameraPunch(npc.Center, 2.6f, 10, "DeerPhaseRumble");
                }
                return null;
            }

            //幕四：收势(风雪落回二阶段常态)
            context.VeilTarget = 0.75f;
            if (Timer >= StateEnd) {
                //二阶段出招环从头开始(首招=冲撞，节奏陡变)
                context.AttackPhaseIndex = 0;
                return new DeerclopsStalkState();
            }
            return null;
        }

        public override void OnExit(DeerclopsStateContext context) {
            base.OnExit(context);
            context.Npc.dontTakeDamage = false;
        }
    }
}

using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States
{
    /// <summary>一→二阶段：狂化。后撤长啸，海应声立起，落雨开始</summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.PhaseTwoTransition, typeof(FishronStateContext))]
    internal class FishronPhaseTwoTransitionState : FishronStateBase
    {
        public override string StateName => "PhaseTwoTransition";
        public override FishronStateIndex StateIndex => FishronStateIndex.PhaseTwoTransition;
        public override bool AllowFarSnap => false;

        private const int BackdashEnd = 24;
        private const int RoarEnd = 74;
        private const int TotalTime = 124;

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            context.PhaseTwoStarted = true;
            //投技初始装填：入二阶段先打一轮常规循环，漩涡卷客不抢开场
            if (context.GrabCooldown < 600) {
                context.GrabCooldown = 600;
            }
            //公平阀：清场上气泡，走廊重置
            DukeFishronAI.ClearMinions(alsoTornado: false);
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.damage = 0;

            Timer++;

            //幕一：猛然后撤拉开舞台
            if (Timer <= BackdashEnd) {
                if (Timer == 1) {
                    Vector2 back = (npc.Center - player.Center).SafeNormalize(-Vector2.UnitY);
                    npc.velocity = back * 22f;
                    npc.netUpdate = true;
                }
                npc.velocity *= 0.93f;
                FaceBody(npc, player.Center, 0.15f);
                return null;
            }

            //幕二：长啸，海应声立起
            if (Timer <= RoarEnd) {
                npc.velocity *= 0.88f;
                FaceBody(npc, player.Center, 0.12f);
                context.FrameCommand = 1;
                context.SetChargeState(3, (Timer - BackdashEnd) / (float)(RoarEnd - BackdashEnd));

                if (Timer == BackdashEnd + 6) {
                    SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 1.25f, Pitch = -0.3f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.8f, Pitch = -0.35f, MaxInstances = 3 }, npc.Center);
                    FishronStormSky.PushFlash(0.7f, npc.Center);
                    FishronMotionFX.CameraPunch(npc.Center, 6f, 16, "FishronFrenzy");
                }
                //脚下海面立浪応和（纯视觉）
                if (!VaultUtils.isServer && Timer % 8 == 0) {
                    Vector2 surface = FishronMotionFX.FindSurfaceBelow(
                        npc.Center + new Vector2(Main.rand.NextFloat(-360f, 360f), 0f), out _);
                    FishronMotionFX.SpawnSplashBurst(surface, 0.8f, playSound: Main.rand.NextBool(3));
                }
                return null;
            }

            //幕三：压低身形逼近凝视，狂化的宣告
            npc.velocity *= 0.94f;
            Vector2 stare = player.Center + new Vector2(Math.Sign(npc.Center.X - player.Center.X) * 300f, -80f);
            npc.velocity = Vector2.Lerp(npc.velocity, (stare - npc.Center).SafeNormalize(Vector2.Zero) * 7f, 0.08f);
            FaceBody(npc, player.Center, 0.15f);

            //鳍梢开始渗电水花
            if (!VaultUtils.isServer && Timer % 5 == 0) {
                FishronMotionFX.SpawnSprayCone(npc.Center + Main.rand.NextVector2Circular(50f, 34f),
                    -Vector2.UnitY, 1, 1f, 3f, 0.4f, 0.7f);
            }

            if (Timer >= TotalTime) {
                //狂化开场三连突
                context.AttackRingIndex = 0;
                return new FishronTidalDashPrepareState();
            }
            return null;
        }

        public override void OnExit(FishronStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.Npc.damage = context.Npc.defDamage;
        }
    }

    /// <summary>二→三阶段：入夜雷暴。冲天离场，天黑透，随雷一同砸回来</summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.PhaseThreeTransition, typeof(FishronStateContext))]
    internal class FishronPhaseThreeTransitionState : FishronStateBase
    {
        public override string StateName => "PhaseThreeTransition";
        public override FishronStateIndex StateIndex => FishronStateIndex.PhaseThreeTransition;
        public override bool AllowFarSnap => false;

        private const int AscendEnd = 46;
        //末相预警线 -30%：落雷预告 52→36 帧（预告晚亮，落点帧不动）
        private const int TelegraphFrame = 76;
        private const int StrikeFrame = 112;
        private const int TotalTime = 206;

        private Vector2 strikePoint;
        private bool strikeResolved;

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            context.PhaseThreeStarted = true;
            strikeResolved = false;
            //入夜续命：三阶开幕回填 20% 上限血（服务端裁决，netUpdate 广播，绿字各端可见）
            if (!VaultUtils.isClient) {
                NPC npc = context.Npc;
                int heal = (int)(npc.lifeMax * 0.20f);
                npc.life = Math.Min(npc.life + heal, npc.lifeMax);
                npc.HealEffect(heal);
                npc.netUpdate = true;
            }
            //夜幕清场：气泡与龙卷都归于雨
            DukeFishronAI.ClearMinions(alsoTornado: true);
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.damage = 0;
            FishronStormSky.PushRainBoost(0.4f);

            Timer++;

            //幕一：冲天而去（拖着水尾直上云层）
            if (Timer <= AscendEnd) {
                npc.velocity = Vector2.Lerp(npc.velocity, -Vector2.UnitY * 38f, 0.1f);
                AimBodyAlongVelocity(npc);
                context.FrameCommand = 2;
                if (Timer == 6) {
                    SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 1.1f, Pitch = 0.25f }, npc.Center);
                }
                if (!VaultUtils.isServer && Timer % 2 == 0) {
                    FishronMotionFX.SpawnSprayCone(npc.Center, Vector2.UnitY, 2, 3f, 9f, 0.4f);
                }
                return null;
            }

            //幕二：他不在了，只剩雨，空场张力
            if (Timer < StrikeFrame) {
                npc.dontTakeDamage = true;
                npc.chaseable = false;
                npc.velocity = Vector2.Zero;
                //藏去玩家头顶极高处
                npc.Center = player.Center + new Vector2(0, -1600f);

                //确定性落点：玩家接近侧反方向 260px 的地面
                if (!strikeResolved) {
                    strikeResolved = true;
                    int side = Math.Sign(player.velocity.X);
                    if (side == 0) {
                        side = player.direction;
                    }
                    strikePoint = FishronMotionFX.FindSurfaceBelow(
                        player.Center + new Vector2(-side * 260f, -40f), out _);
                }

                if ((int)Timer == TelegraphFrame && !VaultUtils.isClient) {
                    //他就是那道雷：预告线立在落点
                    Projectile.NewProjectile(npc.GetSource_FromAI(), strikePoint, -Vector2.UnitY,
                        ModContent.ProjectileType<FishronTelegraph>(), 0, 0f, Main.myPlayer,
                        -1, -1, FishronTelegraph.PackParams(1, StrikeFrame - TelegraphFrame));
                }
                if (Timer % 20 == 0 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Thunder with {
                        Volume = 0.5f,
                        Pitch = -0.6f + Timer / (float)StrikeFrame * 0.4f,
                        MaxInstances = 3
                    }, player.Center);
                }
                return null;
            }

            //落雷帧：公爵与雷同落
            if ((int)Timer == StrikeFrame) {
                npc.Center = strikePoint - new Vector2(0, 70f);
                npc.velocity = Vector2.Zero;
                npc.dontTakeDamage = false;
                npc.chaseable = true;
                npc.netUpdate = true;

                FishronStormSky.PushFlash(1f, strikePoint);
                FishronMotionFX.SpawnSplashBurst(strikePoint, 2.4f);
                FishronMotionFX.CameraPunch(strikePoint, 13f, 20, "FishronNightfall", Vector2.UnitY);
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 1.2f, Pitch = 0.2f }, strikePoint);
                if (!VaultUtils.isServer) {
                    InnoVault.PRT.PRTLoader.NewParticle<CalamityOverhaul.Content.PRTTypes.PRT_SkyBolt>(
                        strikePoint, Vector2.Zero, FishronMotionFX.StormBolt, 1f)?
                        .Configure(strikePoint - new Vector2(0, 1100f), strikePoint, 30);
                }
                return null;
            }

            //幕三：从水坑里带电升起
            npc.velocity = Vector2.Lerp(npc.velocity, -Vector2.UnitY * 3.2f, 0.06f);
            FaceBody(npc, player.Center, 0.08f);
            if ((int)Timer == StrikeFrame + 40) {
                context.FrameCommand = 1;
                SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 1.2f, Pitch = -0.1f }, npc.Center);
            }

            if (Timer >= TotalTime) {
                //雷暴开环：风暴连突起手
                context.AttackRingIndex = 0;
                return new FishronHoverState();
            }
            return null;
        }

        public override void OnExit(FishronStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.Npc.dontTakeDamage = false;
            context.Npc.chaseable = true;
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}

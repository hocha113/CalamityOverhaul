using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.States
{
    /// <summary>
    /// 盲侧横贯（二阶段）：摆出原版经典的高位俯冲姿态骗预读→化雾消隐→自屏侧平线暴冲；<br/>
    /// 修罗模式第二轮把骗局再反转一次，真从头顶砸下。车道预警+入场雾是公平前摇
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EocStateIndex.BlindsideCross, typeof(EocStateContext))]
    internal class EocBlindsideCrossState : EocStateBase
    {
        public override string StateName => "EocBlindsideCross";
        public override EocStateIndex StateIndex => EocStateIndex.BlindsideCross;
        public override bool AllowFogStep => false;

        private enum CrossPhase
        {
            RiseFake,   //高位假姿态
            Vanish,     //化雾消隐+侧移
            LaneWarn,   //车道预警
            Cross,      //横贯
        }

        private const int RiseTime = 46;
        private const int VanishTime = 16;
        private const int WarnTime = 32;
        private const int CrossFlight = 27;
        private const int CrossBrake = 13;

        private int MaxReps => 2;
        private float CrossSpeed => Context.IsAsuraMode ? 63f : 57f;
        /// <summary>修罗模式第二轮改真俯冲</summary>
        private bool SecondRepIsDive => Context.IsAsuraMode;

        private EocStateContext Context;
        private CrossPhase phase;
        private int repIndex;
        private bool launched;
        private bool repositioned;

        public override void OnEnter(EocStateContext context) {
            base.OnEnter(context);
            Context = context;
            phase = CrossPhase.RiseFake;
            repIndex = 0;
            launched = false;
            repositioned = false;
        }

        public override IEocState OnUpdate(EocStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            DisableContactDamage(npc);

            switch (phase) {
                case CrossPhase.RiseFake:
                    UpdateRiseFake(npc, player, context);
                    break;
                case CrossPhase.Vanish:
                    UpdateVanish(npc, player, context);
                    break;
                case CrossPhase.LaneWarn:
                    UpdateLaneWarn(npc, player, context);
                    break;
                case CrossPhase.Cross:
                    return UpdateCross(npc, player, context);
            }

            return null;
        }

        private void SwitchPhase(CrossPhase next) {
            phase = next;
            Timer = 0;
        }

        private void UpdateRiseFake(NPC npc, Player player, EocStateContext context) {
            //经典高位俯冲姿态：正上方压顶，瞳孔死盯脚下
            Vector2 fakePoint = player.Center + new Vector2(36f, -440f);
            EocMotion.SpringHover(npc, fakePoint, 0.022f, 0.1f, 28f);
            FaceTarget(npc, player.Center, 0.5f);

            float progress = Timer / (float)RiseTime;
            context.SetChargeState(1, progress);
            context.PushIris(progress * 0.9f, EocMotion.IrisRed);

            //末段下压蓄势，把"要俯冲了"卖足
            if (progress > 0.7f) {
                npc.velocity.Y += 0.6f;
                if (!VaultUtils.isServer) {
                    npc.position += Main.rand.NextVector2Circular(1.4f, 1.4f);
                }
            }
            if (Timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Volume = 0.7f, Pitch = -0.3f }, npc.Center);
            }

            Timer++;
            if (Timer >= RiseTime) {
                EocMotion.FeintBlink(npc, context);
                repositioned = false;
                SwitchPhase(CrossPhase.Vanish);
            }
        }

        private void UpdateVanish(NPC npc, Player player, EocStateContext context) {
            float progress = Timer / (float)VanishTime;
            context.FogHideGoal = 1f;
            context.ScalePulse = 1f - 0.16f * progress;
            npc.velocity *= 0.7f;

            if (Timer == 2) {
                EocMotion.MistPuff(npc.Center, 7, 1.6f, 0.6f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.8f, Pitch = -0.55f }, npc.Center);
                }
            }

            //中点瞬移到出手位（权威端），旧位与新位都有雾掩护
            if (Timer == VanishTime / 2 && !repositioned) {
                repositioned = true;
                bool dive = SecondRepIsDive && repIndex == 1;
                if (!VaultUtils.isClient) {
                    Vector2 attackPos;
                    if (dive) {
                        //反转的反转：真从头顶
                        attackPos = player.Center + new Vector2(Main.rand.NextFloat(-40f, 40f), -720f);
                    }
                    else {
                        float side = Main.rand.NextBool() ? -1f : 1f;
                        attackPos = player.Center + new Vector2(side * 780f, Main.rand.NextFloat(-50f, 30f));
                    }
                    npc.Center = attackPos;
                    npc.velocity = Vector2.Zero;
                    npc.netUpdate = true;
                }
                EocMotion.MistPuff(npc.Center, 6, 1.5f, 0.55f);
            }

            Timer++;
            if (Timer >= VanishTime) {
                SwitchPhase(CrossPhase.LaneWarn);
            }
        }

        private void UpdateLaneWarn(NPC npc, Player player, EocStateContext context) {
            float progress = Timer / (float)WarnTime;
            //缓慢显形是最后一重读法
            context.FogHideGoal = MathHelper.Clamp(0.85f - progress, 0.15f, 1f);
            npc.velocity *= 0.85f;
            FaceTarget(npc, player.Center, 0.6f);

            //车道预警：横贯线（或俯冲线）
            Vector2 aimDir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitX);
            context.LaneIntensity = 0.5f + progress * 0.5f;
            context.LaneStart = npc.Center;
            context.LaneDir = aimDir;
            context.LaneLength = 1750f;
            context.LaneProgress = progress;
            context.SetChargeState(1, progress);
            context.PushIris(progress, EocMotion.IrisRed);

            if (Timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.9f, Pitch = -0.5f }, npc.Center);
            }
            //升调嘶声
            if (Timer % 8 == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie2 with { Volume = 0.35f, Pitch = -0.4f + progress * 0.7f }, npc.Center);
            }

            Timer++;
            if (Timer >= WarnTime) {
                launched = false;
                SwitchPhase(CrossPhase.Cross);
            }
        }

        private IEocState UpdateCross(NPC npc, Player player, EocStateContext context) {
            if (!launched) {
                launched = true;
                context.FogHideGoal = 0f;
                context.FogHide = 0.2f;
                Vector2 dir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitX)
                    .RotatedBy(Main.rand.NextFloat(-0.06f, 0.06f));
                if (!VaultUtils.isClient) {
                    EocMotion.DashLaunch(npc, context, dir, CrossSpeed, 1.3f);
                    npc.netUpdate = true;
                }
                else {
                    EocMotion.DashLaunch(npc, context, dir, CrossSpeed, 1.3f);
                }
                EocMotion.Shake(npc.Center, 6.5f, 12, dir);
            }

            FaceVelocity(npc);
            EnableContactDamageIfFast(npc, 28f, 1.3f);
            context.PushDashVisuals(1f, 1f);

            Timer++;
            if (Timer > CrossFlight) {
                npc.velocity *= 0.72f;
                EocMotion.BrakeDroplets(npc);
            }

            if (Timer >= CrossFlight + CrossBrake) {
                repIndex++;
                if (repIndex < MaxReps) {
                    SwitchPhase(CrossPhase.RiseFake);
                    return null;
                }
                if (VaultUtils.isClient) {
                    return null;
                }
                return new EocVeilHoverState(context.IsAsuraMode ? 42 : 56);
            }
            return null;
        }

        public override void OnExit(EocStateContext context) {
            base.OnExit(context);
            context.FogHideGoal = 0f;
        }
    }
}

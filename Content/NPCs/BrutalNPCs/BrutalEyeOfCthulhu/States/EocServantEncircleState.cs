using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.States
{
    /// <summary>
    /// 仆从血环合围（二阶段）：血肉之环绕玩家缓缓收拢→全员同时向心扑杀+主眼自上贯穿<br/>
    /// 环半径经主眼 ai[3] 同步驱动仆从槽位，缺口清晰可读，玩家须预判选缝穿出
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EocStateIndex.ServantEncircle, typeof(EocStateContext))]
    internal class EocServantEncircleState : EocStateBase
    {
        public override string StateName => "EocServantEncircle";
        public override EocStateIndex StateIndex => EocStateIndex.ServantEncircle;

        private enum EncirclePhase
        {
            Summon,     //升空召唤
            RingHold,   //血环收拢
            Converge,   //合围扑杀
            Recover,    //回气
        }

        private const int SummonTime = 42;
        private const int RingHoldTime = 96;
        private const int ConvergeTime = 60;
        private const int RecoverTime = 36;
        private const float RingStartRadius = 640f;
        private const float RingEndRadius = 470f;

        private int ServantCount => Context.IsDeathMode ? 8 : 7;
        private float ConvergeSpeed => Context.IsDeathMode ? 27f : 23f;

        private EocStateContext Context;
        private EncirclePhase phase;
        private bool converged;
        private bool diveLaunched;

        public override void OnEnter(EocStateContext context) {
            base.OnEnter(context);
            Context = context;
            phase = EncirclePhase.Summon;
            converged = false;
            diveLaunched = false;
        }

        public override IEocState OnUpdate(EocStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            DisableContactDamage(npc);

            switch (phase) {
                case EncirclePhase.Summon:
                    UpdateSummon(npc, player, context);
                    break;
                case EncirclePhase.RingHold:
                    UpdateRingHold(npc, player, context);
                    break;
                case EncirclePhase.Converge:
                    UpdateConverge(npc, player, context);
                    break;
                case EncirclePhase.Recover:
                    npc.velocity *= 0.9f;
                    FaceTarget(npc, player.Center, 0.2f);
                    Timer++;
                    if (Timer >= RecoverTime) {
                        if (VaultUtils.isClient) {
                            return null;
                        }
                        return new EocVeilHoverState(context.IsDeathMode ? 40 : 54);
                    }
                    break;
            }

            return null;
        }

        private void SwitchPhase(EncirclePhase next) {
            phase = next;
            Timer = 0;
        }

        private void UpdateSummon(NPC npc, Player player, EocStateContext context) {
            Vector2 highPoint = player.Center + new Vector2(0f, -450f);
            EocMotion.SpringHover(npc, highPoint, 0.02f, 0.1f, 26f);
            FaceTarget(npc, player.Center, 0.35f);

            if (Timer == 1) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.9f, Pitch = -0.4f }, npc.Center);
                }
                //环半径初值写 ai[3]，仆从槽位读它
                if (!VaultUtils.isClient) {
                    npc.ai[3] = RingStartRadius;
                    npc.netUpdate = true;
                }
            }

            //权威端环状召唤
            if (Timer == 12 && !VaultUtils.isClient) {
                for (int i = 0; i < ServantCount; i++) {
                    float angle = MathHelper.TwoPi * i / ServantCount;
                    Vector2 spawnPos = player.Center + angle.ToRotationVector2() * (RingStartRadius + 160f);
                    ServantOfCthulhuAI.SpawnFormationServant(npc, spawnPos,
                        ServantOfCthulhuAI.ModeSeek, ServantOfCthulhuAI.FormationRing, i, ServantCount);
                    EocMotion.BloodBurst(spawnPos, 0.45f, playSound: false);
                }
            }

            Timer++;
            if (Timer >= SummonTime) {
                SwitchPhase(EncirclePhase.RingHold);
            }
        }

        private void UpdateRingHold(NPC npc, Player player, EocStateContext context) {
            //主眼压在环顶正上方，凝视等待
            Vector2 highPoint = player.Center + new Vector2(0f, -430f);
            EocMotion.SpringHover(npc, highPoint, 0.016f, 0.1f, 22f);
            FaceTarget(npc, player.Center, 0.4f);

            float progress = Timer / (float)RingHoldTime;
            //环收拢，权威端写，周期 netUpdate 带下去
            if (!VaultUtils.isClient) {
                npc.ai[3] = MathHelper.Lerp(RingStartRadius, RingEndRadius, progress);
            }
            context.SetChargeState(3, progress);
            context.PushIris(0.4f + progress * 0.6f, EocMotion.IrisRed);

            //心跳压迫，节拍渐急
            int beatInterval = (int)MathHelper.Lerp(34f, 16f, progress);
            if (Timer % beatInterval == 0) {
                EocScreenFX.PushPulse(0.4f + progress * 0.4f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.6f, Pitch = -0.75f }, player.Center);
                }
            }
            EocScreenFX.PushVignette(0.24f + progress * 0.14f);

            Timer++;
            if (Timer >= RingHoldTime) {
                converged = false;
                diveLaunched = false;
                SwitchPhase(EncirclePhase.Converge);
            }
        }

        private void UpdateConverge(NPC npc, Player player, EocStateContext context) {
            //合围信号：全员向心+主眼贯穿
            if (!converged) {
                converged = true;
                if (!VaultUtils.isClient) {
                    foreach (NPC n in Main.ActiveNPCs) {
                        if (n.type != NPCID.ServantofCthulhu) {
                            continue;
                        }
                        if ((int)n.ai[0] != ServantOfCthulhuAI.ModeSeek || (int)n.ai[2] != npc.whoAmI) {
                            continue;
                        }
                        Vector2 dir = (player.Center - n.Center).SafeNormalize(Vector2.UnitY);
                        ServantOfCthulhuAI.LaunchServant(n, dir * ConvergeSpeed);
                    }
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.1f, Pitch = 0.05f }, npc.Center);
                }
                EocMotion.Shake(player.Center, 5f, 12);
            }

            //主眼延迟半拍自上贯穿，跟合围错开一层压力
            if (Timer == 14 && !diveLaunched) {
                diveLaunched = true;
                Vector2 diveDir = (player.Center + player.velocity * 8f - npc.Center).SafeNormalize(Vector2.UnitY);
                EocMotion.DashLaunch(npc, context, diveDir, Context.IsDeathMode ? 50f : 45f, 1.15f);
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
            }

            if (diveLaunched) {
                FaceVelocity(npc);
                EnableContactDamageIfFast(npc, 26f, 1.3f);
                context.PushDashVisuals(1f, 1f);
                if (Timer > 38) {
                    npc.velocity *= 0.75f;
                    EocMotion.BrakeDroplets(npc);
                }
            }
            else {
                FaceTarget(npc, player.Center, 0.5f);
            }

            Timer++;
            if (Timer >= ConvergeTime) {
                SwitchPhase(EncirclePhase.Recover);
            }
        }
    }
}

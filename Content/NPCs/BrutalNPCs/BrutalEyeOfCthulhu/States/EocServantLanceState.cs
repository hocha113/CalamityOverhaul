using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.States
{
    /// <summary>
    /// 仆从血枪列：仆从在主眼身后列成纵队→逐发点射→主眼以一记不变轨的坦率冲刺收尾<br/>
    /// 枪列后的直冲永远诚实，与变轨冲刺形成教学对照
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EocStateIndex.ServantLance, typeof(EocStateContext))]
    internal class EocServantLanceState : EocStateBase
    {
        public override string StateName => "EocServantLance";
        public override EocStateIndex StateIndex => EocStateIndex.ServantLance;

        private enum LancePhase
        {
            Rear,       //占位召唤
            Load,       //列队装填
            Fire,       //逐发点射
            Punctuate,  //坦率直冲收尾
        }

        private const int RearTime = 38;
        private const int LoadTime = 62;
        private const int FireInterval = 13;
        private const int PunctuateReel = 16;
        private const int PunctuateFlight = 26;
        private const int PunctuateBrake = 12;

        private int ServantCount => Context.IsAsuraMode ? 5 : 4;
        private float LanceSpeed => Context.IsAsuraMode ? 33f : 29f;

        private EocStateContext Context;
        private LancePhase phase;
        private int fireTimer;
        private bool dashLaunched;

        public override void OnEnter(EocStateContext context) {
            base.OnEnter(context);
            Context = context;
            phase = LancePhase.Rear;
            fireTimer = 0;
            dashLaunched = false;
        }

        public override IEocState OnUpdate(EocStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            DisableContactDamage(npc);

            switch (phase) {
                case LancePhase.Rear:
                    UpdateRear(npc, player, context);
                    break;
                case LancePhase.Load:
                    UpdateLoad(npc, player, context);
                    break;
                case LancePhase.Fire:
                    UpdateFire(npc, player, context);
                    break;
                case LancePhase.Punctuate:
                    return UpdatePunctuate(npc, player, context);
            }

            return null;
        }

        private void SwitchPhase(LancePhase next) {
            phase = next;
            Timer = 0;
        }

        private void UpdateRear(NPC npc, Player player, EocStateContext context) {
            float side = npc.Center.X < player.Center.X ? -1f : 1f;
            Vector2 rearPoint = player.Center + new Vector2(side * 540f, -110f);
            EocMotion.SpringHover(npc, rearPoint, 0.02f, 0.1f, 26f);
            FaceTarget(npc, player.Center, 0.35f);

            if (Timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie3 with { Volume = 0.9f, Pitch = -0.35f }, npc.Center);
            }

            //权威端召唤纵队仆从
            if (Timer == 14 && !VaultUtils.isClient) {
                for (int i = 0; i < ServantCount; i++) {
                    Vector2 spawnPos = npc.Center + Main.rand.NextVector2Circular(60f, 60f);
                    ServantOfCthulhuAI.SpawnFormationServant(npc, spawnPos,
                        ServantOfCthulhuAI.ModeSeek, ServantOfCthulhuAI.FormationLance, i, 0f);
                    EocMotion.BloodBurst(spawnPos, 0.5f, playSound: false);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.8f, Pitch = -0.2f }, npc.Center);
                }
            }

            Timer++;
            if (Timer >= RearTime) {
                SwitchPhase(LancePhase.Load);
            }
        }

        private void UpdateLoad(NPC npc, Player player, EocStateContext context) {
            //稳住阵位，位置漂移随玩家
            float side = npc.Center.X < player.Center.X ? -1f : 1f;
            Vector2 holdPoint = player.Center + new Vector2(side * 540f, -110f);
            EocMotion.SpringHover(npc, holdPoint, 0.012f, 0.1f, 18f);
            FaceTarget(npc, player.Center, 0.4f);
            context.SetChargeState(1, Timer / (float)LoadTime);
            context.PushIris(0.5f + 0.4f * (Timer / (float)LoadTime), EocMotion.Arterial);

            //装填音，从队首到队尾
            if (Timer % 15 == 7 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.5f, Pitch = 0.4f + Timer / (float)LoadTime * 0.3f }, npc.Center);
            }

            Timer++;
            if (Timer >= LoadTime) {
                fireTimer = 0;
                SwitchPhase(LancePhase.Fire);
            }
        }

        private void UpdateFire(NPC npc, Player player, EocStateContext context) {
            float side = npc.Center.X < player.Center.X ? -1f : 1f;
            Vector2 holdPoint = player.Center + new Vector2(side * 540f, -110f);
            EocMotion.SpringHover(npc, holdPoint, 0.012f, 0.1f, 16f);
            FaceTarget(npc, player.Center, 0.5f);

            fireTimer++;
            Timer++;

            if (fireTimer >= FireInterval) {
                fireTimer = 0;

                if (!VaultUtils.isClient) {
                    NPC front = FindFrontServant(npc);
                    if (front != null) {
                        Vector2 predicted = EocMotion.PredictTarget(player, front.Center, LanceSpeed, 0.65f);
                        Vector2 dir = (predicted - front.Center).SafeNormalize(Vector2.UnitY);
                        ServantOfCthulhuAI.LaunchServant(front, dir * LanceSpeed);
                        //主眼后坐，质量语言
                        npc.velocity -= dir * 8f;
                        npc.netUpdate = true;
                    }
                }
                //出膛演出（本地按最近的出击仆从判断不了，就地放主眼口沫）
                context.PushIris(0.9f, EocMotion.BrightBlood);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.75f, Pitch = 0.1f }, npc.Center);
                }
            }

            //列队清空或超时→收尾直冲
            bool anyLeft = FindFrontServant(npc) != null;
            if ((!anyLeft && fireTimer > 8) || Timer >= FireInterval * (ServantCount + 2)) {
                dashLaunched = false;
                SwitchPhase(LancePhase.Punctuate);
            }
        }

        private IEocState UpdatePunctuate(NPC npc, Player player, EocStateContext context) {
            Timer++;

            if (Timer <= PunctuateReel) {
                //快速小后撤
                float progress = Timer / (float)PunctuateReel;
                Vector2 awayDir = (npc.Center - player.Center).SafeNormalize(Vector2.UnitY);
                EocMotion.ReelBack(npc, awayDir, progress, 4f);
                FaceTarget(npc, player.Center, 0.6f);
                context.SetChargeState(1, progress);
                if (Timer == 1 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.8f, Pitch = -0.45f }, npc.Center);
                }
                return null;
            }

            if (!dashLaunched) {
                dashLaunched = true;
                context.ResetChargeState();
                Vector2 dir = (EocMotion.PredictTarget(player, npc.Center, 41f, 0.5f) - npc.Center)
                    .SafeNormalize(Vector2.UnitY);
                //坦率直冲：不变轨，教学对照
                EocMotion.DashLaunch(npc, context, dir, 41f);
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
            }

            FaceVelocity(npc);
            EnableContactDamageIfFast(npc, 25f, 1.1f);
            context.PushDashVisuals(1f, 1f);

            if (Timer > PunctuateReel + PunctuateFlight) {
                npc.velocity *= 0.7f;
                EocMotion.BrakeDroplets(npc);
            }

            if (Timer >= PunctuateReel + PunctuateFlight + PunctuateBrake) {
                if (VaultUtils.isClient) {
                    return null;
                }
                return new EocVeilHoverState(context.IsAsuraMode ? 44 : 60);
            }
            return null;
        }

        /// <summary>找槽位最小的待发仆从</summary>
        private static NPC FindFrontServant(NPC director) {
            NPC best = null;
            int bestSlot = int.MaxValue;
            foreach (NPC n in Main.ActiveNPCs) {
                if (n.type != NPCID.ServantofCthulhu) {
                    continue;
                }
                if ((int)n.ai[0] != ServantOfCthulhuAI.ModeSeek || (int)n.ai[2] != director.whoAmI) {
                    continue;
                }
                int slot = (int)n.ai[1] % 100;
                if (slot < bestSlot) {
                    bestSlot = slot;
                    best = n;
                }
            }
            return best;
        }
    }
}

using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States
{
    /// <summary>头部分离仪式（阶段转换演出）：锁扣崩开 → 头颅升空 → 光柱换体 → 二阶段宣告</summary>
    [InnoVault.StateMachines.VaultState((int)GolemStateIndex.HeadDetach, typeof(GolemStateContext))]
    internal class GolemHeadDetachState : GolemStateBase
    {
        public override string StateName => "HeadDetach";
        public override GolemStateIndex StateIndex => GolemStateIndex.HeadDetach;

        internal static int SwapTick => 152;   //换体瞬间（附着头→分离头）
        internal static int RoarTick => 190;   //二阶段宣告
        internal static int EndTime => 236;

        public override void OnEnter(GolemStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;

            //立刻挂二阶段标记，防重复进仪式
            npc.ai[GolemAiSlots.BodyPhase] = GolemPhase.Sundered;

            if (!VaultUtils.isClient) {
                npc.TargetClosest();
                npc.netUpdate = true;
                //公平阀：清场敌方弹幕
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.hostile) {
                        p.Kill();
                    }
                }
                //双拳收拢护卫
                GolemLimbStatus limbs = context.Limbs;
                if (limbs.LeftFistAlive) {
                    GolemBodyAI.CommandFist(limbs.LeftFistIndex, GolemFistCommand.GuardOrbit, npc.Center, 20, 20f, 0);
                }
                if (limbs.RightFistAlive) {
                    GolemBodyAI.CommandFist(limbs.RightFistIndex, GolemFistCommand.GuardOrbit, npc.Center, 20, 20f, 0);
                }
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.7f, Volume = 0.6f }, npc.Center);
                GolemScreenEffects.PushShockRing(npc.Center, 0.7f, 520f);
            }
        }

        public override IGolemState OnUpdate(GolemStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            npc.dontTakeDamage = true;
            npc.noTileCollide = false;
            GroundBrake(npc);
            context.FrameMode = 1;

            //每帧重申阶段标记
            npc.ai[GolemAiSlots.BodyPhase] = GolemPhase.Sundered;

            //仪式全程宝石炽热
            float ritualT = MathHelper.Clamp(Timer / (float)SwapTick, 0f, 1f);
            context.SetChargeState(2, ritualT);
            context.VeinGlow = Math.Max(context.VeinGlow, ritualT);

            //躯干震颤加剧
            if (!VaultUtils.isServer && Timer > 40 && Timer % Math.Max(18 - Timer / 14, 4) == 0) {
                GolemScreenEffects.Shake(1.2f + ritualT * 2.4f);
            }

            //换体瞬间：附着头静默退场，分离头原地接棒
            if (Timer == SwapTick) {
                if (!VaultUtils.isClient) {
                    GolemLimbStatus limbs = context.Limbs;
                    Vector2 headPos = GolemFacts.HeadAnchor(npc) - new Vector2(0f, 220f);
                    if (limbs.HeadAlive) {
                        NPC head = Main.npc[limbs.HeadIndex];
                        headPos = head.Center;
                        GolemFacts.FindOverride<GolemHeadAI>(head)?.SilentRemoveOnServer();
                    }
                    SpawnFreeHeadAt(context, headPos);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.4f, Volume = 1f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.1f, Volume = 0.8f }, npc.Center);
                    GolemScreenEffects.PushSunFlash(GolemFacts.HeadAnchor(npc) - new Vector2(0f, 220f), 0.55f, 24);
                    GolemScreenEffects.PushShockRing(GolemFacts.HeadAnchor(npc), 0.9f, 700f);
                    GolemScreenEffects.Shake(6f);
                    //颈口喷发
                    for (int i = 0; i < 26; i++) {
                        Vector2 neck = GolemFacts.HeadAnchor(npc) + new Vector2(Main.rand.NextFloat(-16f, 16f), 6f);
                        PRTLoader.NewParticle<PRT_Spark>(neck,
                            new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-9f, -4f)),
                            new Color(255, 205, 100), Main.rand.NextFloat(1f, 1.5f)).Configure(true, 26);
                    }
                    for (int i = 0; i < 10; i++) {
                        PRTLoader.NewParticle<PRT_MarbleChip>(GolemFacts.HeadAnchor(npc) + Main.rand.NextVector2Circular(24f, 10f),
                            new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-6f, -2f)),
                            new Color(122, 104, 78), Main.rand.NextFloat(0.8f, 1.3f)).Configure(44);
                    }
                }
            }

            //二阶段宣告：重踏 + 环波
            if (Timer == RoarTick && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.35f }, npc.Center);
                GolemScreenEffects.Shake(5f);
                GolemScreenEffects.PushShockRing(npc.Bottom, 0.8f, 600f);
            }

            Timer++;
            if (Timer >= EndTime) {
                npc.dontTakeDamage = false;
                if (!VaultUtils.isClient) {
                    return new GolemConnectorState();
                }
            }
            return null;
        }

        /// <summary>生成分离头（服务端）</summary>
        private static void SpawnFreeHeadAt(GolemStateContext context, Vector2 pos) {
            NPC npc = context.Npc;
            int index = NPC.NewNPC(npc.GetSource_FromAI(), (int)pos.X, (int)pos.Y, NPCID.GolemHeadFree);
            if (index < 0 || index >= Main.maxNPCs) {
                return;
            }
            NPC part = Main.npc[index];
            part.ai[GolemAiSlots.PartBodyIndex] = npc.whoAmI;
            part.target = npc.target;
            part.velocity = new Vector2(0f, -4f);
            part.netUpdate = true;
        }
    }
}

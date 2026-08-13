using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.States
{
    /// <summary>66% 转阶段演出：三声递进嘶吼、器官补员、进入全招池</summary>
    [InnoVault.StateMachines.VaultState((int)WofStateIndex.PhaseTransition, typeof(WofStateContext))]
    internal class WofPhaseTransitionState : WofStateBase
    {
        public override string StateName => "PhaseTransition";
        public override WofStateIndex StateIndex => WofStateIndex.PhaseTransition;

        private const int RoarInterval = 38;
        private const int TotalTime = 140;

        public override IWofState OnUpdate(WofStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            //演出期慢爬，攒足压迫
            context.AdvanceFactor = 0.3f;
            context.MouthCommand = Timer % RoarInterval < 14 ? 1 : 2;
            float p = Timer / (float)TotalTime;
            context.SetChargeState(3, p);
            context.WallFlush = 0.6f + 0.4f * p;

            //三声递进嘶吼
            if (Timer % RoarInterval == 1 && Timer < RoarInterval * 3 + 2 && !VaultUtils.isServer) {
                int roarIndex = Timer / RoarInterval;
                float power = 0.8f + roarIndex * 0.35f;
                WofMotionFX.MouthRoar(npc, power);
                //沿墙面喷发碎肉
                for (int i = 0; i < 3 + roarIndex * 2; i++) {
                    float y = Main.rand.NextFloat(WofWallField.Top, WofWallField.Bottom);
                    WofMotionFX.SpawnBloodBurst(new Vector2(WofWallField.WallFaceX(npc), y),
                        0.7f + roarIndex * 0.3f, new Vector2(npc.direction, 0f));
                }
            }

            //器官补员(服务端)：饥饿者补满缺口
            if (Timer == RoarInterval * 2 && !VaultUtils.isClient) {
                RespawnHungries(context);
            }

            if (Timer >= TotalTime) {
                //写入阶段位：ai[1]=2，随NPC同步
                if (!VaultUtils.isClient) {
                    npc.ai[1] = 2f;
                    npc.netUpdate = true;
                }
                context.Phase = 2;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Volume = 0.85f, Pitch = -0.4f }, npc.Center);
                }
                return new WofAdvanceState();
            }
            return null;
        }

        /// <summary>按空缺锚位补员饥饿者(镜像原版槽位分布)</summary>
        internal static void RespawnHungries(WofStateContext context) {
            NPC npc = context.Npc;
            List<NPC> alive = context.CollectHungries();
            const int maxSlots = 11;
            bool[] used = new bool[maxSlots];
            foreach (NPC hungry in alive) {
                int slot = (int)System.Math.Round((hungry.ai[0] + 0.05f) / 0.1f);
                if (slot >= 0 && slot < maxSlots) {
                    used[slot] = true;
                }
            }
            float spawnY = (npc.Center.Y + WofWallField.Bottom) / 2f;
            for (int i = 0; i < maxSlots; i++) {
                if (used[i]) {
                    continue;
                }
                int hungryIndex = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.position.X, (int)spawnY,
                    NPCID.TheHungry, npc.whoAmI, i * 0.1f - 0.05f);
                if (hungryIndex < Main.maxNPCs) {
                    Main.npc[hungryIndex].netUpdate = true;
                }
            }
        }
    }
}

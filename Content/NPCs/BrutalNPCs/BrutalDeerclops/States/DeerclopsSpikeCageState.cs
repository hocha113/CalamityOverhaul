using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States
{
    /// <summary>
    /// 合拢牢笼：两侧破土前沿向玩家所在合拢，留出口袋；随后口袋中心延迟爆发。
    /// 出路=跳过行进前沿，或从已退潮的一侧撤出；一侧留缺口
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)DeerclopsStateIndex.SpikeCage, typeof(DeerclopsStateContext))]
    internal class DeerclopsSpikeCageState : DeerclopsStateBase
    {
        public override string StateName => "SpikeCage";
        public override DeerclopsStateIndex StateIndex => DeerclopsStateIndex.SpikeCage;

        private const int Slam1 = 36;
        private const int Stomp2Start = 118;
        private const int Slam2 = Stomp2Start + 36;
        private const int StateEnd = 212;

        public override IDeerclopsState OnUpdate(DeerclopsStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            context.HaltMovement = true;
            npc.damage = 0;
            FaceTarget(context);

            if (Timer <= Slam1 + 20) {
                context.AnimMode = DeerAnimMode.Stomp;
                context.AnimTimer = Timer;
            }
            else if (Timer > Stomp2Start && Timer <= Slam2 + 12) {
                context.AnimMode = DeerAnimMode.Stomp;
                context.AnimTimer = Timer - Stomp2Start;
            }

            if (Timer == Slam1) {
                SlamFeedback(npc, 7f);
                SpawnConvergingWalls(context);
            }

            if (Timer == Slam2) {
                SlamFeedback(npc, 8.5f);
                SpawnPocketBurst(context);
            }

            if (Timer >= StateEnd) {
                return new DeerclopsStalkState();
            }
            return null;
        }

        private static void SlamFeedback(NPC npc, float strength) {
            DeerclopsMotion.CameraPunch(npc.Bottom, strength, 22, "DeerCageSlam", Vector2.UnitY);
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.DeerclopsRubbleAttack with { Volume = 0.9f, Pitch = -0.15f }, npc.Bottom);
            }
        }

        /// <summary>两侧合拢前沿。锚定施法瞬间的玩家脚下，缺口侧由锚点奇偶决定(确定性)</summary>
        private void SpawnConvergingWalls(DeerclopsStateContext context) {
            if (VaultUtils.isClient) {
                return;
            }
            NPC npc = context.Npc;
            Player player = context.Target;
            if (player == null) {
                return;
            }

            Point anchor = player.Bottom.ToTileCoordinates();
            int damage = context.IsDeathMode ? 20 : 16;
            bool phase2 = context.IsPhase2;
            int pocketHalf = phase2 ? 6 : 7;
            int outerHalf = 44;
            int spacing = 2;
            int columnsPerSide = (outerHalf - pocketHalf) / spacing;
            int gapSide = anchor.X % 2 == 0 ? 1 : -1;

            for (int side = -1; side <= 1; side += 2) {
                for (int j = 0; j < columnsPerSide; j++) {
                    //缺口列：缺口侧每7列留一列，成为撤出通道
                    if (side == gapSide && j % 7 == 3) {
                        continue;
                    }
                    int tileX = anchor.X + side * (outerHalf - j * spacing);
                    float lean = side * 0.16f;
                    //外圈墙体长驻，内圈快退让路
                    bool longHold = j < 4;
                    float scale = MathHelper.Clamp(1.25f - j * 0.02f, 0.7f, 1.3f) + (phase2 ? 0.15f : 0f);
                    int telegraph = TelegraphTime(context, 12 + j * (phase2 ? 3 : 4), 10);
                    DeerIceSpikeProj.TrySpawn(npc, tileX, anchor.Y, lean, scale, telegraph, damage, longHold);
                }
            }
        }

        /// <summary>口袋中心延迟爆发，惩罚原地不动；预兆裂隙给足30帧</summary>
        private void SpawnPocketBurst(DeerclopsStateContext context) {
            if (VaultUtils.isClient) {
                return;
            }
            NPC npc = context.Npc;
            Player player = context.Target;
            if (player == null) {
                return;
            }

            //爆心用当前玩家位置(追击口袋里的人)
            Point center = player.Bottom.ToTileCoordinates();
            int damage = context.IsDeathMode ? 21 : 17;
            for (int i = -2; i <= 2; i++) {
                int tileX = center.X + i * 2;
                float lean = Math.Sign(i) * 0.22f;
                float scale = 1.5f - Math.Abs(i) * 0.12f;
                int telegraph = TelegraphTime(context, 30 + Math.Abs(i) * 3, 22);
                DeerIceSpikeProj.TrySpawn(npc, tileX, center.Y, lean, scale, telegraph, damage);
            }
        }
    }
}

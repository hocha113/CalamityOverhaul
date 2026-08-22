using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States
{
    /// <summary>
    /// 冰霜震荡：两次跺地，放出沿地表双向行进的霜脉冲(必须跳越)。
    /// 第二次更快，二阶段追加错拍第三对，连跳考验
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)DeerclopsStateIndex.FrostQuake, typeof(DeerclopsStateContext))]
    internal class DeerclopsFrostQuakeState : DeerclopsStateBase
    {
        public override string StateName => "FrostQuake";
        public override DeerclopsStateIndex StateIndex => DeerclopsStateIndex.FrostQuake;

        private const int Slam1 = 36;
        private const int Stomp2Start = 68;
        private const int Slam2 = Stomp2Start + 36;
        private const int ExtraPairTime = Slam2 + 20;
        private const int StateEnd = 162;

        public override IDeerclopsState OnUpdate(DeerclopsStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            context.HaltMovement = true;
            npc.damage = 0;
            FaceTarget(context);

            if (Timer <= Stomp2Start) {
                context.AnimMode = DeerAnimMode.Stomp;
                context.AnimTimer = Timer;
            }
            else if (Timer <= Slam2 + 12) {
                context.AnimMode = DeerAnimMode.Stomp;
                context.AnimTimer = Timer - Stomp2Start;
            }

            if (Timer == Slam1) {
                SlamFeedback(npc, 8f);
                SpawnPulsePair(context, 13f);
            }

            if (Timer == Slam2) {
                SlamFeedback(npc, 9f);
                SpawnPulsePair(context, 17f);
            }

            //二阶段错拍第三对：前两对的节奏刚学会就被打乱
            if (context.IsPhase2 && Timer == ExtraPairTime) {
                SpawnPulsePair(context, 15f);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DeerclopsIceAttack with { Volume = 0.8f, Pitch = -0.3f }, npc.Bottom);
                }
            }

            if (Timer >= StateEnd) {
                return new DeerclopsStalkState();
            }
            return null;
        }

        private static void SlamFeedback(NPC npc, float strength) {
            DeerclopsMotion.CameraPunch(npc.Bottom, strength, 24, "DeerQuakeSlam", Vector2.UnitY);
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.DeerclopsRubbleAttack with { Volume = 1f, Pitch = -0.25f }, npc.Bottom);
                SoundEngine.PlaySound(SoundID.Item70 with { Volume = 0.6f, Pitch = -0.4f }, npc.Bottom);
                for (int i = 0; i < 14; i++) {
                    Dust dust = Dust.NewDustPerfect(npc.Bottom + new Vector2(Main.rand.NextFloat(-60f, 60f), 0f),
                        DustID.Snow, new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), -Main.rand.NextFloat(1.5f, 5f)), 70, default, Main.rand.NextFloat(1.1f, 2f));
                    dust.noGravity = Main.rand.NextBool(3);
                }
            }
        }

        private void SpawnPulsePair(DeerclopsStateContext context, float speed) {
            if (VaultUtils.isClient) {
                return;
            }
            NPC npc = context.Npc;
            int damage = context.IsDeathMode ? 22 : 18;
            Vector2 spawnPos = npc.Bottom - new Vector2(0f, 20f);
            for (int dir = -1; dir <= 1; dir += 2) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, Vector2.Zero,
                    ModContent.ProjectileType<DeerFrostPulseProj>(), damage, 0f, Main.myPlayer,
                    dir * speed, 1500f);
            }
        }
    }
}

using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.States
{
    /// <summary>撤离：玩家全灭后按原版语义吼叫并消散(3秒宽限，期间复活可拉回)</summary>
    [InnoVault.StateMachines.VaultState((int)WofStateIndex.Despawn, typeof(WofStateContext))]
    internal class WofDespawnState : WofStateBase
    {
        public override string StateName => "Despawn";
        public override WofStateIndex StateIndex => WofStateIndex.Despawn;

        private const int TotalTime = 180;

        public override void OnEnter(WofStateContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath10 with { Volume = 1f, Pitch = -0.2f }, context.Npc.Center);
            }
        }

        public override IWofState OnUpdate(WofStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            //有玩家复活则重返战斗(服务端决策，镜像原版计时回落)
            if (!VaultUtils.isClient && context.Target.Alives()) {
                return new WofAdvanceState();
            }

            context.AdvanceFactor = MathHelper.Lerp(1f, 0.45f, Timer / (float)TotalTime);
            context.WallFlush = MathHelper.Lerp(0.4f, 0f, Timer / (float)TotalTime);
            context.MouthCommand = 2;

            //血雾渐浓，墙在雾中退场
            if (!VaultUtils.isServer && Timer % 4 == 0) {
                float y = Main.rand.NextFloat(WofWallField.Top, WofWallField.Bottom);
                WofMotionFX.SpawnWallSeep(npc, 2f);
                InnoVault.PRT.PRTLoader.NewParticle<PRT_WofBloodMist>(
                    new Vector2(WofWallField.WallFaceX(npc) - npc.direction * Main.rand.NextFloat(0f, 200f), y),
                    new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.5f)),
                    WofMotionFX.BloodDark, Main.rand.NextFloat(1.2f, 2f))?.Configure(Main.rand.Next(50, 80), 0.6f);
            }

            //离场前一拍关滤镜
            if (Timer >= TotalTime - 1) {
                WallOfFleshAI.ShutdownFilter();
            }

            //原版语义：吼叫后life=0直接消失，无掉落
            if (Timer >= TotalTime && !VaultUtils.isClient) {
                SoundEngine.PlaySound(SoundID.NPCDeath10, npc.Center);
                npc.life = 0;
                npc.active = false;
                if (Main.netMode != NetmodeID.SinglePlayer) {
                    NetMessage.SendData(MessageID.DamageNPC, -1, -1, null, npc.whoAmI, -1f);
                }
            }
            return null;
        }
    }
}

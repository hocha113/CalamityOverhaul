using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>短路，电磁脉冲即时伤害</summary>
    internal class ShortCircuit : QuickHackDef
    {
        public override void SetDefaults() {
            UploadTime = 60;
            RamCost = 2;
            Category = QuickHackCategory.Lethal;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not NpcScannable s) return false;
            NPC npc = Main.npc[s.NpcIndex];
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int dmg = Math.Max(30, (int)(npc.lifeMax * 0.02f));
                npc.SimpleStrikeNPC(dmg, 0, false, 0f, null, false, 0f, true);
            }
            if (Main.netMode != NetmodeID.Server) EmitVisual(npc);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (target is NpcScannable s && s.NpcIndex >= 0
                && s.NpcIndex < Main.maxNPCs && Main.npc[s.NpcIndex].active)
                EmitVisual(Main.npc[s.NpcIndex]);
        }

        private static void EmitVisual(NPC npc) {
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, new Color(100, 180, 255), 1.5f).Configure(false, 15);
            }
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2f, 2f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, Color.White, 2.0f).Configure(false, 8);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.ShortCircuit, npc.Center);
            }
        }
    }
}

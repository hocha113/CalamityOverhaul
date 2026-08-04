using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>赛博精神病，狂暴攻击周围单位</summary>
    internal class Cyberpsychosis : QuickHackDef
    {
        public override void SetDefaults() {
            UploadTime = 150;
            RamCost = 5;
            Category = QuickHackCategory.Control;
        }

        public override int GetDuration() => 60 * 8; //8秒持续

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not NpcScannable s) return false;
            NPC npc = Main.npc[s.NpcIndex];
            if (Main.netMode != NetmodeID.Server) EmitApplyVisual(npc);
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                HackEffectTracker.PropagateNpcEffectToGroup(this, s.NpcIndex,
                    caster?.whoAmI ?? Main.myPlayer,
                    Main.netMode == NetmodeID.Server ? null : EmitBurstParticles);
            }
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (target is NpcScannable s && s.NpcIndex >= 0
                && s.NpcIndex < Main.maxNPCs && Main.npc[s.NpcIndex].active)
                EmitApplyVisual(Main.npc[s.NpcIndex]);
        }

        private static void EmitApplyVisual(NPC npc) {
            EmitBurstParticles(npc);
            CombatText.NewText(npc.Hitbox, new Color(255, 0, 50),
                HackTime.Cyberpsychosis.Value, true);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (target is not NpcScannable s) return true;
            NPC npc = Main.npc[s.NpcIndex];
            if (Main.netMode != NetmodeID.Server) EmitTickVisual(npc, elapsed);
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (target is not NpcScannable s || s.NpcIndex < 0
                || s.NpcIndex >= Main.maxNPCs) return;
            NPC npc = Main.npc[s.NpcIndex];
            if (npc.active) EmitTickVisual(npc, elapsed);
        }

        private static void EmitTickVisual(NPC npc, int elapsed) {
            if (elapsed % 10 == 0) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(
                    npc.width * 0.4f, npc.height * 0.4f);
                Vector2 vel = Main.rand.NextVector2Circular(2f, 2f);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, new Color(255, 50, 50), 0.6f).Configure(false, 15);
            }
        }

        public override void OnRemove(IHackTarget target) {
            if (target is not NpcScannable s) return;
            NPC npc = Main.npc[s.NpcIndex];
            if (Main.netMode != NetmodeID.Server) EmitRemoveVisual(npc);
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (target is NpcScannable s && s.NpcIndex >= 0
                && s.NpcIndex < Main.maxNPCs && Main.npc[s.NpcIndex].active)
                EmitRemoveVisual(Main.npc[s.NpcIndex]);
        }

        private static void EmitRemoveVisual(NPC npc) {
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2f, 2f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, new Color(180, 80, 80), 0.5f).Configure(false, 15);
            }
        }

        //群组成员复用
        private static void EmitBurstParticles(NPC npc) {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.5f, 3.5f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, new Color(255, 30, 30), 1.0f).Configure(false, 30);
            }
        }
    }
}

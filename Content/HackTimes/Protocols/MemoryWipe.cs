using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>记忆清除，抹除仇恨</summary>
    internal class MemoryWipe : QuickHackDef
    {
        public override void SetDefaults() {
            UploadTime = 80;
            RamCost = 3;
            Category = QuickHackCategory.Covert;
        }

        public override int GetDuration() => 60 * 5; //5秒失忆

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not NpcScannable s) return false;
            NPC npc = Main.npc[s.NpcIndex];
            if (Main.netMode != NetmodeID.Server) EmitApplyVisual(npc);
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                HackEffectTracker.PropagateNpcEffectToGroup(this, s.NpcIndex,
                    caster?.whoAmI ?? Main.myPlayer,
                    Main.netMode == NetmodeID.Server ? null : EmitApplyParticles);
            }
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (target is NpcScannable s && s.NpcIndex >= 0
                && s.NpcIndex < Main.maxNPCs && Main.npc[s.NpcIndex].active)
                EmitApplyVisual(Main.npc[s.NpcIndex]);
        }

        private static void EmitApplyVisual(NPC npc) {
            EmitApplyParticles(npc);
            CombatText.NewText(npc.Hitbox, new Color(80, 255, 200),
                HackTime.MemoryWiped.Value, true);
        }

        //群组成员复用
        private static void EmitApplyParticles(NPC npc) {
            for (int i = 0; i < 10; i++) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(
                    npc.width * 0.3f, npc.height * 0.3f);
                Vector2 vel = new(0, Main.rand.NextFloat(-1.5f, -0.3f));
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, new Color(50, 255, 180), 0.7f).Configure(false, 30);
            }
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
            if (elapsed % 12 == 0) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(
                    npc.width * 0.4f, npc.height * 0.4f);
                Vector2 vel = new(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-1f, -0.2f));
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, new Color(0, 220, 150), 0.4f).Configure(false, 20);
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
            for (int i = 0; i < 4; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(1.5f, 1.5f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, new Color(0, 180, 120), 0.5f).Configure(false, 15);
            }
        }
    }
}

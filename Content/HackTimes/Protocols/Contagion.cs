using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>蔓延，到期向附近 NPC 一跳扩散</summary>
    internal class Contagion : QuickHackDef
    {
        /// <summary>扩散搜索半径（像素）</summary>
        public const float SpreadRadius = 400f;

        public override void SetDefaults() {
            UploadTime = 100;
            RamCost = 4;
            Category = QuickHackCategory.Contagion;
        }

        public override int GetDuration() => 60 * 6; //6秒后扩散

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not NpcScannable s) return false;
            NPC npc = Main.npc[s.NpcIndex];
            if (Main.netMode != NetmodeID.Server) EmitApplyVisual(npc);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (target is NpcScannable s && s.NpcIndex >= 0
                && s.NpcIndex < Main.maxNPCs && Main.npc[s.NpcIndex].active)
                EmitApplyVisual(Main.npc[s.NpcIndex]);
        }

        private static void EmitApplyVisual(NPC npc) {
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, new Color(30, 220, 60), 1.0f).Configure(false, 25);
            }
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (target is not NpcScannable s) return true;
            NPC npc = Main.npc[s.NpcIndex];
            //每 20 帧 15 伤
            if (elapsed % 20 == 0) {
                npc.SimpleStrikeNPC(15, 0, false, 0f, null, false, 0f, true);
            }
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
            if (elapsed % 6 == 0) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(
                    npc.width * 0.3f, npc.height * 0.3f);
                Vector2 vel = new(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1.5f, 0f));
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, new Color(50, 255, 80), 0.6f).Configure(false, 20);
            }
        }

        public override void OnRemove(IHackTarget target) {
            if (target is not NpcScannable s) return;
            NPC npc = Main.npc[s.NpcIndex];
            var eff = HackEffectTracker.GetEffect<Contagion>(npc.whoAmI);
            //二代不再扩散（一跳）
            if (eff != null && eff.Generation > 0) return;

            int casterIdx = eff?.CasterIndex ?? Main.myPlayer;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC other = Main.npc[i];
                if (!other.active || other.whoAmI == npc.whoAmI
                    || other.friendly || other.dontTakeDamage) continue;
                if (HackEffectTracker.HasEffect<Contagion>(other.whoAmI)) continue;

                float dist = Vector2.Distance(npc.Center, other.Center);
                if (dist > SpreadRadius) continue;

                var newEff = HackEffectTracker.Apply(Get<Contagion>(), other.whoAmI, casterIdx);
                if (newEff != null) {
                    newEff.Generation = 1; //二代
                }

                if (Main.netMode != NetmodeID.Server)
                    EmitSpreadVisual(npc.Center, other.Center);
            }

            if (Main.netMode != NetmodeID.Server) EmitRemoveVisual(npc);
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (target is NpcScannable s && s.NpcIndex >= 0
                && s.NpcIndex < Main.maxNPCs && Main.npc[s.NpcIndex].active)
                EmitRemoveVisual(Main.npc[s.NpcIndex]);
        }

        private static void EmitSpreadVisual(Vector2 from, Vector2 to) {
            for (int i = 0; i < 6; i++) {
                float t = i / 6f;
                Vector2 pos = Vector2.Lerp(from, to, t);
                PRTLoader.NewParticle<PRT_Spark>(pos,
                    Main.rand.NextVector2Circular(1f, 1f),
                    new Color(60, 255, 90), 0.5f).Configure(false, 20);
            }
        }

        private static void EmitRemoveVisual(NPC npc) {
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, new Color(80, 255, 120), 0.8f).Configure(false, 20);
            }
        }
    }
}

using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;

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
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, new Color(30, 220, 60), 1.0f).Configure(false, 25);
            }
            return true;
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (target is not NpcScannable s) return true;
            NPC npc = Main.npc[s.NpcIndex];
            //每 20 帧 15 伤
            if (elapsed % 20 == 0) {
                npc.SimpleStrikeNPC(15, 0, false, 0f, null, false, 0f, true);
            }
            if (elapsed % 6 == 0) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(
                    npc.width * 0.3f, npc.height * 0.3f);
                Vector2 vel = new(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1.5f, 0f));
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, new Color(50, 255, 80), 0.6f).Configure(false, 20);
            }
            return true;
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

                for (int j = 0; j < 6; j++) {
                    float t = j / 6f;
                    Vector2 pos = Vector2.Lerp(npc.Center, other.Center, t);
                    PRTLoader.NewParticle<PRT_Spark>(pos, Main.rand.NextVector2Circular(1f, 1f), new Color(60, 255, 90), 0.5f).Configure(false, 20);
                }
            }

            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, new Color(80, 255, 120), 0.8f).Configure(false, 20);
            }
        }
    }
}
